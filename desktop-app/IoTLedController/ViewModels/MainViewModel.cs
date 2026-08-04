using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
using MediaColor = System.Windows.Media.Color;

namespace IoTLedController.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
        private UdpClient? _udp;
        private CancellationTokenSource? _ambiCts;
        private CancellationTokenSource? _audioCts;

        // ── Navigasyon ────────────────────────────────────────────────────────
        [ObservableProperty] private string _currentPage = "Connect";

        // ── Bağlantı ──────────────────────────────────────────────────────────
        [ObservableProperty] private string _deviceIp = "192.168.1.100";
        [ObservableProperty] private bool   _isConnected;
        [ObservableProperty] private string _connectionStatus = "Bağlı Değil";
        [ObservableProperty] private string _deviceInfo = "—";

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
        [ObservableProperty] private double _audioGain = 2.0;
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
        }

        [RelayCommand]
        private void Navigate(string page) => CurrentPage = page;

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

        [RelayCommand]
        private void ToggleAmbiLight()
        {
            if (AmbiRunning)
            {
                _ambiCts?.Cancel();
                AmbiRunning = false;
            }
            else
            {
                AmbiRunning = true;
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
                    await _udp.SendAsync(packet, packet.Length);
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
                int h = bounds.Height / 5;
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

                    for (int px = z * zoneW; px < (z + 1) * zoneW; px += 10)
                    {
                        for (int py = 0; py < h; py += 10)
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

        [RelayCommand]
        private void ToggleAudio()
        {
            if (AudioRunning)
            {
                _audioCts?.Cancel();
                AudioRunning = false;
            }
            else
            {
                AudioRunning = true;
                _audioCts = new CancellationTokenSource();
                Task.Run(() => RunAudioLoopAsync(_audioCts.Token));
            }
        }

        private async Task RunAudioLoopAsync(CancellationToken ct)
        {
            var rnd = new Random();
            while (!ct.IsCancellationRequested)
            {
                byte[] packet = new byte[18];
                for (int i = 0; i < 6; i++)
                {
                    byte r = (byte)(rnd.Next(0, 255) * AudioGain);
                    byte g = (byte)(rnd.Next(0, 255) * AudioGain);
                    byte b = (byte)(rnd.Next(0, 255) * AudioGain);
                    packet[i * 3]     = r;
                    packet[i * 3 + 1] = g;
                    packet[i * 3 + 2] = b;

                    var c = MediaColor.FromRgb(r, g, b);
                    int capturedI = i;
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => AudioColors[capturedI] = c);
                }

                if (_udp != null)
                {
                    await _udp.SendAsync(packet, packet.Length);
                }

                await Task.Delay(50, ct);
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
