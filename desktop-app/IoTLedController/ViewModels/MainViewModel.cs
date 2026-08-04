using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
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

        // Ses smoothing için önceki değerler
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

        // RadioButton IsChecked bağlantıları
        public bool IsMode0 { get => CurrentMode == 0; set { if (value) SetModeCommand.Execute("0"); } }
        public bool IsMode1 { get => CurrentMode == 1; set { if (value) SetModeCommand.Execute("1"); } }
        public bool IsMode2 { get => CurrentMode == 2; set { if (value) SetModeCommand.Execute("2"); } }
        public bool IsMode3 { get => CurrentMode == 3; set { if (value) SetModeCommand.Execute("3"); } }

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
                AmbiColors.Add(MediaColor.FromRgb(0, 0, 0));
                AudioColors.Add(MediaColor.FromRgb(0, 0, 0));
            }

            Spotify.TrackChanged += t => SpotifyTrack = t;
            Spotify.DominantColorExtracted += c =>
            {
                var mediaCol = MediaColor.FromRgb(c.R, c.G, c.B);
                SpotifyColor = mediaCol;
                if (CurrentMode == 0) SetGlobalColor(mediaCol);
            };
            Spotify.AuthStatusChanged += s => SpotifyAuth = s;

            // Uygulama açılışında ağı otomatik tara
            Task.Run(() => DiscoverDeviceAsync());
        }

        [RelayCommand]
        private void Navigate(string page) => CurrentPage = page;

        [RelayCommand]
        private async Task DiscoverDeviceAsync()
        {
            IsSearching = true;
            ConnectionStatus = "Ağdaki LithoSync Cihazı Otomatik Taranıyor...";
            try
            {
                using var udpClient = new UdpClient();
                udpClient.EnableBroadcast = true;
                udpClient.Client.ReceiveTimeout = 2500;
                byte[] discoverPacket = Encoding.UTF8.GetBytes("LITHOSYNC_DISCOVER");
                await udpClient.SendAsync(discoverPacket, discoverPacket.Length, new IPEndPoint(IPAddress.Broadcast, 4210));
                var result = await udpClient.ReceiveAsync();
                string responseStr = Encoding.UTF8.GetString(result.Buffer);
                using var doc = JsonDocument.Parse(responseStr);
                if (doc.RootElement.TryGetProperty("ip", out var ipProp))
                {
                    DeviceIp = ipProp.GetString()!;
                    ConnectionStatus = $"Cihaz Bulundu! ({DeviceIp}) Bağlanılıyor...";
                    await ConnectAsync();
                    return;
                }
            }
            catch
            {
                try { DeviceIp = "iot-led.local"; await ConnectAsync(); return; }
                catch { }
            }
            finally { IsSearching = false; }
            if (!IsConnected) ConnectionStatus = "Otomatik cihaz bulunamadı. IP adresini manuel girin.";
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
                ConnectionStatus = $"Bağlandı ({DeviceIp})";
                _udp?.Close();
                _udp = new UdpClient();
                _udp.Connect(DeviceIp, 4210);
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConnectionStatus = $"Hata: {ex.Message}";
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
                ConnectionStatus = "Wi-Fi Sıfırlandı. Cihaz 'IoT-LED-Setup' ağında başlatılıyor.";
            }
        }

        // ── LED Mod Seçimi (RadioButton'lardan çağrılır) ──────────────────────
        [RelayCommand]
        private async Task SetModeAsync(string modeStr)
        {
            if (!int.TryParse(modeStr, out int m)) return;
            CurrentMode = m;
            await PostJsonAsync("/setMode", new { mode = m });
        }

        [RelayCommand]
        private async Task SetBrightnessAsync()
        {
            await PostJsonAsync("/setBrightness", new { brightness = Brightness });
        }

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

        // ── AMBILIGHT — Ekranın 4 Kenarından Örnekleme ───────────────────────
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
                int delayMs = 1000 / Math.Max(1, AmbiTargetFps);
                byte[] packet = CaptureEdgeColors();
                if (_udp != null && packet.Length == 18)
                    try { await _udp.SendAsync(packet, packet.Length); } catch { }

                for (int i = 0; i < 6; i++)
                {
                    var c = MediaColor.FromRgb(packet[i * 3], packet[i * 3 + 1], packet[i * 3 + 2]);
                    int ci = i;
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => AmbiColors[ci] = c);
                }
                frames++;
                if (sw.ElapsedMilliseconds >= 1000)
                {
                    AmbiFps = frames * 1000.0 / sw.ElapsedMilliseconds;
                    frames = 0; sw.Restart();
                }
                await Task.Delay(delayMs, ct);
            }
        }

        /// <summary>
        /// Ekranın alt kenarını 6 bölgeye bölerek ortalama renk çeker.
        /// Ek olarak gamma 0.6 düzeltmesi uygulanır → canlı, parlak renkler.
        /// </summary>
        private byte[] CaptureEdgeColors()
        {
            byte[] buf = new byte[18];
            try
            {
                var b = Screen.PrimaryScreen!.Bounds;
                int stripH = Math.Max(8, b.Height / 8); // Alt %12.5
                int y = b.Height - stripH;

                using var bmp = new Bitmap(b.Width, stripH, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(b.X, y, 0, 0, new Size(b.Width, stripH));

                // Hızlı unsafe piksel erişimi
                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, stripH),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int stride = data.Stride;

                unsafe
                {
                    byte* ptr = (byte*)data.Scan0;
                    int zoneW = b.Width / 6;
                    for (int z = 0; z < 6; z++)
                    {
                        long r = 0, g2 = 0, blue = 0;
                        int count = 0;
                        int xStart = z * zoneW;
                        int xEnd = xStart + zoneW;
                        for (int px = xStart; px < xEnd; px += 12)
                        {
                            for (int py = 0; py < stripH; py += 8)
                            {
                                byte* p = ptr + py * stride + px * 4;
                                blue += p[0]; g2 += p[1]; r += p[2];
                                count++;
                            }
                        }
                        if (count > 0)
                        {
                            // Gamma 0.55 düzeltmesi: canlılaştır
                            buf[z * 3]     = GammaCorrect((double)r / count);
                            buf[z * 3 + 1] = GammaCorrect((double)g2 / count);
                            buf[z * 3 + 2] = GammaCorrect((double)blue / count);
                        }
                    }
                }
                bmp.UnlockBits(data);
            }
            catch { }
            return buf;
        }

        private static byte GammaCorrect(double v)
        {
            double norm = v / 255.0;
            return (byte)(Math.Pow(norm, 0.55) * 255.0);
        }

        // ── GERÇEK SES ANALİZİ — NAudio WASAPI + FFT Frekans Bantları ────────
        [RelayCommand]
        private async Task ToggleAudioAsync()
        {
            if (AudioRunning)
            {
                AudioRunning = false;
                try { _audioCapture?.StopRecording(); _audioCapture?.Dispose(); _audioCapture = null; }
                catch { }
            }
            else
            {
                AudioRunning = true;
                await SetModeAsync("3");
                StartWasapiCapture();
            }
        }

        private void StartWasapiCapture()
        {
            try
            {
                _audioCapture = new WasapiLoopbackCapture();
                int sampleRate = _audioCapture.WaveFormat.SampleRate;
                int channels   = _audioCapture.WaveFormat.Channels;

                // FFT buffer — 2048 örneklik pencere
                const int FFT_SIZE = 2048;
                float[] fftBuffer = new float[FFT_SIZE];
                int fftPos = 0;

                _audioCapture.DataAvailable += (s, e) =>
                {
                    if (!AudioRunning) return;

                    // 32-bit float örnekleri al
                    int sampleCount = e.BytesRecorded / 4;
                    for (int i = 0; i < sampleCount; i++)
                    {
                        float sample = BitConverter.ToSingle(e.Buffer, i * 4);
                        // Stereo ise ortalama al
                        if (channels == 2 && i % 2 == 1)
                        {
                            fftBuffer[fftPos] = (fftBuffer[fftPos] + sample) * 0.5f;
                            fftPos++;
                        }
                        else
                        {
                            fftBuffer[fftPos % FFT_SIZE] = sample;
                            if (channels == 1) fftPos++;
                        }

                        if (fftPos >= FFT_SIZE)
                        {
                            ProcessFft(fftBuffer, sampleRate);
                            fftPos = 0;
                        }
                    }
                };

                _audioCapture.StartRecording();
            }
            catch (Exception ex)
            {
                AudioRunning = false;
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    System.Windows.MessageBox.Show($"Ses yakalama başlatılamadı:\n{ex.Message}", "Hata"));
            }
        }

        private void ProcessFft(float[] samples, int sampleRate)
        {
            // Hann penceresi uygula
            var complex = new Complex[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                double hann = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (samples.Length - 1)));
                complex[i] = new Complex(samples[i] * hann, 0);
            }

            // FFT
            FftInPlace(complex);

            int binCount = complex.Length / 2;
            double binHz = (double)sampleRate / complex.Length;

            // 6 frekans bandı (Hz aralıkları) — Sub-bass → Treble
            (double lo, double hi, (byte r, byte g, byte b) baseColor)[] bands =
            {
                (20,   80,   (220,  20, 60)),   // Sub-bass → Kırmızı
                (80,   250,  (255, 80,  0)),    // Bass → Turuncu
                (250,  800,  (255,220,  0)),    // Low-mid → Sarı
                (800,  2500, (0,  200, 80)),    // Mid → Yeşil
                (2500, 6000, (0,  120,255)),    // High-mid → Mavi
                (6000, 20000,(160, 32,240)),    // Treble → Mor
            };

            float smoothing = (float)AudioSmoothing;
            float gain = (float)AudioGain;

            byte[] packet = new byte[18];
            for (int z = 0; z < 6; z++)
            {
                int loIdx = Math.Max(0, (int)(bands[z].lo / binHz));
                int hiIdx = Math.Min(binCount - 1, (int)(bands[z].hi / binHz));

                float rms = 0;
                int cnt = hiIdx - loIdx + 1;
                for (int k = loIdx; k <= hiIdx; k++)
                    rms += (float)(complex[k].Magnitude * complex[k].Magnitude);
                rms = (float)Math.Sqrt(rms / Math.Max(1, cnt));

                // Gain + clamp
                float level = Math.Min(1.0f, rms * gain);

                // Exponential smoothing
                _smoothedBands[z] = _smoothedBands[z] * smoothing + level * (1 - smoothing);
                float v = _smoothedBands[z];

                var bc = bands[z].baseColor;
                byte r = (byte)(bc.r * v);
                byte g = (byte)(bc.g * v);
                byte b = (byte)(bc.b * v);

                packet[z * 3] = r; packet[z * 3 + 1] = g; packet[z * 3 + 2] = b;

                var color = MediaColor.FromRgb(r, g, b);
                int zi = z;
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => AudioColors[zi] = color);
            }

            if (_udp != null)
                try { _udp.Send(packet, packet.Length); } catch { }
        }

        /// <summary>Cooley-Tukey FFT — in-place iterative</summary>
        private static void FftInPlace(Complex[] a)
        {
            int n = a.Length;
            // Bit-reverse permutation
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { var t = a[i]; a[i] = a[j]; a[j] = t; }
            }
            // FFT
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                var wlen = new Complex(Math.Cos(ang), Math.Sin(ang));
                for (int i = 0; i < n; i += len)
                {
                    var w = Complex.One;
                    for (int j = 0; j < len / 2; j++)
                    {
                        var u = a[i + j];
                        var v = a[i + j + len / 2] * w;
                        a[i + j]           = u + v;
                        a[i + j + len / 2] = u - v;
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
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PostAsync($"http://{DeviceIp}{path}", content);
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
