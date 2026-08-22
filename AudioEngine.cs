using System;
using System.Buffers;
using System.Collections.Generic;
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

    private bool _dataReceivedOnce = false;
    private bool _isStopping = false;
    private bool _inStandby = false;
    private DateTime _lastSoundTime = DateTime.Now;

    public bool EnableAutoSync { get; set; } = true;

    private const float SILENCE_THRESHOLD = 0.0001f;
    private static readonly TimeSpan StandbyTimeout = TimeSpan.FromSeconds(3);

    public AudioEngine(Action<string> logger)
    {
        _logger = logger ?? (_ => { });
    }

    public void Start(MMDevice source, List<OutputTarget> targets, bool enableAutoSync)
    {
        try
        {
            _isStopping = false;
            _inStandby = false;
            EnableAutoSync = enableAutoSync;
            _targets.Clear();
            _targets.AddRange(targets);

            _logger($"Iniciando captura desde: {source.FriendlyName}");

            _cap = new WasapiLoopbackCapture(source);
            WaveFormat captureFormat = _cap.WaveFormat;

            int count = 0;
            foreach (var t in targets)
            {
                var b = new BufferedWaveProvider(captureFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(5),
                    DiscardOnBufferOverflow = true
                };

                AplicarDelayInicial(b, captureFormat, t.DelayMs);

                var o = new WasapiOut(t.Device, AudioClientShareMode.Shared, true, 100);
                o.Init(b);
                o.Play();

                _bufs.Add(b);
                _outs.Add(o);

                _logger($"  Salida [{++count}]: {t.Device.FriendlyName} | Retardo: {t.DelayMs}ms | Vol: {t.Volume}%");
            }

            _cap.DataAvailable += OnDataAvailable;
            _cap.RecordingStopped += (s, e) =>
            {
                _logger("Captura detenida.");
                _dataReceivedOnce = false;
            };

            _cap.StartRecording();
            _logger("AudioTwin v1.2.3 Motor activo.");
        }
        catch (Exception ex)
        {
            _logger($"ERROR al iniciar: {ex.Message}");
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
            _logger("✔ Flujo de audio activo.");
            _dataReceivedOnce = true;
        }

        float rms = CalcularRMS_32BitFloat(e.Buffer, e.BytesRecorded);

        if (rms > SILENCE_THRESHOLD)
        {
            _lastSoundTime = DateTime.Now;

            if (_inStandby)
            {
                _inStandby = false;
                _logger("🔊 Audio detectado saliendo de Standby.");
                
                // Al despertar, re-aplicamos estrictamente las latencias actuales de los targets
                if (EnableAutoSync && _cap != null)
                {
                    EjecutarReinicioLatenciaAlDespertar(_cap.WaveFormat);
                }
            }

            InyectarMuestrasConVolumenOptimizado(e.Buffer, e.BytesRecorded);
        }
        else
        {
            if (!_inStandby && (DateTime.Now - _lastSoundTime) > StandbyTimeout)
            {
                _inStandby = true;
                if (EnableAutoSync)
                {
                    _logger("🌙 Silencio prolongado (Standby). Limpiando búferes...");
                    LimpiarBufers();
                }
            }

            if (!_inStandby)
            {
                InyectarMuestrasConVolumenOptimizado(e.Buffer, e.BytesRecorded);
            }
        }
    }

    private void EjecutarReinicioLatenciaAlDespertar(WaveFormat fmt)
    {
        _logger("🔄 Re-sincronizando latencias de dispositivos tras reposo...");
        for (int i = 0; i < _bufs.Count; i++)
        {
            try
            {
                _bufs[i].ClearBuffer();
                // Lee el valor actual de latencia configurado en memoria para este target
                int delayActual = _targets[i].DelayMs;
                AplicarDelayInicial(_bufs[i], fmt, delayActual);
            }
            catch { }
        }
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

    private void LimpiarBufers()
    {
        for (int i = 0; i < _bufs.Count; i++)
        {
            _bufs[i].ClearBuffer();
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

    private static float CalcularRMS_32BitFloat(byte[] buffer, int bytesRecorded)
    {
        float sum = 0;
        int sampleCount = bytesRecorded / 4;

        for (int i = 0; i < bytesRecorded; i += 4)
        {
            float sample = BitConverter.ToSingle(buffer, i);
            sum += sample * sample;
        }

        return (float)Math.Sqrt(sum / Math.Max(1, sampleCount));
    }

    public void Stop()
    {
        _isStopping = true;
        _logger("Deteniendo motor suavemente...");

        System.Threading.Thread.Sleep(30);

        _cap?.StopRecording();
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

        _logger("Motor detenido limpiamente.");
    }
}