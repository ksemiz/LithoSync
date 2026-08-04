using NAudio.Wave;
using NAudio.Dsp;
using IoTLedController.Models;

namespace IoTLedController.Services;

// =============================================================================
//  AudioAnalysisService.cs  —  NAudio WASAPI Loopback + FFT → LED renk haritası
//
//  Çalışma prensibi:
//    1. WASAPI Loopback ile sistem ses çıkışını yakala (kayıpsız)
//    2. FFT ile frekans spektrumu analiz et
//    3. Frekans bantlarını 6 LED'e renk haritasıyla eşle:
//       LED 0-1: Bass    (20-250 Hz)   → Kırmızı/Turuncu
//       LED 2-3: Mid     (250-4K Hz)   → Yeşil/Sarı
//       LED 4-5: Treble  (4K-20K Hz)  → Mavi/Mor
//    4. 30 FPS'de UDP üzerinden ESP32'ye gönder
// =============================================================================

public sealed class AudioAnalysisService : IDisposable
{
    private readonly UdpSender _udp;
    private WasapiLoopbackCapture? _capture;
    private CancellationTokenSource? _cts;
    private Task? _sendTask;

    // FFT buffer
    private const int FftSize   = 4096;
    private readonly float[] _audioBuffer = new float[FftSize];
    private int    _bufferPos   = 0;
    private readonly object _bufferLock = new();

    // Analiz sonuçları (thread-safe)
    private float[] _bandEnergies = new float[6];

    // Ayarlar
    public int   TargetFps     { get; set; } = 30;
    public float Gain          { get; set; } = 3.0f;   // Amplifikasyon
    public float Smoothing     { get; set; } = 0.7f;   // Hareket yumuşatma (0-1)

    public bool IsRunning      { get; private set; }
    public event Action<LedColor[]>? ColorsUpdated;

    // Bant sınırları (Hz) — her bant 2 LED'e karşılık gelir
    private static readonly (float Low, float High, float[] HueRange)[] Bands = {
        (20,   120,  new[]{ 0f,   20f }),   // Sub-bass → koyu kırmızı
        (120,  300,  new[]{ 20f,  40f }),   // Bass     → kırmızı-turuncu
        (300,  1000, new[]{ 60f,  80f }),   // Low-mid  → yeşil
        (1000, 4000, new[]{ 80f, 100f }),   // Mid      → sarı-yeşil
        (4000, 8000, new[]{ 200f,230f }),   // High-mid → mavi
        (8000, 20000,new[]{ 270f,300f }),   // Treble   → mor
    };

    // Önceki değerler (smoothing için)
    private readonly float[] _prevEnergies = new float[6];

    public AudioAnalysisService(UdpSender udp) => _udp = udp;

    // ── Başlat ────────────────────────────────────────────────────────────────
    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        _cts = new CancellationTokenSource();

        // WASAPI Loopback yakalama başlat
        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += OnDataAvailable;
        _capture.StartRecording();

