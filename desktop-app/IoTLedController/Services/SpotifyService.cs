using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using ColorThief;
using System.Drawing;
using IoTLedController.Models;

namespace IoTLedController.Services;

// =============================================================================
//  SpotifyService.cs  —  Spotify Web API → Albüm kapağı renk analizi
//
//  Çalışma prensibi:
//    1. OAuth 2.0 PKCE akışı ile Spotify'a giriş (tarayıcı açılır)
//    2. Aktif çalan şarkıyı düzenli kontrol et (3 sn aralık)
//    3. Şarkı değişirse albüm kapağını indir
//    4. ColorThief ile dominant 6 rengi çıkar
//    5. Audio features (tempo, valence, energy) ile animasyon üret
//    6. 10-30 FPS'de UDP üzerinden LED'lere gönder
// =============================================================================

public sealed class SpotifyService : IDisposable
{
    private readonly UdpSender     _udp;
    private readonly HttpClient    _http;

    // Spotify credentials — kullanıcı tarafından doldurulacak
    private string _clientId     = "YOUR_SPOTIFY_CLIENT_ID";
    private int    _callbackPort = 5543;

    private SpotifyClient?          _spotify;
    private EmbedIOAuthServer?      _authServer;
    private CancellationTokenSource? _cts;
    private Task?                   _mainLoop;

    // Şarkı durumu
    private string   _currentTrackId = "";
    private LedColor[] _palette      = new LedColor[6];
    private float    _tempo          = 120;
    private float    _energy         = 0.5f;
    private float    _animPhase      = 0f;
    private bool     _isPlaying      = false;

    // Durum olayları
    public event Action<string, string>?  TrackChanged;  // (title, artist)
    public event Action<LedColor[]>?      ColorsUpdated;
    public event Action<string>?          StatusChanged;

    public bool IsAuthenticated => _spotify is not null;
    public bool IsRunning       { get; private set; }

    public SpotifyService(UdpSender udp)
    {
        _udp  = udp;
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    // ── Kimlik bilgilerini ayarla ──────────────────────────────────────────────
    public void SetCredentials(string clientId)
    {
        _clientId = clientId;
    }

    // ── OAuth Giriş ───────────────────────────────────────────────────────────
    public async Task AuthenticateAsync()
    {
        StatusChanged?.Invoke("Spotify'a bağlanılıyor...");

        _authServer?.Dispose();
        _authServer = new EmbedIOAuthServer(
            new Uri($"http://localhost:{_callbackPort}/callback"), _callbackPort);
        await _authServer.Start();

        var tcs = new TaskCompletionSource<string>();

        _authServer.AuthorizationCodeReceived += async (_, response) =>
        {
            await _authServer.Stop();
            var tokenResponse = await new OAuthClient().RequestToken(
                new PKCETokenRequest(_clientId, response.Code, _authServer.BaseUri,
                    response.State, PKCEUtil.GetVerifier(response.State)));

            _spotify = new SpotifyClient(SpotifyClientConfig.CreateDefault()
                .WithAuthenticator(new PKCEAuthenticator(_clientId, tokenResponse)));

            tcs.TrySetResult("ok");
            StatusChanged?.Invoke("Spotify bağlandı ✓");
        };

        _authServer.ErrorReceived += (_, error, state) =>
        {
            tcs.TrySetException(new Exception($"OAuth hatası: {error}"));
        };

        var (verifier, challenge) = PKCEUtil.GenerateCodes();
        var loginRequest = new LoginRequest(_authServer.BaseUri, _clientId,
            LoginRequest.ResponseType.Code)
        {
            CodeChallengeMethod = "S256",
            CodeChallenge       = challenge,
            State               = verifier,
            Scope               = new[] {
                Scopes.UserReadCurrentlyPlaying,
                Scopes.UserReadPlaybackState,
            }
        };

        // Tarayıcıda Spotify giriş sayfasını aç
        var loginUri = loginRequest.ToUri();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = loginUri.ToString(),
            UseShellExecute = true
        });

