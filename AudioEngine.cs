using NAudio.Wave;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;

public class AudioEngine
{
    private WasapiLoopbackCapture? _cap;
    private List<WasapiOut> _outs = new();
    private List<BufferedWaveProvider> _bufs = new();
    private Action<string> _logger;

    public AudioEngine(Action<string> logger)
    {
        _logger = logger ?? (_ => { });
    }

    public void Start(MMDevice source, List<MMDevice> targets)
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
                    DiscardOnBufferOverflow = true
                };

                var o = new WasapiOut(t, AudioClientShareMode.Shared, true, 100);
                o.Init(b);
                o.Play();

                _bufs.Add(b);
                _outs.Add(o);

                _logger($"  Salida [{++count}]: {t.FriendlyName}");
            }

            _cap.DataAvailable += (s, e) =>
            {
                // Solo registramos la primera vez que llegan datos para confirmar flujo
                if (!_dataReceivedOnce)
                {
                    _logger("✔ Datos de audio recibidos (flujo activo).");
                    _dataReceivedOnce = true;
                }

                foreach (var b in _bufs)
                    b.AddSamples(e.Buffer, 0, e.BytesRecorded);
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

    private bool _dataReceivedOnce = false;

    public void Stop()
    {
        _logger("Deteniendo motor...");
        _cap?.StopRecording();
        foreach (var o in _outs)
        {
            o.Stop();
            o.Dispose();
        }
        _outs.Clear();
        _bufs.Clear();
        _logger("Motor detenido.");
    }
}