        // UDP gönderim döngüsü
        _sendTask = Task.Run(() => SendLoop(_cts.Token));
    }

    // ── Durdur ────────────────────────────────────────────────────────────────
    public async Task StopAsync()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _cts?.Cancel();
        _capture?.StopRecording();
        _capture?.Dispose();
        _capture = null;
        if (_sendTask is not null)
            await _sendTask.ConfigureAwait(false);
    }

    // ── Ses verisi alındığında ────────────────────────────────────────────────
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // WaveFormat → float dönüşümü
        int bytesPerSample = _capture!.WaveFormat.BitsPerSample / 8;
        int channels       = _capture.WaveFormat.Channels;
        int sampleRate     = _capture.WaveFormat.SampleRate;

        lock (_bufferLock)
        {
            for (int i = 0; i < e.BytesRecorded; i += bytesPerSample * channels)
            {
                // Stereo → mono mix
                float sample = 0;
                for (int ch = 0; ch < channels; ch++)
                {
                    int offset = i + ch * bytesPerSample;
                    if (offset + bytesPerSample > e.BytesRecorded) break;

                    sample += bytesPerSample switch
                    {
                        4 => BitConverter.ToSingle(e.Buffer, offset),           // float32
                        2 => BitConverter.ToInt16(e.Buffer, offset) / 32768f,   // int16
                        _ => 0f
                    };
                }
                sample /= channels;

                _audioBuffer[_bufferPos % FftSize] = sample;
                _bufferPos++;
            }
        }

        // FFT hesapla
        CalculateFft(sampleRate);
    }

    // ── FFT hesaplama ve bant enerjisi ────────────────────────────────────────
    private void CalculateFft(int sampleRate)
    {
        float[] samples;
        lock (_bufferLock)
        {
            samples = new float[FftSize];
            Array.Copy(_audioBuffer, samples, FftSize);
        }

        // Hanning penceresi uygula
        var fftBuffer = new Complex[FftSize];
        for (int i = 0; i < FftSize; i++)
        {
            double window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (FftSize - 1)));
            fftBuffer[i].X = (float)(samples[i] * window);
            fftBuffer[i].Y = 0;
        }

        FastFourierTransform.FFT(true, (int)Math.Log2(FftSize), fftBuffer);

        // Bant enerjisi hesapla
        float freqResolution = sampleRate / (float)FftSize;
        for (int b = 0; b < 6; b++)
        {
            int startBin = (int)(Bands[b].Low  / freqResolution);
            int endBin   = (int)(Bands[b].High / freqResolution);
            endBin = Math.Min(endBin, FftSize / 2);

            float energy = 0;
            for (int bin = startBin; bin < endBin; bin++)
            {
                float magnitude = (float)Math.Sqrt(
                    fftBuffer[bin].X * fftBuffer[bin].X +
                    fftBuffer[bin].Y * fftBuffer[bin].Y);
                energy += magnitude;
            }
            energy /= (endBin - startBin + 1);
            energy *= Gain;
            energy = Math.Clamp(energy, 0, 1);

            // Smoothing (üstel hareketli ortalama)
            _prevEnergies[b] = _prevEnergies[b] * Smoothing + energy * (1 - Smoothing);
            _bandEnergies[b] = _prevEnergies[b];
        }
    }

    // ── UDP gönderim döngüsü ──────────────────────────────────────────────────
    private async Task SendLoop(CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(1000.0 / TargetFps);

        while (!ct.IsCancellationRequested)
        {
            var start = DateTime.UtcNow;

            LedColor[] colors = EnergiestoColors(_bandEnergies);
            _udp.SendColors(colors);
            ColorsUpdated?.Invoke(colors);

            var elapsed   = DateTime.UtcNow - start;
            var remaining = interval - elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining, ct).ConfigureAwait(false);
        }
    }

    // ── Bant enerjisi → LED renk dönüşümü ────────────────────────────────────
    private static LedColor[] EnergiestoColors(float[] energies)
    {
        var colors = new LedColor[6];
        for (int i = 0; i < 6; i++)
        {
            float e = energies[i];
            // Her bant için HSV'den RGB'ye dönüştür
            (float lowHue, float highHue) = (Bands[i].HueRange[0], Bands[i].HueRange[1]);
            float hue = lowHue + (highHue - lowHue) * e;
            float sat = 0.8f + e * 0.2f;
            float val = e;

            var (r, g, b) = HsvToRgb(hue, sat, val);
            colors[i] = new LedColor(r, g, b);
        }
        return colors;
    }

    // ── HSV → RGB dönüşümü ────────────────────────────────────────────────────
    private static (byte R, byte G, byte B) HsvToRgb(float hue, float sat, float val)
    {
        if (val <= 0) return (0, 0, 0);
        if (sat <= 0) { byte v = (byte)(val * 255); return (v, v, v); }

        hue %= 360;
        float h = hue / 60f;
        int   i = (int)h;
        float f = h - i;
        float p = val * (1 - sat);
        float q = val * (1 - sat * f);
        float t = val * (1 - sat * (1 - f));

        (float r, float g, float b) = i switch
        {
            0 => (val, t, p),
            1 => (q,  val, p),
            2 => (p,  val, t),
            3 => (p,  q,  val),
            4 => (t,  p,  val),
            _ => (val, p,  q)
        };

        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    // ── IDisposable ───────────────────────────────────────────────────────────
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _capture?.Dispose();
    }
}
