using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IoTLedController.Models;
using IoTLedController.Services;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Media;

namespace IoTLedController.ViewModels;

// =============================================================================
//  MainViewModel.cs  —  MVVM ViewModel (CommunityToolkit.Mvvm)
// =============================================================================

public partial class MainViewModel : ObservableObject, IDisposable
{
    // ─── Servisler ────────────────────────────────────────────────────────────
    private readonly UdpSender            _udp;
    private readonly AmbiLightService     _ambi;
    private readonly AudioAnalysisService _audio;
    private readonly SpotifyService       _spotify;
    private readonly HttpClient           _http;

    // ─── Bağlantı ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string  _espIp      = "192.168.1.100";
    [ObservableProperty] private int     _udpPort    = 4210;
    [ObservableProperty] private bool    _isConnected = false;
    [ObservableProperty] private string  _connectionStatus = "Bağlı değil";
    [ObservableProperty] private string  _currentPage = "Connect";
    [ObservableProperty] private long    _udpSentPackets = 0;
    [ObservableProperty] private string  _deviceInfo = "";

    // ─── LED Kontrol ──────────────────────────────────────────────────────────
    [ObservableProperty] private int     _selectedMode = 0;

    // Mod radio buton bağlamaları
    public bool IsMode0 { get => _selectedMode == 0; set { if (value) { SelectedMode = 0; OnPropertyChanged(); } } }
    public bool IsMode1 { get => _selectedMode == 1; set { if (value) { SelectedMode = 1; OnPropertyChanged(); } } }
    public bool IsMode2 { get => _selectedMode == 2; set { if (value) { SelectedMode = 2; OnPropertyChanged(); } } }
    public bool IsMode3 { get => _selectedMode == 3; set { if (value) { SelectedMode = 3; OnPropertyChanged(); } } }
    [ObservableProperty] private int     _brightness   = 80;
    [ObservableProperty] private Color   _globalColor  = Colors.White;

    // 6 LED'in bireysel renkleri
    public ObservableCollection<LedColorItem> LedColors { get; } = new(
        Enumerable.Range(0, 6).Select(i => new LedColorItem { Index = i, Color = Colors.Black }));

    // ─── AmbiLight ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _ambiRunning  = false;
    [ObservableProperty] private double _ambiFps      = 0;
    [ObservableProperty] private int    _ambiTargetFps = 30;

    // Canlı AmbiLight renk önizlemesi
    public ObservableCollection<Color> AmbiColors { get; } = new(
        Enumerable.Repeat(Colors.Black, 6));

    // ─── Ses Analizi ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _audioRunning  = false;
    [ObservableProperty] private float  _audioGain     = 3.0f;
    [ObservableProperty] private float  _audioSmoothing = 0.7f;

    public ObservableCollection<Color> AudioColors { get; } = new(
        Enumerable.Repeat(Colors.Black, 6));

    // ─── Spotify ──────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _spotifyAuthenticated = false;
    [ObservableProperty] private bool   _spotifyRunning       = false;
    [ObservableProperty] private string _spotifyClientId      = "YOUR_SPOTIFY_CLIENT_ID";
    [ObservableProperty] private string _spotifyStatus        = "Bağlı değil";
    [ObservableProperty] private string _currentTrackTitle    = "";
    [ObservableProperty] private string _currentTrackArtist   = "";

    public ObservableCollection<Color> SpotifyColors { get; } = new(
        Enumerable.Repeat(Colors.Black, 6));

    // ─── Durum çubuğu ────────────────────────────────────────────────────────
    [ObservableProperty] private string _statusMessage = "Hazır";

    // ─── Yapıcı ───────────────────────────────────────────────────────────────
    public MainViewModel()
    {
        _udp     = new UdpSender();
        _ambi    = new AmbiLightService(_udp);
        _audio   = new AudioAnalysisService(_udp);
        _spotify = new SpotifyService(_udp);
        _http    = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        // Servis olaylarını bağla
        _ambi.ColorsUpdated    += OnAmbiColorsUpdated;
        _audio.ColorsUpdated   += OnAudioColorsUpdated;
        _spotify.ColorsUpdated += OnSpotifyColorsUpdated;
        _spotify.TrackChanged  += OnTrackChanged;
        _spotify.StatusChanged += s => App.Current.Dispatcher.Invoke(() => SpotifyStatus = s);
    }

