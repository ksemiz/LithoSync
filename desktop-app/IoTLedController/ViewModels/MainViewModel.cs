using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IoTLedController.Services;
using NAudio.Wave;
using MediaColor = System.Windows.Media.Color;

namespace IoTLedController.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
        private UdpClient? _udp;
        private CancellationTokenSource? _ambiCts;
        private WasapiLoopbackCapture? _audioCapture;
        private readonly float[] _smoothedBands = new float[6];

        // ── Navigasyon ────────────────────────────────────────────────────────
        [ObservableProperty] private string _currentPage = "Connect";

        // ── Bağlantı ──────────────────────────────────────────────────────────
        [ObservableProperty] private string _deviceIp = "192.168.1.100";
        [ObservableProperty] private bool   _isConnected;
        [ObservableProperty] private string _connectionStatus = "Cihaz Aranıyor...";
        [ObservableProperty] private string _deviceInfo = "—";
        [ObservableProperty] private bool   _isSearching;

        // ── LED Kontrol ───────────────────────────────────────────────────────
        private int _currentMode;
        public int CurrentMode
        {
            get => _currentMode;
            set
            {
                if (SetProperty(ref _currentMode, value))
                {
                    OnPropertyChanged(nameof(IsMode0));
                    OnPropertyChanged(nameof(IsMode1));
                    OnPropertyChanged(nameof(IsMode2));
                    OnPropertyChanged(nameof(IsMode3));
                }
            }
        }

        public bool IsMode0 { get => CurrentMode == 0; set { if (value && CurrentMode != 0) _ = SetModeAsync("0"); } }
        public bool IsMode1 { get => CurrentMode == 1; set { if (value && CurrentMode != 1) _ = SetModeAsync("1"); } }
        public bool IsMode2 { get => CurrentMode == 2; set { if (value && CurrentMode != 2) _ = SetModeAsync("2"); } }
        public bool IsMode3 { get => CurrentMode == 3; set { if (value && CurrentMode != 3) _ = SetModeAsync("3"); } }

        [ObservableProperty] private byte       _brightness = 200;
        [ObservableProperty] private MediaColor _globalColor = MediaColor.FromRgb(255, 100, 0);
        public ObservableCollection<LedItemVM> LedColors { get; } = new();

        // ── AmbiLight ─────────────────────────────────────────────────────────
        [ObservableProperty] private bool   _ambiRunning;
        [ObservableProperty] private int    _ambiTargetFps = 30;
        [ObservableProperty] private double _ambiFps;
        public ObservableCollection<MediaColor> AmbiColors { get; } = new();

        // ── Ses Analizi ───────────────────────────────────────────────────────
        [ObservableProperty] private bool   _audioRunning;
        [ObservableProperty] private double _audioGain = 3.0;
        [ObservableProperty] private double _audioSmoothing = 0.6;
        public ObservableCollection<MediaColor> AudioColors { get; } = new();

        // ── Spotify ───────────────────────────────────────────────────────────
        [ObservableProperty] private bool   _spotifyAuth;
        [ObservableProperty] private string _spotifyTrack = "Müzik Çalınmıyor";
        [ObservableProperty] private MediaColor _spotifyColor = MediaColor.FromRgb(100, 0, 200);
        public SpotifyService Spotify { get; } = new();

        public MainViewModel()
        {
            for (int i = 0; i < 6; i++)
            {
                LedColors.Add(new LedItemVM { Index = i, Color = MediaColor.FromRgb(255, 255, 255) });
                AmbiColors.Add(MediaColor.FromRgb(20, 20, 40));
                AudioColors.Add(MediaColor.FromRgb(20, 20, 40));
            }
            Spotify.TrackChanged += t => SpotifyTrack = t;
            Spotify.DominantColorExtracted += c =>
            {
                var mc = MediaColor.FromRgb(c.R, c.G, c.B);
                SpotifyColor = mc;
                if (CurrentMode == 0) SetGlobalColor(mc);
            };
            Spotify.AuthStatusChanged += s => SpotifyAuth = s;
            Task.Run(() => DiscoverDeviceAsync());
        }

        [RelayCommand]
        private void Navigate(string page) => CurrentPage = page;

        [RelayCommand]
        private async Task DiscoverDeviceAsync()
        {
            IsSearching = true;
            ConnectionStatus = "Ağdaki LithoSync Cihazı Taranıyor...";
            try
            {
                using var udpClient = new UdpClient();
                udpClient.EnableBroadcast = true;
                udpClient.Client.ReceiveTimeout = 2500;
                byte[] pkt = Encoding.UTF8.GetBytes("LITHOSYNC_DISCOVER");
                await udpClient.SendAsync(pkt, pkt.Length, new IPEndPoint(IPAddress.Broadcast, 4210));
                var res = await udpClient.ReceiveAsync();
                using var doc = JsonDocument.Parse(res.Buffer);
                if (doc.RootElement.TryGetProperty("ip", out var ip))
                {
                    DeviceIp = ip.GetString()!;
                    await ConnectAsync();
                    return;
                }
            }
            catch
            {
                try { DeviceIp = "iot-led.local"; await ConnectAsync(); return; } catch { }
            }
            finally { IsSearching = false; }
            if (!IsConnected) ConnectionStatus = "Cihaz bulunamadı. IP adresini girin.";
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            ConnectionStatus = "Bağlanıyor...";
            try
            {
                var res = await _http.GetStringAsync($"http://{DeviceIp}/status");
                using var doc = JsonDocument.Parse(res);
                var root = doc.RootElement;
                IsConnected = root.GetProperty("ok").GetBoolean();
                CurrentMode = root.GetProperty("mode").GetInt32();
                Brightness  = (byte)root.GetProperty("brightness").GetInt32();
                var mac = root.TryGetProperty("mac", out var m) ? m.GetString() : "?";
                var ver = root.TryGetProperty("version", out var v) ? v.GetString() : "?";
                DeviceInfo = $"MAC: {mac} | Ver: {ver}";
                ConnectionStatus = $"✅ Bağlandı ({DeviceIp})";
                _udp?.Close();
                _udp = new UdpClient();
                _udp.Connect(DeviceIp, 4210);
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConnectionStatus = $"❌ Hata: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ResetWifiAsync()
        {
            if (!IsConnected) return;
            var r = System.Windows.MessageBox.Show(
                "Wi-Fi ayarları silinip cihaz AP moduna geçecek. Emin misiniz?",
                "Wi-Fi Sıfırla", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (r == System.Windows.MessageBoxResult.Yes)
            {
                await PostJsonAsync("/reset", new { });
                IsConnected = false;
                ConnectionStatus = "Wi-Fi Sıfırlandı. 'IoT-LED-Setup' ağına bağlanın.";
            }
        }

        [RelayCommand]
        private async Task SetModeAsync(string modeStr)
        {
            if (!int.TryParse(modeStr, out int m)) return;
            CurrentMode = m;
            await PostJsonAsync("/setMode", new { mode = m });
        }

        [RelayCommand]
        private async Task SetBrightnessAsync() =>
            await PostJsonAsync("/setBrightness", new { brightness = Brightness });

        [RelayCommand]
        private void PickAndSetGlobalColor()
        {
            using var dlg = new ColorDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
                SetGlobalColor(MediaColor.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B));
        }

        public async void SetGlobalColor(MediaColor c)
        {
            GlobalColor = c;
            await PostJsonAsync("/setColor", new { r = c.R, g = c.G, b = c.B });
        }

        [RelayCommand]
        private void PickAndSetLedColor(LedItemVM item)
        {
            using var dlg = new ColorDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var c = MediaColor.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B);
                item.Color = c;
                _ = PostJsonAsync("/setLedColor", new { index = item.Index, r = c.R, g = c.G, b = c.B });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // AMBILIGHT — Ekran Okuma
        // ─────────────────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task ToggleAmbiLightAsync()
        {
            if (AmbiRunning)
            {
                _ambiCts?.Cancel();
                AmbiRunning = false;
            }
            else
            {
                AmbiRunning = true;
                await SetModeAsync("3");
                _ambiCts = new CancellationTokenSource();
                _ = Task.Run(() => RunAmbiLoopAsync(_ambiCts.Token));
            }
        }

        private async Task RunAmbiLoopAsync(CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int frames = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    int delayMs = 1000 / Math.Max(1, AmbiTargetFps);
                    byte[] packet = CaptureScreenColors();

                    // UDP gönder (bağlı değilse es geç)
                    if (_udp != null)
                    {
                        try { _udp.Send(packet, packet.Length); } catch { }
                    }

                    // UI önizleme güncelle
                    var colors = new MediaColor[6];
                    for (int i = 0; i < 6; i++)
                        colors[i] = MediaColor.FromRgb(packet[i*3], packet[i*3+1], packet[i*3+2]);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        for (int i = 0; i < 6; i++) AmbiColors[i] = colors[i];
                    });

                    frames++;
                    if (sw.ElapsedMilliseconds >= 1000)
                    {
                        AmbiFps = Math.Round(frames * 1000.0 / sw.ElapsedMilliseconds, 1);
                        frames = 0; sw.Restart();
                    }
                    await Task.Delay(delayMs, ct);
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(100, ct); } // hata → kısa bekleme, devam
            }
        }

        /// <summary>
        /// Ekranın alt şeridini GDI+ ile okur, 6 zona böler, ortalama renk hesaplar.
        /// Gamma düzeltmesi ile canlı renkler.
        /// </summary>
        private byte[] CaptureScreenColors()
        {
            var buf = new byte[18];
            try
            {
                var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
                var b = screen.Bounds;

                // Alt %15'i al
                int stripH = Math.Max(4, b.Height * 15 / 100);
                int captureY = b.Y + b.Height - stripH;

                using var bmp = new Bitmap(b.Width, stripH, PixelFormat.Format24bppRgb);
                using var g = Graphics.FromImage(bmp);
                g.CopyFromScreen(b.X, captureY, 0, 0, new Size(b.Width, stripH));

                // LockBits ile hızlı piksel erişimi (safe, no unsafe needed)
                var bmpData = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, stripH),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format24bppRgb);

                int stride = bmpData.Stride;
                int zoneW = bmp.Width / 6;
                var rawBytes = new byte[stride * stripH];
                Marshal.Copy(bmpData.Scan0, rawBytes, 0, rawBytes.Length);
                bmp.UnlockBits(bmpData);

                for (int z = 0; z < 6; z++)
                {
                    long r = 0, gv = 0, bv = 0;
                    int count = 0;
                    int xStart = z * zoneW;
                    int xEnd = xStart + zoneW;

                    for (int py = 0; py < stripH; py += 8)
                    {
                        for (int px = xStart; px < xEnd; px += 10)
                        {
                            int idx = py * stride + px * 3;
                            bv += rawBytes[idx];
                            gv += rawBytes[idx + 1];
                            r  += rawBytes[idx + 2];
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        buf[z*3]   = Gamma((double)r / count);
                        buf[z*3+1] = Gamma((double)gv / count);
                        buf[z*3+2] = Gamma((double)bv / count);
                    }
                }
            }
            catch { /* Ekran okuma hatası → siyah döner */ }
            return buf;
        }

        private static byte Gamma(double v) =>
            (byte)(Math.Pow(v / 255.0, 0.55) * 255.0);

        // ─────────────────────────────────────────────────────────────────────
        // SES ANALİZİ — NAudio WASAPI + FFT
        // ─────────────────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task ToggleAudioAsync()
        {
            if (AudioRunning)
            {
                AudioRunning = false;
                StopAudioCapture();
            }
            else
            {
                AudioRunning = true;
                await SetModeAsync("3");
                StartAudioCapture();
            }
        }

        private void StopAudioCapture()
        {
            try { _audioCapture?.StopRecording(); } catch { }
            try { _audioCapture?.Dispose(); } catch { }
            _audioCapture = null;
        }

        private readonly object _fftLock = new();
        private float[] _fftAccum = new float[2048];
        private int _fftAccumPos = 0;

        private void StartAudioCapture()
        {
            try
            {
                _audioCapture = new WasapiLoopbackCapture();
                int sr = _audioCapture.WaveFormat.SampleRate;
                int ch = _audioCapture.WaveFormat.Channels;

                _audioCapture.DataAvailable += (_, e) =>
                {
                    if (!AudioRunning) return;
                    try
                    {
                        // 32-bit float örnekleri oku
                        int floatCount = e.BytesRecorded / 4;
                        lock (_fftLock)
                        {
                            for (int i = 0; i < floatCount; i += ch) // her frame bir kez (ch kanal atla)
                            {
                                // Stereo: sol+sağ ortalaması
                                float sample = 0;
                                for (int c2 = 0; c2 < ch && i + c2 < floatCount; c2++)
                                    sample += BitConverter.ToSingle(e.Buffer, (i + c2) * 4);
                                sample /= ch;

                                _fftAccum[_fftAccumPos++] = sample;

                                if (_fftAccumPos >= 2048)
                                {
                                    ProcessFft(_fftAccum, sr);
                                    _fftAccumPos = 0;
                                }
                            }
                        }
                    }
                    catch { }
                };

                _audioCapture.StartRecording();
            }
            catch (Exception ex)
            {
                AudioRunning = false;
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    System.Windows.MessageBox.Show(
                        $"Ses yakalama başlatılamadı:\n{ex.Message}\n\nWindows Ses Ayarları → Kayıtlı Cihazlar → Stereo Mix etkin olmalı.",
                        "Ses Hatası"));
            }
        }

        private void ProcessFft(float[] samples, int sampleRate)
        {
            // Hann penceresi + FFT
            var cx = new Complex[2048];
            for (int i = 0; i < 2048; i++)
            {
                double hann = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / 2047));
                cx[i] = new Complex(samples[i] * hann, 0);
            }
            FftInPlace(cx);

            double binHz = (double)sampleRate / 2048;

            // 6 frekans bandı: Sub-bass / Bass / Low-mid / Mid / High-mid / Treble
            double[] lo  = { 20,   80,  250,  800, 2500, 6000 };
            double[] hi  = { 80,  250,  800, 2500, 6000, 20000 };
            byte[]   cr  = { 220,  255,  255,   0,   0,  160 };
            byte[]   cg  = {  20,   80,  200, 220, 120,   32 };
            byte[]   cb  = {  60,    0,    0,  60, 255,  240 };

            float smoothing = (float)Math.Clamp(AudioSmoothing, 0, 0.97);
            float gain      = (float)Math.Clamp(AudioGain, 0.5, 20.0);

            var packet = new byte[18];
            var uiColors = new MediaColor[6];

            for (int z = 0; z < 6; z++)
            {
                int loIdx = Math.Max(0, (int)(lo[z] / binHz));
                int hiIdx = Math.Min(1023, (int)(hi[z] / binHz));

                double rms = 0;
                int cnt = hiIdx - loIdx + 1;
                for (int k = loIdx; k <= hiIdx; k++)
                    rms += cx[k].Magnitude * cx[k].Magnitude;
                rms = Math.Sqrt(rms / Math.Max(1, cnt));

                float level = (float)Math.Min(1.0, rms * gain);
                _smoothedBands[z] = _smoothedBands[z] * smoothing + level * (1f - smoothing);
                float v = _smoothedBands[z];

                byte r = (byte)(cr[z] * v);
                byte g = (byte)(cg[z] * v);
                byte b = (byte)(cb[z] * v);
                packet[z*3] = r; packet[z*3+1] = g; packet[z*3+2] = b;
                uiColors[z] = MediaColor.FromRgb(r, g, b);
            }

            // UDP gönder
            if (_udp != null)
                try { _udp.Send(packet, packet.Length); } catch { }

            // UI güncelle
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                for (int z = 0; z < 6; z++) AudioColors[z] = uiColors[z];
            });
        }

        private static void FftInPlace(Complex[] a)
        {
            int n = a.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { var t = a[i]; a[i] = a[j]; a[j] = t; }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2.0 * Math.PI / len;
                var wlen = new Complex(Math.Cos(ang), Math.Sin(ang));
                for (int i = 0; i < n; i += len)
                {
                    var w = Complex.One;
                    for (int j2 = 0; j2 < len / 2; j2++)
                    {
                        var u = a[i + j2];
                        var vv = a[i + j2 + len/2] * w;
                        a[i + j2]         = u + vv;
                        a[i + j2 + len/2] = u - vv;
                        w *= wlen;
                    }
                }
            }
        }

        [RelayCommand]
        private async Task StartSpotifyAuthAsync() => await Spotify.StartAuthAsync();

        private async Task PostJsonAsync(string path, object data)
        {
            if (!IsConnected) return;
            try
            {
                var json = JsonSerializer.Serialize(data);
                await _http.PostAsync($"http://{DeviceIp}{path}",
                    new StringContent(json, Encoding.UTF8, "application/json"));
            }
            catch { }
        }
    }

    public partial class LedItemVM : ObservableObject
    {
        [ObservableProperty] private int _index;
        [ObservableProperty] private MediaColor _color;
    }
}
