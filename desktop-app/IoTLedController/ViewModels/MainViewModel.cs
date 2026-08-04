using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
        private CancellationTokenSource? _audioCts;
        private WasapiLoopbackCapture? _audioCapture;

        // ── Navigasyon ────────────────────────────────────────────────────────
        [ObservableProperty] private string _currentPage = "Connect";

        // ── Bağlantı ──────────────────────────────────────────────────────────
        [ObservableProperty] private string _deviceIp = "192.168.1.100";
        [ObservableProperty] private bool   _isConnected;
        [ObservableProperty] private string _connectionStatus = "Cihaz Aranıyor...";
        [ObservableProperty] private string _deviceInfo = "—";
        [ObservableProperty] private bool   _isSearching;

        // ── LED Kontrol ───────────────────────────────────────────────────────
        [ObservableProperty] private int        _currentMode;
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
        [ObservableProperty] private double _audioGain = 2.5;
        [ObservableProperty] private double _audioSmoothing = 0.5;
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

            // Otomatik Keşif
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
                var endPoint = new IPEndPoint(IPAddress.Broadcast, 4210);
                await udpClient.SendAsync(discoverPacket, discoverPacket.Length, endPoint);

                var result = await udpClient.ReceiveAsync();
                string responseStr = Encoding.UTF8.GetString(result.Buffer);

                using var doc = JsonDocument.Parse(responseStr);
                if (doc.RootElement.TryGetProperty("ip", out var ipProp))
                {
                    string foundIp = ipProp.GetString()!;
                    DeviceIp = foundIp;
                    ConnectionStatus = $"Cihaz Bulundu! ({foundIp}) Bağlanılıyor...";
                    await ConnectAsync();
                    return;
                }
            }
            catch
            {
                try
                {
                    DeviceIp = "iot-led.local";
                    await ConnectAsync();
                    return;
                }
                catch { }
            }
            finally
            {
                IsSearching = false;
            }

            if (!IsConnected)
            {
                ConnectionStatus = "Otomatik cihaz bulunamadı. IP adresini manuel girin.";
            }
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
            var dialogResult = System.Windows.MessageBox.Show(
                "Wi-Fi ayarları silinip cihaz AP moduna ('IoT-LED-Setup') geçecek.\nEmin misiniz?",
                "Wi-Fi Sıfırla",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (dialogResult == System.Windows.MessageBoxResult.Yes)
            {
                await PostJsonAsync("/reset", new { });
                IsConnected = false;
                ConnectionStatus = "Wi-Fi Sıfırlandı. Cihaz 'IoT-LED-Setup' ağında başlatılıyor.";
            }
        }

        [RelayCommand]
        private async Task SetModeAsync(string modeStr)
        {
            if (int.TryParse(modeStr, out int m))
            {
                CurrentMode = m;
                await PostJsonAsync("/setMode", new { mode = m });
            }
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
            {
                var c = MediaColor.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B);
                SetGlobalColor(c);
            }
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

        // ── AMBILIGHT (EKRAN OKUMA) ───────────────────────────────────────────
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
                await SetModeAsync("3"); // ESP32'yi UDP Moduna geçir
                _ambiCts = new CancellationTokenSource();
                Task.Run(() => RunAmbiLoopAsync(_ambiCts.Token));
            }
        }

        private async Task RunAmbiLoopAsync(CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int frames = 0;

            while (!ct.IsCancellationRequested)
            {
                int delayMs = 1000 / Math.Max(1, AmbiTargetFps);
                byte[] packet = CaptureBottomScreenColors();

                if (_udp != null && packet.Length == 18)
                {
                    try { await _udp.SendAsync(packet, packet.Length); } catch { }
                }

                for (int i = 0; i < 6; i++)
                {
                    int idx = i * 3;
                    var c = MediaColor.FromRgb(packet[idx], packet[idx + 1], packet[idx + 2]);
                    int capturedI = i;
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => AmbiColors[capturedI] = c);
                }

                frames++;
                if (sw.ElapsedMilliseconds >= 1000)
                {
                    AmbiFps = frames * 1000.0 / sw.ElapsedMilliseconds;
                    frames = 0;
                    sw.Restart();
                }

                await Task.Delay(delayMs, ct);
            }
        }

        private byte[] CaptureBottomScreenColors()
        {
            byte[] buf = new byte[18];
            try
            {
                var bounds = Screen.PrimaryScreen.Bounds;
                int h = Math.Max(10, bounds.Height / 5);
                int y = bounds.Height - h;

                using var bmp = new Bitmap(bounds.Width, h, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.X, y, 0, 0, new Size(bounds.Width, h));
                }

                int zoneW = bounds.Width / 6;
                for (int z = 0; z < 6; z++)
                {
                    long r = 0, g = 0, b = 0;
                    int count = 0;

                    for (int px = z * zoneW; px < (z + 1) * zoneW; px += 15)
                    {
                        for (int py = 0; py < h; py += 15)
                        {
                            var c = bmp.GetPixel(px, py);
                            r += c.R; g += c.G; b += c.B;
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        buf[z * 3]     = (byte)(r / count);
                        buf[z * 3 + 1] = (byte)(g / count);
                        buf[z * 3 + 2] = (byte)(b / count);
                    }
                }
            }
            catch { }

            return buf;
        }

        // ── GERÇEK NAUDIO SES ANALİZİ (WASAPI LOOPBACK) ──────────────────────
        [RelayCommand]
        private async Task ToggleAudioAsync()
        {
            if (AudioRunning)
            {
                AudioRunning = false;
                try
                {
                    _audioCapture?.StopRecording();
                    _audioCapture?.Dispose();
                    _audioCapture = null;
                }
                catch { }
            }
            else
            {
                AudioRunning = true;
                await SetModeAsync("3"); // ESP32'yi UDP Moduna geçir
                StartWasapiAudioCapture();
            }
        }

        private void StartWasapiAudioCapture()
        {
            try
            {
                _audioCapture = new WasapiLoopbackCapture();
                _audioCapture.DataAvailable += (s, e) =>
                {
                    if (!AudioRunning) return;

                    float maxVol = 0;
                    int sampleCount = e.BytesRecorded / 4;
                    for (int i = 0; i < e.BytesRecorded; i += 4)
                    {
                        float sample = BitConverter.ToSingle(e.Buffer, i);
                        maxVol = Math.Max(maxVol, Math.Abs(sample));
                    }

                    float volume = Math.Min(1.0f, maxVol * (float)AudioGain);
                    byte r = (byte)(volume * 255);
                    byte g = (byte)(volume * 180);
                    byte b = (byte)((1.0f - volume) * 255);

                    byte[] packet = new byte[18];
                    for (int i = 0; i < 6; i++)
                    {
                        packet[i * 3]     = r;
                        packet[i * 3 + 1] = g;
                        packet[i * 3 + 2] = b;

                        var c = MediaColor.FromRgb(r, g, b);
                        int capI = i;
                        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => AudioColors[capI] = c);
                    }

                    if (_udp != null)
                    {
                        try { _udp.Send(packet, packet.Length); } catch { }
                    }
                };

                _audioCapture.StartRecording();
            }
            catch (Exception ex)
            {
                AudioRunning = false;
                System.Windows.MessageBox.Show($"Ses yakalama başlatılamadı: {ex.Message}");
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
