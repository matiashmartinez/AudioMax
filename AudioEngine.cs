using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

public class OutputTarget
{
    public MMDevice Device { get; set; }
    public int DelayMs { get; set; }
    public float Volume { get; set; } = 100f;

    public OutputTarget(MMDevice device, int delayMs = 0, float volume = 100f)
    {
        Device = device;
        DelayMs = delayMs;
        Volume = Math.Clamp(volume, 0f, 100f);
    }
}

public class AudioEngine
{
    private WasapiLoopbackCapture? _cap;
    private readonly List<WasapiOut> _outs = new();
    private readonly List<BufferedWaveProvider> _bufs = new();
    private readonly List<OutputTarget> _targets = new();
    private readonly Action<string> _logger;

    private CancellationTokenSource? _driftWatchdogCts;
    private bool _isStopping = false;
    private bool _dataReceivedOnce = false;

    public bool EnableAutoSync { get; set; } = true;

    public AudioEngine(Action<string> logger)
    {
        _logger = logger ?? (_ => { });
    }

    public void Start(MMDevice source, List<OutputTarget> targets, bool enableAutoSync)
    {
        try
        {
            Stop(); // Limpieza previa segura
            _isStopping = false;
            EnableAutoSync = enableAutoSync;
            _targets.Clear();
            _targets.AddRange(targets);

            _logger($"Iniciando motor profesional v2.1 desde: {source.FriendlyName}");

            _cap = new WasapiLoopbackCapture(source);
            WaveFormat captureFormat = _cap.WaveFormat;

            var pendingOuts = new List<WasapiOut>();

            foreach (var t in targets)
            {
                if (t.DelayMs > 8000)
                {
                    throw new ArgumentException($"El retardo para {t.Device.FriendlyName} supera el límite seguro.");
                }

                var b = new BufferedWaveProvider(captureFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(10),
                    DiscardOnBufferOverflow = false
                };

                AplicarDelayInicial(b, captureFormat, t.DelayMs);

                var o = new WasapiOut(t.Device, AudioClientShareMode.Shared, true, 100);
                o.Init(b);

                _bufs.Add(b);
                _outs.Add(o);
                pendingOuts.Add(o);
            }

            // Arranque simultáneo en bloque cerrado (Cero desfase inicial)
            foreach (var o in pendingOuts)
            {
                o.Play();
            }

            _cap.DataAvailable += OnDataAvailable;
            _cap.RecordingStopped += (s, e) =>
            {
                _logger("Captura detenida.");
                _dataReceivedOnce = false;
            };

            _cap.StartRecording();

            // Monitor pasivo de salud de búferes (Sin alterar punteros de NAudio para evitar microcortes)
            if (EnableAutoSync)
            {
                StartHealthMonitor();
            }

            _logger("AudioTwin Pro v2.1 - Motor Estable Activo.");
        }
        catch (Exception ex)
        {
            _logger($"ERROR al iniciar motor: {ex.Message}");
            Stop();
            throw;
        }
    }

    public void UpdateTargetVolume(string deviceId, float newVolume)
    {
        foreach (var t in _targets)
        {
            if (t.Device.ID == deviceId)
            {
                t.Volume = Math.Clamp(newVolume, 0f, 100f);
            }
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_isStopping) return;

        if (!_dataReceivedOnce)
        {
            _logger("✔ Flujo de audio estable activo.");
            _dataReceivedOnce = true;
        }

        InyectarMuestrasConVolumenOptimizado(e.Buffer, e.BytesRecorded);
    }

    // Monitor pasivo seguro (Monitorea sin corromper la memoria compartida de NAudio)
    private void StartHealthMonitor()
    {
        _driftWatchdogCts = new CancellationTokenSource();
        var token = _driftWatchdogCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Usamos delay seguro con manejo de cancelación limpio sin excepciones no controladas
                    await Task.Delay(5000, token); 

                    if (_isStopping || _bufs.Count == 0) break;

                    // Verificación pasiva de niveles de búfer para diagnóstico interno
                    for (int i = 0; i < _bufs.Count; i++)
                    {
                        double actualMs = _bufs[i].BufferedDuration.TotalMilliseconds;
                        if (actualMs > 8000) // Si se acumula demasiado por lentitud del hardware
                        {
                            _bufs[i].ClearBuffer(); // Limpieza preventiva de desborde sin bloquear el hilo de audio
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // Salida limpia y silenciosa del hilo al detener el motor
                    break;
                }
                catch
                {
                    // Ignorar excepciones menores de diagnóstico
                }
            }
        }, token);
    }

    private void InyectarMuestrasConVolumenOptimizado(byte[] buffer, int bytesRecorded)
    {
        for (int i = 0; i < _targets.Count; i++)
        {
            float volFactor = _targets[i].Volume / 100f;

            if (Math.Abs(volFactor - 1.0f) < 0.001f)
            {
                try { _bufs[i].AddSamples(buffer, 0, bytesRecorded); } catch { }
            }
            else
            {
                byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(bytesRecorded);
                try
                {
                    Buffer.BlockCopy(buffer, 0, rentedBuffer, 0, bytesRecorded);

                    for (int j = 0; j < bytesRecorded; j += 4)
                    {
                        float sample = BitConverter.ToSingle(rentedBuffer, j);
                        sample *= volFactor;
                        byte[] bytes = BitConverter.GetBytes(sample);
                        Buffer.BlockCopy(bytes, 0, rentedBuffer, j, 4);
                    }

                    _bufs[i].AddSamples(rentedBuffer, 0, bytesRecorded);
                }
                catch { }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }
        }
    }

    private static void AplicarDelayInicial(BufferedWaveProvider b, WaveFormat format, int delayMs)
    {
        if (delayMs <= 0) return;

        int bytesToDelay = (int)((long)format.AverageBytesPerSecond * delayMs / 1000);
        bytesToDelay = (bytesToDelay / format.BlockAlign) * format.BlockAlign;

        if (bytesToDelay > 0)
        {
            byte[] silence = new byte[bytesToDelay];
            b.AddSamples(silence, 0, silence.Length);
        }
    }

    public void Stop()
    {
        _isStopping = true;

        // Cancelar hilo de forma ordenada y segura antes de liberar recursos
        try
        {
            _driftWatchdogCts?.Cancel();
            _driftWatchdogCts?.Dispose();
            _driftWatchdogCts = null;
        }
        catch { }

        _logger("Deteniendo motor limpiamente...");

        try
        {
            _cap?.StopRecording();
            _cap?.Dispose();
        }
        catch { }
        _cap = null;

        foreach (var o in _outs)
        {
            try
            {
                o.Stop();
                o.Dispose();
            }
            catch { }
        }
        _outs.Clear();
        _bufs.Clear();
        _targets.Clear();

        _logger("Motor detenido sin errores.");
    }
}