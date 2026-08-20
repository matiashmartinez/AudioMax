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
    private List<WasapiOut> _outs = new();
    private List<BufferedWaveProvider> _bufs = new();
    private Action<string> _logger;
    private bool _dataReceivedOnce = false;

    public AudioEngine(Action<string> logger)
    {
        _logger = logger ?? (_ => { });
    }

    public void Start(MMDevice source, List<OutputTarget> targets)
    {
        try
        {
            _logger($"Iniciando captura desde: {source.FriendlyName}");

            _cap = new WasapiLoopbackCapture(source);
            WaveFormat captureFormat = _cap.WaveFormat;
            _logger($"Formato de captura: {captureFormat}");

            int count = 0;
            foreach (var t in targets)
            {
                var b = new BufferedWaveProvider(captureFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(5),
                    DiscardOnBufferOverflow = true
                };

                // Retardo: Se inyecta silencio inicial en el buffer según los ms solicitados
                if (t.DelayMs > 0)
                {
                    int bytesToDelay = (int)((long)captureFormat.AverageBytesPerSecond * t.DelayMs / 1000);
                    int blockAlign = captureFormat.BlockAlign;
                    bytesToDelay = (bytesToDelay / blockAlign) * blockAlign; // Alineamiento de canal

                    if (bytesToDelay > 0)
                    {
                        byte[] silence = new byte[bytesToDelay];
                        b.AddSamples(silence, 0, silence.Length);
                    }
                }

                var o = new WasapiOut(t.Device, AudioClientShareMode.Shared, true, 100);
                o.Init(b);
                o.Play();

                _bufs.Add(b);
                _outs.Add(o);

                _logger($"  Salida [{++count}]: {t.Device.FriendlyName} | Retardo: {t.DelayMs} ms");
            }

            _cap.DataAvailable += (s, e) =>
            {
                if (!_dataReceivedOnce)
                {
                    _logger("✔ Datos de audio recibidos (flujo activo).");
                    _dataReceivedOnce = true;
                }

                foreach (var b in _bufs)
                {
                    b.AddSamples(e.Buffer, 0, e.BytesRecorded);
                }
            };

            _cap.RecordingStopped += (s, e) =>
            {
                _logger("Captura detenida.");
                _dataReceivedOnce = false;
            };

            _cap.StartRecording();
            _logger("Motor de audio iniciado correctamente.");
        }
        catch (Exception ex)
        {
            _logger($"ERROR al iniciar: {ex.Message}");
            throw;
        }
    }

    public void Stop()
    {
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
        _logger("Motor detenido.");
    }
}