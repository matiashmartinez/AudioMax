// Lógica de espejo de alto rendimiento en C# 14
using NAudio.CoreAudioApi;
using NAudio.Wave;

public class AudioController
{
    private WasapiLoopbackCapture _capture;

    public AudioController(WasapiLoopbackCapture capture)
    {
        _capture = capture;
    }

    private List<WasapiOut> _outputs = new();
    private List<BufferedWaveProvider> _buffers = new();

    public void Start(MMDevice source, IEnumerable<MMDevice> targets)
    {
        _capture = new WasapiLoopbackCapture(source);
        foreach (var t in targets)
        {
            var buf = new BufferedWaveProvider(_capture.WaveFormat);
            var outDev = new WasapiOut(t, AudioClientShareMode.Shared, true, 50);
            outDev.Init(buf); outDev.Play(); _buffers.Add(buf); _outputs.Add(outDev);
        }
        _capture.DataAvailable += (s, e) => {
            foreach (var b in _buffers) b.AddSamples(e.Buffer, 0, e.BytesRecorded);
        };
        _capture.StartRecording();
    }
}