    // =========================================================================
    //  KOMUTLAR — Navigasyon
    // =========================================================================

    [RelayCommand]
    private void Navigate(string page) => CurrentPage = page;

    // =========================================================================
    //  KOMUTLAR — Bağlantı
    // =========================================================================

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            StatusMessage = "Bağlanılıyor...";
            _udp.Connect(EspIp, UdpPort);
            _http.BaseAddress = new Uri($"http://{EspIp}/");

            // /status endpoint'ini test et
            var resp = await _http.GetAsync("status");
            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root      = doc.RootElement;
            int mode      = root.GetProperty("mode").GetInt32();
            int bright    = root.GetProperty("brightness").GetInt32();
            string ver    = root.GetProperty("version").GetString() ?? "";

            SelectedMode   = mode;
            Brightness     = bright;
            IsConnected    = true;
            DeviceInfo     = $"IP: {EspIp}  |  v{ver}";
            ConnectionStatus = $"Bağlandı — {EspIp}";
            StatusMessage  = $"ESP32 bağlandı (v{ver})";
        }
        catch (Exception ex)
        {
            IsConnected      = false;
            ConnectionStatus = "Bağlantı başarısız";
            StatusMessage    = $"Hata: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        if (!IsConnected) return;
        StatusMessage = "OTA güncelleme kontrol ediliyor...";
        try
        {
            await _http.GetAsync("checkUpdate");
            StatusMessage = "OTA kontrolü başlatıldı (sonuç için seri monitor'ü izleyin)";
        }
        catch (Exception ex) { StatusMessage = $"OTA Hatası: {ex.Message}"; }
    }

    // =========================================================================
    //  KOMUTLAR — LED Kontrol
    // =========================================================================

    [RelayCommand]
    private async Task SetModeAsync()
    {
        await PostJsonAsync("setMode", $"{{\"mode\":{SelectedMode}}}");
        StatusMessage = $"Mod: {GetModeName(SelectedMode)}";
    }

    [RelayCommand]
    private async Task SetGlobalColorAsync()
    {
        var c = GlobalColor;
        await PostJsonAsync("setColor", $"{{\"r\":{c.R},\"g\":{c.G},\"b\":{c.B}}}");
    }

    [RelayCommand]
    private async Task PickAndSetGlobalColorAsync()
    {
        var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            GlobalColor = Color.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B);
            await SetGlobalColorAsync();
        }
    }

    [RelayCommand]
    private async Task PickAndSetLedColorAsync(LedColorItem item)
    {
        var dlg = new System.Windows.Forms.ColorDialog { FullOpen = true };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            item.Color = Color.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B);
            item.OnPropertyChanged(nameof(item.Color));
            item.OnPropertyChanged(nameof(item.Brush));
            await SetLedColorAsync(item);
        }
    }

    [RelayCommand]
    private async Task SetLedColorAsync(LedColorItem item)
    {
        var c = item.Color;
        await PostJsonAsync("setLedColor",
            $"{{\"index\":{item.Index},\"r\":{c.R},\"g\":{c.G},\"b\":{c.B}}}");
    }

    [RelayCommand]
    private async Task SetBrightnessAsync()
    {
        await PostJsonAsync("setBrightness", $"{{\"brightness\":{Brightness}}}");
    }

    // =========================================================================
    //  KOMUTLAR — AmbiLight
    // =========================================================================

    [RelayCommand]
    private async Task ToggleAmbiLightAsync()
    {
        if (!_ambi.IsRunning)
        {
            if (!IsConnected) { StatusMessage = "Önce ESP32'ye bağlanın"; return; }
            await SetModeAsync(); // UDP modunu aktifleştir
            _ambi.TargetFps = AmbiTargetFps;
            _ambi.Start();
            AmbiRunning   = true;
            StatusMessage = $"AmbiLight başlatıldı ({AmbiTargetFps} FPS)";
        }
        else
        {
            await _ambi.StopAsync();
            AmbiRunning   = false;
            StatusMessage = "AmbiLight durduruldu";
        }
    }

    // =========================================================================
    //  KOMUTLAR — Ses Analizi
    // =========================================================================

    [RelayCommand]
    private async Task ToggleAudioAsync()
    {
        if (!_audio.IsRunning)
        {
            if (!IsConnected) { StatusMessage = "Önce ESP32'ye bağlanın"; return; }
            _audio.Gain      = AudioGain;
            _audio.Smoothing = AudioSmoothing;
            _audio.Start();
            AudioRunning  = true;
            StatusMessage = "Ses analizi başlatıldı";
        }
        else
        {
            await _audio.StopAsync();
            AudioRunning  = false;
            StatusMessage = "Ses analizi durduruldu";
        }
    }

    // =========================================================================
    //  KOMUTLAR — Spotify
    // =========================================================================

    [RelayCommand]
    private async Task SpotifyAuthAsync()
    {
        try
        {
            _spotify.SetCredentials(SpotifyClientId);
            StatusMessage = "Spotify giriş sayfası açılıyor...";
            await _spotify.AuthenticateAsync();
            SpotifyAuthenticated = true;
            StatusMessage = "Spotify bağlandı ✓";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Spotify hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleSpotifyAsync()
    {
        if (!SpotifyAuthenticated)
        {
            StatusMessage = "Önce Spotify'a giriş yapın";
            return;
        }

        if (!_spotify.IsRunning)
        {
            if (!IsConnected) { StatusMessage = "Önce ESP32'ye bağlanın"; return; }
            _spotify.Start();
            SpotifyRunning = true;
            StatusMessage  = "Spotify modu başlatıldı";
        }
        else
        {
            await _spotify.StopAsync();
            SpotifyRunning = false;
            StatusMessage  = "Spotify modu durduruldu";
        }
    }

    // =========================================================================
    //  Olaylar — Renk güncellemeleri (UI thread'e marshal)
    // =========================================================================

    private void OnAmbiColorsUpdated(LedColor[] colors)
    {
        UdpSentPackets = _udp.SentPackets;
        App.Current.Dispatcher.Invoke(() => {
            AmbiFps = _ambi.ActualFps;
            UpdateColorCollection(AmbiColors, colors);
        });
    }



    private void OnAudioColorsUpdated(LedColor[] colors) =>
        App.Current.Dispatcher.Invoke(() => UpdateColorCollection(AudioColors, colors));

    private void OnSpotifyColorsUpdated(LedColor[] colors) =>
        App.Current.Dispatcher.Invoke(() => UpdateColorCollection(SpotifyColors, colors));

    private void OnTrackChanged(string title, string artist) =>
        App.Current.Dispatcher.Invoke(() => {
            CurrentTrackTitle  = title;
            CurrentTrackArtist = artist;
        });

    private static void UpdateColorCollection(ObservableCollection<Color> col, LedColor[] colors)
    {
        for (int i = 0; i < Math.Min(col.Count, colors.Length); i++)
            col[i] = colors[i].ToMediaColor();
    }

    // =========================================================================
    //  Yardımcılar
    // =========================================================================

    private async Task PostJsonAsync(string endpoint, string json)
    {
        if (!IsConnected) return;
        try
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _http.PostAsync(endpoint, content);
        }
        catch (Exception ex)
        {
            StatusMessage = $"HTTP Hatası: {ex.Message}";
        }
    }

    private static string GetModeName(int mode) => mode switch
    {
        0 => "Statik",
        1 => "Knight Rider",
        2 => "Şimşek",
        3 => "UDP / Ambilight",
        _ => "Bilinmeyen"
    };

    public void Dispose()
    {
        _ambi.Dispose();
        _audio.Dispose();
        _spotify.Dispose();
        _udp.Dispose();
        _http.Dispose();
    }
}

// ─── Yardımcı model: Bireysel LED renk öğesi ─────────────────────────────────
public class LedColorItem : ObservableObject
{
    public int Index { get; set; }

    private Color _color;
    public Color Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    public SolidColorBrush Brush => new(Color);
    public string Label => $"LED {Index + 1}";
}