        // OAuth tamamlanana kadar bekle (max 120 sn)
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(120));
    }

    // ── Servisi Başlat ────────────────────────────────────────────────────────
    public void Start()
    {
        if (IsRunning || _spotify is null) return;
        IsRunning = true;
        _cts = new CancellationTokenSource();
        _mainLoop = Task.Run(() => MainLoop(_cts.Token));
    }

    // ── Servisi Durdur ────────────────────────────────────────────────────────
    public async Task StopAsync()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _cts?.Cancel();
        if (_mainLoop is not null)
            await _mainLoop.ConfigureAwait(false);
        _udp.SendColors(Enumerable.Repeat(LedColor.Black, 6).ToArray());
    }

    // ── Ana döngü ─────────────────────────────────────────────────────────────
    private async Task MainLoop(CancellationToken ct)
    {
        using var trackTimer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        using var ledTimer   = new PeriodicTimer(TimeSpan.FromMilliseconds(50)); // ~20 FPS

        var trackTask = TrackUpdateLoop(trackTimer, ct);
        var ledTask   = LedAnimateLoop(ledTimer, ct);

        await Task.WhenAll(trackTask, ledTask).ConfigureAwait(false);
    }

    // ── Şarkı güncelleme döngüsü (3 sn aralık) ───────────────────────────────
    private async Task TrackUpdateLoop(PeriodicTimer timer, CancellationToken ct)
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await UpdateCurrentTrackAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[SPOTIFY] Track hatası: {ex.Message}");
            }
        }
    }

    // ── LED animasyon döngüsü (~20 FPS) ───────────────────────────────────────
    private async Task LedAnimateLoop(PeriodicTimer timer, CancellationToken ct)
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            if (!_isPlaying) continue;

            // Tempo'ya göre faz hesapla
            _animPhase += (_tempo / 60f) * 0.05f;  // BPM → döngü hızı
            if (_animPhase > 1f) _animPhase -= 1f;

            LedColor[] frame = AnimateFrame(_palette, _animPhase, _energy);
            _udp.SendColors(frame);
            ColorsUpdated?.Invoke(frame);
        }
    }

    // ── Aktif şarkıyı Spotify'dan çek ─────────────────────────────────────────
    private async Task UpdateCurrentTrackAsync()
    {
        if (_spotify is null) return;

        var playback = await _spotify.Player.GetCurrentPlayback();
        _isPlaying = playback?.IsPlaying ?? false;

        if (!_isPlaying || playback?.Item is not FullTrack track) return;

        if (track.Id == _currentTrackId) return;  // Aynı şarkı
        _currentTrackId = track.Id;

        string title  = track.Name;
        string artist = string.Join(", ", track.Artists.Select(a => a.Name));
        StatusChanged?.Invoke($"♪ {title} — {artist}");
        TrackChanged?.Invoke(title, artist);

        // Albüm kapağını indir ve renkleri çıkar
        string? imageUrl = track.Album.Images.FirstOrDefault()?.Url;
        if (imageUrl is not null)
            _palette = await ExtractPaletteAsync(imageUrl);

        // Audio features: tempo, energy
        var features = await _spotify.Tracks.GetAudioFeatures(track.Id);
        if (features is not null)
        {
            _tempo  = features.Tempo;
            _energy = features.Energy;
        }
    }

    // ── Albüm kapağından renk paleti çıkar ───────────────────────────────────
    private async Task<LedColor[]> ExtractPaletteAsync(string imageUrl)
    {
        try
        {
            byte[] imgBytes = await _http.GetByteArrayAsync(imageUrl);
            using var ms  = new System.IO.MemoryStream(imgBytes);
            using var bmp = new Bitmap(ms);

            var thief  = new ColorThief.ColorThief();
            var quant  = thief.GetPalette(bmp, colorCount: 6, quality: 5);
            var palette = new LedColor[6];

            for (int i = 0; i < Math.Min(6, quant.Count); i++)
            {
                var c = quant[i].Color;
                palette[i] = new LedColor(c.R, c.G, c.B);
            }
            // Eksik slotları son renkle doldur
            for (int i = quant.Count; i < 6; i++)
                palette[i] = palette[Math.Max(0, quant.Count - 1)];

            return palette;
        }
        catch
        {
            return Enumerable.Repeat(new LedColor(128, 0, 128), 6).ToArray();
        }
    }

    // ── Animasyon karesi üret ─────────────────────────────────────────────────
    private static LedColor[] AnimateFrame(LedColor[] palette, float phase, float energy)
    {
        var frame = new LedColor[6];
        for (int i = 0; i < 6; i++)
        {
            // Her LED için sinüs dalgası parlaklık faktörü
            double offset = (i / 6.0) + phase;
            float  factor = (float)(0.3 + 0.7 * (0.5 + 0.5 * Math.Sin(offset * 2 * Math.PI)));

            // Energy değerine göre parlaklık artır
            factor = factor * (0.4f + energy * 0.6f);
            factor = Math.Clamp(factor, 0f, 1f);

            frame[i] = new LedColor(
                (byte)(palette[i].R * factor),
                (byte)(palette[i].G * factor),
                (byte)(palette[i].B * factor)
            );
        }
        return frame;
    }

    // ── IDisposable ───────────────────────────────────────────────────────────
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _authServer?.Dispose();
        _http.Dispose();
    }
}
