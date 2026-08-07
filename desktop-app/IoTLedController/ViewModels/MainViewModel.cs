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
        private readonly UpdaterService _updater = new();

        // ── Navigasyon ────────────────────────────────────────────────────────
        [ObservableProperty] private string _currentPage = "Connect";

        // ── Bağlantı ──────────────────────────────────────────────────────────
        [ObservableProperty] private string _deviceIp = "iot-led.local";  // dahili — UI'da gösterilmez
        [ObservableProperty] private bool   _isConnected;
        [ObservableProperty] private string _connectionStatus = "LithoSync cihazı aranıyor...";
        [ObservableProperty] private string _deviceInfo = "—";
        [ObservableProperty] private bool   _isSearching;
        private CancellationTokenSource? _discoveryCts;

        // ── Güncelleme ────────────────────────────────────────────────────────
        public string AppCurrentVersion => $"v{UpdaterService.CurrentVersion}";
        [ObservableProperty] private string _updateStatusText = "Uygulama güncel olup olmadığı kontrol edilmedi.";
        [ObservableProperty] private bool   _isDownloadingUpdate;
        [ObservableProperty] private int    _updateDownloadPercent;
        [ObservableProperty] private bool   _hasPendingUpdate;
        // IsNotDownloadingUpdate — buton IsEnabled için
        public bool IsNotDownloadingUpdate => !IsDownloadingUpdate;
        private IoTLedController.Services.GitHubRelease? _pendingRelease;

        partial void OnIsDownloadingUpdateChanged(bool value) =>
            OnPropertyChanged(nameof(IsNotDownloadingUpdate));

        // ── LED Modu ──────────────────────────────────────────────────────────
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
                    OnPropertyChanged(nameof(AnimColorVisible));
                }
            }
        }

        public bool IsMode0 { get => CurrentMode == 0; set { if (value && CurrentMode != 0) _ = SetModeAsync("0"); } }
        public bool IsMode1 { get => CurrentMode == 1; set { if (value && CurrentMode != 1) _ = SetModeAsync("1"); } }
        public bool IsMode2 { get => CurrentMode == 2; set { if (value && CurrentMode != 2) _ = SetModeAsync("2"); } }
        public bool IsMode3 { get => CurrentMode == 3; set { if (value && CurrentMode != 3) _ = SetModeAsync("3"); } }

        // Animasyon rengi (Knight Rider / Thunder için)
        private MediaColor _animColor = MediaColor.FromRgb(255, 0, 0);
        public MediaColor AnimColor
        {
            get => _animColor;
            set
            {
                if (SetProperty(ref _animColor, value))
                    _ = PostJsonAsync("/setAnimColor", new { r = value.R, g = value.G, b = value.B });
            }
        }

        public System.Windows.Visibility AnimColorVisible =>
            (CurrentMode == 1 || CurrentMode == 2)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

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
        [ObservableProperty] private double _audioGain      = 3.0;
        [ObservableProperty] private double _audioSmoothing = 0.6;
        public ObservableCollection<MediaColor> AudioColors { get; } = new();

        // Ses işleme state
        private float _smoothBass;
        private float _smoothEnergy;
        private float _hue;
        private float _beatBrightness;
        private long  _lastFftMs;
        private readonly object _fftLock = new();
        private float[] _fftAccum = new float[2048];
        private int     _fftAccumPos = 0;

        // ── Spotify ───────────────────────────────────────────────────────────
        [ObservableProperty] private bool        _spotifyAuth;
        [ObservableProperty] private string      _spotifyTrack = "Müzik Çalınmıyor";
        [ObservableProperty] private MediaColor  _spotifyColor = MediaColor.FromRgb(100, 0, 200);
        public SpotifyService Spotify { get; } = new();

        // ────────────────────────────────────────────────────────────────────────────
        public MainViewModel()
        {
            for (int i = 0; i < 6; i++)
            {
                LedColors.Add(new LedItemVM { Index = i, Color = MediaColor.FromRgb(255, 255, 255) });
                AmbiColors.Add(MediaColor.FromRgb(20, 20, 40));
                AudioColors.Add(MediaColor.FromRgb(20, 20, 40));
            }
            Spotify.TrackChanged           += t => SpotifyTrack = t;
            Spotify.DominantColorExtracted += c =>
            {
                var mc = MediaColor.FromRgb(c.R, c.G, c.B);
                SpotifyColor = mc;
                if (CurrentMode == 0) SetGlobalColor(mc);
            };
            Spotify.AuthStatusChanged += s => SpotifyAuth = s;
            // Uygulama başlatılınca otomatik cihaz araması başlat
            Task.Run(() => DiscoverDeviceAsync());
            // Uygulama başlatıldıktan 8 sn sonra arka planda güncelleme kontrol et
            Task.Run(async () => {
                await Task.Delay(8000);
                await CheckUpdateAsync(silent: true);
            });
        }

        [RelayCommand]
        private void Navigate(string page) => CurrentPage = page;

        // ═ Uygulama Güncellemesi ═══════════════════════════════════════════════════════════════
        [RelayCommand]
        private async Task CheckUpdateAsync(bool silent = false)
        {
            if (!silent)
                UpdateStatusText = "🔍 GitHub'da yeni sürüm kontrol ediliyor...";
            try
            {
                var release = await _updater.CheckForUpdateAsync();
                if (release is null)
                {
                    HasPendingUpdate = false;
                    _pendingRelease  = null;
                    UpdateStatusText = $"✅ Uygulamanız güncel. (Mevcut: {UpdaterService.CurrentVersion})";
                }
                else
                {
                    HasPendingUpdate = true;
                    _pendingRelease  = release;
                    var ver = release.TagName.TrimStart('v');
                    UpdateStatusText = $"📦 Yeni sürüm mevcut: v{ver}\n↓ Güncelle ve Yeniden Başlat butonuna basarak yükseltebilirsiniz.";
                }
            }
            catch
            {
                if (!silent)
                    UpdateStatusText = "⚠️ Güncelleme kontrolü başarsız. İnternet bağlantınızı kontrol edin.";
            }
        }

        [RelayCommand]
        private async Task ApplyUpdateAsync()
        {
            if (_pendingRelease is null) return;

            IsDownloadingUpdate   = true;
            UpdateDownloadPercent = 0;

            var progress = new Progress<(int Percent, string Status)>(x =>
            {
                UpdateDownloadPercent = x.Percent;
                UpdateStatusText      = x.Status;
            });

            try
            {
                await _updater.DownloadAndUpdateAsync(_pendingRelease, progress);
                // DownloadAndUpdateAsync Application.Current.Shutdown() çağırır — bu noktaya ulaşılmaz
            }
            catch (Exception ex)
            {
                IsDownloadingUpdate = false;
                UpdateStatusText    = $"❌ Güncelleme hatası: {ex.Message}";
            }
        }

        // ═ Cihaz Keşif — UDP Broadcast + mDNS fallback + 10sn otomatik retry ════════
        [RelayCommand]
        private async Task DiscoverDeviceAsync()
        {
            // Önceki arama döngüsünü iptal et
            _discoveryCts?.Cancel();
            _discoveryCts = new CancellationTokenSource();
            var ct = _discoveryCts.Token;

            IsSearching = true;

            while (!ct.IsCancellationRequested)
            {
                ConnectionStatus = "🔍 LithoSync cihazı ağında aranıyor...";
                string? foundIp = null;

                // ─ Deneme 1: UDP Broadcast ─────────────────────────────────────────
                try
                {
                    using var udpClient = new UdpClient();
                    udpClient.EnableBroadcast = true;
                    udpClient.Client.SetSocketOption(
                        SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                    byte[] pkt = Encoding.UTF8.GetBytes("LITHOSYNC_DISCOVER");
                    await udpClient.SendAsync(pkt, pkt.Length,
                        new IPEndPoint(IPAddress.Broadcast, 4210));

                    // Task.WhenAny ile proper async timeout
                    var receiveTask = udpClient.ReceiveAsync();
                    var timeoutTask = Task.Delay(2500, ct);
                    var winner = await Task.WhenAny(receiveTask, timeoutTask);

                    if (winner == receiveTask && !receiveTask.IsFaulted)
                    {
                        var res = await receiveTask;
                        using var doc = JsonDocument.Parse(res.Buffer);
                        if (doc.RootElement.TryGetProperty("ip", out var ipEl))
                            foundIp = ipEl.GetString();
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { /* UDP başarısız, sonraki denemeye geç */ }

                // ─ Deneme 2: mDNS (iot-led.local) ─────────────────────────────────
                if (foundIp == null && !ct.IsCancellationRequested)
                {
                    try
                    {
                        ConnectionStatus = "🌐 mDNS ile aranıyor (iot-led.local)...";
                        using var testHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(2.5) };
                        var resp = await testHttp.GetStringAsync("http://iot-led.local/status");
                        using var doc = JsonDocument.Parse(resp);
                        if (doc.RootElement.TryGetProperty("ok", out var okEl) && okEl.GetBoolean())
                            foundIp = "iot-led.local";
                    }
                    catch (OperationCanceledException) { break; }
                    catch { /* mDNS başarısız */ }
                }

                // ─ Bulundu — bağlan ────────────────────────────────────────────────
                if (foundIp != null && !ct.IsCancellationRequested)
                {
                    DeviceIp = foundIp;
                    IsSearching = false;
                    await ConnectAsync();
                    return;
                }

                // ─ Bulunamadı — 10sn sonra tekrar ─────────────────────────────────
                if (!ct.IsCancellationRequested)
                {
                    ConnectionStatus = "⚠️ Cihaz bulunamadı. Cihazın açık ve aynı ağda olduğundan emin olun. (10sn sonra tekrar aranacak...)";
                    try { await Task.Delay(10_000, ct); }
                    catch (OperationCanceledException) { break; }
                }
            }

            IsSearching = false;
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            ConnectionStatus = "Bağlanıyor...";
            try
            {
                var res  = await _http.GetStringAsync($"http://{DeviceIp}/status");
                using var doc  = JsonDocument.Parse(res);
                var root = doc.RootElement;
                IsConnected = root.GetProperty("ok").GetBoolean();
                CurrentMode = root.GetProperty("mode").GetInt32();
                Brightness  = (byte)root.GetProperty("brightness").GetInt32();
                var mac = root.TryGetProperty("mac", out var m) ? m.GetString() : "?";
                var ver = root.TryGetProperty("version", out var v) ? v.GetString() : "?";
                DeviceInfo      = $"MAC: {mac} | Ver: {ver}";
                ConnectionStatus = $"✅ Bağlandı ({DeviceIp})";
                _udp?.Close();
                _udp = new UdpClient();
                _udp.Connect(DeviceIp, 4210);
            }
            catch (Exception ex)
            {
                IsConnected      = false;
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
                IsConnected      = false;
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

        [RelayCommand]
        private void PickAnimColor()
        {
            using var dlg = new ColorDialog();
            dlg.Color = System.Drawing.Color.FromArgb(AnimColor.R, AnimColor.G, AnimColor.B);
            if (dlg.ShowDialog() == DialogResult.OK)
                AnimColor = MediaColor.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B);
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

        // ═════════════════════════════════════════════════════════════════════
        // AMBILIGHT — Ekranın TAMAMI 320×180 küçük kopyaya indir, 6 dikey zona
        // ═════════════════════════════════════════════════════════════════════
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
            var sw     = System.Diagnostics.Stopwatch.StartNew();
            int frames = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    int    delayMs = 1000 / Math.Max(1, AmbiTargetFps);
                    byte[] packet  = CaptureFullScreenColors();

                    if (_udp != null)
                        try { _udp.Send(packet, packet.Length); } catch { }

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
                        frames  = 0;
                        sw.Restart();
                    }
                    await Task.Delay(delayMs, ct);
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(100, ct); }
            }
        }

        /// <summary>
        /// Tüm ekranı 320×180 küçük bitmap'e çeker, 6 dikey zone'a böler,
        /// her zone'un ortalama rengini hesaplar. Gamma düzeltmesi ile canlı.
        /// </summary>
        private byte[] CaptureFullScreenColors()
        {
            var buf = new byte[18];
            try
            {
                var screen  = Screen.PrimaryScreen ?? Screen.AllScreens[0];
                var b       = screen.Bounds;
                const int W = 320;
                const int H = 180;

                // Ekranı küçük bitmap'e çek (tüm ekran, küçük = hızlı)
                using var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                    // Önce native çözünürlükte yakala
                    using var full = new Bitmap(b.Width, b.Height, PixelFormat.Format24bppRgb);
                    using (var gf = Graphics.FromImage(full))
                        gf.CopyFromScreen(b.X, b.Y, 0, 0, b.Size);
                    // Küçük boyuta ölçekle
                    g.DrawImage(full, 0, 0, W, H);
                }

                var bmpData = bmp.LockBits(
                    new Rectangle(0, 0, W, H),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format24bppRgb);

                int stride = bmpData.Stride;
                var raw    = new byte[stride * H];
                Marshal.Copy(bmpData.Scan0, raw, 0, raw.Length);
                bmp.UnlockBits(bmpData);

                int zoneW = W / 6;
                for (int z = 0; z < 6; z++)
                {
                    long r = 0, gv = 0, bv = 0;
                    int  cnt    = 0;
                    int  xStart = z * zoneW;
                    int  xEnd   = xStart + zoneW;

                    for (int py = 0; py < H; py += 5)
                        for (int px = xStart; px < xEnd; px += 5)
                        {
                            int idx = py * stride + px * 3;
                            bv += raw[idx];
                            gv += raw[idx + 1];
                            r  += raw[idx + 2];
                            cnt++;
                        }

                    if (cnt > 0)
                    {
                        buf[z*3]   = Gamma((double)r  / cnt);
                        buf[z*3+1] = Gamma((double)gv / cnt);
                        buf[z*3+2] = Gamma((double)bv / cnt);
                    }
                }
            }
            catch { }
            return buf;
        }

        private static byte Gamma(double v) =>
            (byte)(Math.Pow(v / 255.0, 0.55) * 255.0);

        // ═════════════════════════════════════════════════════════════════════
        // SES ANALİZİ — 2 Değer: BASS + ENERGY
        // Pırpır yok: asimetrik smoothing + beat pulse + hue shift
        // ═════════════════════════════════════════════════════════════════════
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
            try { _audioCapture?.Dispose();       } catch { }
            _audioCapture = null;
        }

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
                        int floatCount = e.BytesRecorded / 4;
                        lock (_fftLock)
                        {
                            for (int i = 0; i < floatCount; i += ch)
                            {
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
                        $"Ses yakalama başlatılamadı:\n{ex.Message}\n\nWindows Ses → Kayıt → Stereo Mix etkin olmalı.",
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
            float  gain  = (float)Math.Clamp(AudioGain, 0.5, 20.0);

            // ── 1. BASS RMS (20–300 Hz) ───────────────────────────────────────
            int    bassLo  = Math.Max(0,    (int)(20.0  / binHz));
            int    bassHi  = Math.Min(1023, (int)(300.0 / binHz));
            double bassRms = 0;
            for (int k = bassLo; k <= bassHi; k++)
                bassRms += cx[k].Magnitude * cx[k].Magnitude;
            bassRms = Math.Sqrt(bassRms / Math.Max(1, bassHi - bassLo + 1));

            // ── 2. ENERGY — Tüm spektrum RMS ─────────────────────────────────
            double totalRms = 0;
            for (int k = 0; k < 1024; k++)
                totalRms += cx[k].Magnitude * cx[k].Magnitude;
            totalRms = Math.Sqrt(totalRms / 1024.0);

            float bassLevel   = (float)Math.Min(1.0, bassRms  * gain);
            float energyLevel = (float)Math.Min(1.0, totalRms * gain * 0.7f);

            // ── Asimetrik Smoothing: hızlı yüksel, yavaş düş → pırpır yok ────
            float bassAtk   = bassLevel   > _smoothBass   ? 0.35f : 0.93f;
            float energyAtk = energyLevel > _smoothEnergy ? 0.40f : 0.90f;
            _smoothBass   = _smoothBass   * bassAtk   + bassLevel   * (1f - bassAtk);
            _smoothEnergy = _smoothEnergy * energyAtk + energyLevel * (1f - energyAtk);

            // ── Beat Detection: bass ani sıçrayış → kısa parlaklık patlaması ─
            bool isBeat = bassLevel > _smoothBass * 1.7f && _smoothBass > 0.04f;
            if (isBeat) _beatBrightness = Math.Min(1.0f, _beatBrightness + 0.55f);
            else        _beatBrightness *= 0.86f;

            // ── Hue Shift: bass yüksekse sıcak (kırmızı), düşükse serin (mavi) ─
            long  nowMs     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _lastFftMs      = nowMs;
            // 0.02 (kırmızı/turuncu) → 0.62 (mavi/mor)
            float targetHue = 0.02f + (1.0f - _smoothBass) * 0.60f;
            _hue += (targetHue - _hue) * 0.04f; // çok yavaş kayar, pırpır yok

            // ── Parlaklık: energy + beat pulse ─────────────────────────────────
            float brightness = Math.Clamp(_smoothEnergy * 1.6f + _beatBrightness * 0.35f, 0f, 1f);

            var (rBase, gBase, bBase) = HsvToRgb(_hue, 0.92f, brightness);

            var packet   = new byte[18];
            var uiColors = new MediaColor[6];

            // 6 LED aynı renk, ortadan kenarlara hafif dalga (sönme)
            for (int z = 0; z < 6; z++)
            {
                float wave = 1.0f - MathF.Abs(z - 2.5f) / 6.0f * 0.25f;
                byte  pr   = (byte)(rBase * wave);
                byte  pg   = (byte)(gBase * wave);
                byte  pb   = (byte)(bBase * wave);
                packet[z*3] = pr; packet[z*3+1] = pg; packet[z*3+2] = pb;
                uiColors[z] = MediaColor.FromRgb(pr, pg, pb);
            }

            if (_udp != null)
                try { _udp.Send(packet, packet.Length); } catch { }

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                for (int z = 0; z < 6; z++) AudioColors[z] = uiColors[z];
            });
        }

        /// <summary>HSV → RGB (h, s, v: 0-1 arası)</summary>
        private static (byte r, byte g, byte b) HsvToRgb(float h, float s, float v)
        {
            if (v <= 0f) return (0, 0, 0);
            h = h - MathF.Floor(h);
            int   i = (int)(h * 6f);
            float f = h * 6f - i;
            float p = v * (1f - s);
            float q = v * (1f - f * s);
            float t = v * (1f - (1f - f) * s);
            var (rf, gf, bf) = (i % 6) switch
            {
                0 => (v, t, p),
                1 => (q, v, p),
                2 => (p, v, t),
                3 => (p, q, v),
                4 => (t, p, v),
                _ => (v, p, q)
            };
            return ((byte)(rf * 255f), (byte)(gf * 255f), (byte)(bf * 255f));
        }

        /// <summary>Cooley-Tukey FFT — in-place iterative</summary>
        private static void FftInPlace(Complex[] a)
        {
            int n = a.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { var tmp = a[i]; a[i] = a[j]; a[j] = tmp; }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang  = -2.0 * Math.PI / len;
                var    wlen = new Complex(Math.Cos(ang), Math.Sin(ang));
                for (int i = 0; i < n; i += len)
                {
                    var w = Complex.One;
                    for (int j = 0; j < len / 2; j++)
                    {
                        var u = a[i + j];
                        var vv = a[i + j + len/2] * w;
                        a[i + j]         = u + vv;
                        a[i + j + len/2] = u - vv;
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
        [ObservableProperty] private int        _index;
        [ObservableProperty] private MediaColor _color;
    }
}
