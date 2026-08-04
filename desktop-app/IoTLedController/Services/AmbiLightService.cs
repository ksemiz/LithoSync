using System.Drawing;
using System.Drawing.Imaging;
using IoTLedController.Models;

namespace IoTLedController.Services;

// =============================================================================
//  AmbiLightService.cs  —  Ekran yakalama + renk analizi → UDP LED güncelleme
//
//  Çalışma prensibi:
//    1. Ekranın alt 1/4'ünü 30 FPS'de yakala (GDI+ CopyFromScreen)
//    2. 6 yatay bölgeye böl
//    3. Her bölgenin ortalama rengini hesapla
//    4. 18 baytlık paketi UdpSender ile ESP32'ye gönder
// =============================================================================

public sealed class AmbiLightService : IDisposable
{
    private readonly UdpSender _udp;
    private CancellationTokenSource? _cts;
    private Task? _captureTask;

    // Ayarlar
    public int     TargetFps         { get; set; } = 30;
    public double  ScreenCaptureZone { get; set; } = 0.25; // Alt %25
    public float   Saturation        { get; set; } = 1.2f; // Renk doygunluk artırımı
    public float   Brightness        { get; set; } = 1.1f; // Parlaklık artırımı
    public int     SampleStep        { get; set; } = 8;    // Piksel örnekleme adımı (performans)

    // İstatistikler
    public bool   IsRunning      { get; private set; }
    public double ActualFps      { get; private set; }
    public event  Action<LedColor[]>? ColorsUpdated;  // UI güncelleme için

    public AmbiLightService(UdpSender udp) => _udp = udp;

    // ── Başlat ────────────────────────────────────────────────────────────────
    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        _cts = new CancellationTokenSource();
        _captureTask = Task.Run(() => CaptureLoop(_cts.Token));
    }

    // ── Durdur ────────────────────────────────────────────────────────────────
    public async Task StopAsync()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _cts?.Cancel();
        if (_captureTask is not null)
            await _captureTask.ConfigureAwait(false);
    }

    // ── Ana döngü ─────────────────────────────────────────────────────────────
    private async Task CaptureLoop(CancellationToken ct)
    {
        var frameInterval = TimeSpan.FromMilliseconds(1000.0 / TargetFps);
        var fpsStopwatch  = System.Diagnostics.Stopwatch.StartNew();
        int frameCount    = 0;

        while (!ct.IsCancellationRequested)
        {
            var frameStart = DateTime.UtcNow;

            try
            {
                LedColor[] colors = CaptureAndAnalyze();
                _udp.SendColors(colors);
                ColorsUpdated?.Invoke(colors);

                frameCount++;
                if (fpsStopwatch.ElapsedMilliseconds >= 1000)
                {
                    ActualFps  = frameCount / fpsStopwatch.Elapsed.TotalSeconds;
                    frameCount = 0;
                    fpsStopwatch.Restart();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[AMBI] Hata: {ex.Message}");
            }

            // FPS sınırlaması
            var elapsed   = DateTime.UtcNow - frameStart;
            var remaining = frameInterval - elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining, ct).ConfigureAwait(false);
        }
    }

    // ── Ekran yakalama ve renk analizi ────────────────────────────────────────
    private LedColor[] CaptureAndAnalyze()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen!;
        int screenW = screen.Bounds.Width;
        int screenH = screen.Bounds.Height;

        // Yakalama bölgesi: ekranın alt kısmı
        int captureH = (int)(screenH * ScreenCaptureZone);
        int captureY = screenH - captureH;

        using var bitmap = new Bitmap(screenW, captureH, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(0, captureY, 0, 0, new Size(screenW, captureH));

        return AnalyzeBitmap(bitmap, screenW, captureH);
    }

    // ── Bitmap'i 6 bölgeye böl ve ortalama renkleri hesapla ──────────────────
    private LedColor[] AnalyzeBitmap(Bitmap bmp, int width, int height)
    {
        var colors = new LedColor[6];
        int zoneW  = width / 6;

        // BitmapData ile hızlı piksel erişimi (unsafe pointer)
        var bmpData = bmp.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;
                int stride = bmpData.Stride;

                for (int zone = 0; zone < 6; zone++)
                {
                    int x0 = zone * zoneW;
                    int x1 = (zone == 5) ? width : x0 + zoneW;

                    long totalR = 0, totalG = 0, totalB = 0;
                    int  count  = 0;

                    for (int y = 0; y < height; y += SampleStep)
                    {
                        for (int x = x0; x < x1; x += SampleStep)
                        {
                            byte* pixel = ptr + y * stride + x * 4;
                            totalB += pixel[0];  // BGRA sırası
                            totalG += pixel[1];
                            totalR += pixel[2];
                            count++;
                        }
                    }

                    if (count == 0) { colors[zone] = LedColor.Black; continue; }

                    // Ortalama + doygunluk/parlaklık artırımı
                    float r = Math.Clamp((totalR / count) * Brightness, 0, 255);
                    float gr = Math.Clamp((totalG / count) * Brightness, 0, 255);
                    float b = Math.Clamp((totalB / count) * Brightness, 0, 255);

                    // Doygunluk artırımı: ortalamadan uzaklaştır
                    float avg = (r + gr + b) / 3f;
                    r  = Math.Clamp(avg + (r  - avg) * Saturation, 0, 255);
                    gr = Math.Clamp(avg + (gr - avg) * Saturation, 0, 255);
                    b  = Math.Clamp(avg + (b  - avg) * Saturation, 0, 255);

                    colors[zone] = new LedColor((byte)r, (byte)gr, (byte)b);
                }
            }
        }
        finally
        {
            bmp.UnlockBits(bmpData);
        }

        return colors;
    }

    // ── IDisposable ───────────────────────────────────────────────────────────
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
