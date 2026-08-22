using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;
using NAudio.Wave;

public class OutputTarget
{
    public MMDevice Device { get; set; }
    public int DelayMs { get; set; }

    public OutputTarget(MMDevice device, int delayMs = 0)
    {
        Device = device;
        DelayMs = delayMs;
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

    // Ajustes del DSP
    private const float SILENCE_THRESHOLD = 0.0001f;
    private static readonly TimeSpan StandbyTimeout = TimeSpan.FromSeconds(3);

    public AudioEngine(Action<string> logger)
    {
        _logger = logger ?? (_ => { });
    }

    public void Start(MMDevice source, List<OutputTarget> targets)
    {
        try
        {
            _isStopping = false;
            _inStandby = false;
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

                _logger($"  Salida [{++count}]: {t.Device.FriendlyName} | Retardo: {t.DelayMs} ms");
            }

            _cap.DataAvailable += OnDataAvailable;
            _cap.RecordingStopped += (s, e) =>
            {
                _logger("Captura detenida.");
                _dataReceivedOnce = false;
            };

            _cap.StartRecording();
            _logger("Motor de audio iniciado con Smart-Sync.");
        }
        catch (Exception ex)
        {
            _logger($"ERROR al iniciar: {ex.Message}");
            throw;
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

            // Transición: Salir de Standby y RE-SINCRONIZAR exactamente al volver el sonido
            if (_inStandby)
            {
                EjecutarAutoResync();
                _inStandby = false;
                _logger("🔊 Audio detectado. Búferes re-sincronizados y transmisión reanudada.");
            }

            // Inyectar audio real
            InyectarMuestras(e.Buffer, e.BytesRecorded);
        }
        else
        {
            // Transición: Entrar en Standby tras 3 segundos de silencio
            if (!_inStandby && (DateTime.Now - _lastSoundTime) > StandbyTimeout)
            {
                _inStandby = true;
                _logger("🌙 Silencio prolongado (Standby). Congelando búferes para evitar desfasamiento...");
                LimpiarBufers();
            }

            // Si aún no estamos en standby (tolerancia de 3s), seguimos inyectando
            if (!_inStandby)
            {
                InyectarMuestras(e.Buffer, e.BytesRecorded);
            }
        }
    }

    private void InyectarMuestras(byte[] buffer, int bytesRecorded)
    {
        for (int i = 0; i < _bufs.Count; i++)
        {
            try
            {
                _bufs[i].AddSamples(buffer, 0, bytesRecorded);
            }
            catch { }
        }
    }

    private void LimpiarBufers()
    {
        for (int i = 0; i < _bufs.Count; i++)
        {
            _bufs[i].ClearBuffer();
        }
    }

    private void EjecutarAutoResync()
    {
        if (_cap == null) return;
        WaveFormat fmt = _cap.WaveFormat;

        for (int i = 0; i < _bufs.Count; i++)
        {
            _bufs[i].ClearBuffer();
            AplicarDelayInicial(_bufs[i], fmt, _targets[i].DelayMs);
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
        _logger("Deteniendo motor...");

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