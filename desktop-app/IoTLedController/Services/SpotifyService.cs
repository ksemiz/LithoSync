using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace IoTLedController.Services
{
    public class SpotifyService
    {
        private SpotifyClient? _spotify;
        private EmbedIOAuthServer? _server;
        private readonly string _clientId;
        private readonly int _port;
        private readonly HttpClient _httpClient = new();

        public event Action<string>? TrackChanged;
        public event Action<Color>? DominantColorExtracted;
        public event Action<bool>? AuthStatusChanged;

        public bool IsAuthenticated => _spotify != null;

        public SpotifyService(string clientId = "bc8d968a01df4e0a8fdd22a81a63305b", int port = 5543)
        {
            _clientId = clientId;
            _port = port;
        }

        public async Task StartAuthAsync()
        {
            _server = new EmbedIOAuthServer(new Uri($"http://127.0.0.1:{_port}/callback"), _port);
            await _server.Start();

            _server.AuthorizationCodeReceived += async (sender, response) =>
            {
                await _server.Stop();
                var tokenRequest = new AuthorizationCodeTokenRequest(_clientId, "", response.Code, new Uri($"http://127.0.0.1:{_port}/callback"));
                var oauth = new OAuthClient();
                var tokenResponse = await oauth.RequestToken(tokenRequest);
                _spotify = new SpotifyClient(tokenResponse.AccessToken);
                AuthStatusChanged?.Invoke(true);
                await StartPollingAsync();
            };

            var request = new LoginRequest(_server.BaseUri, _clientId, LoginRequest.ResponseType.Code)
            {
                Scope = new[] { Scopes.UserReadCurrentlyPlaying, Scopes.UserReadPlaybackState }
            };

            BrowserUtil.Open(request.ToUri());
        }

        private async Task StartPollingAsync()
        {
            while (_spotify != null)
            {
                try
                {
                    var currentlyPlaying = await _spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                    if (currentlyPlaying?.Item is FullTrack track)
                    {
                        TrackChanged?.Invoke($"{track.Name} - {track.Artists[0].Name}");
                        if (track.Album.Images.Count > 0)
                        {
                            var imageUrl = track.Album.Images[0].Url;
                            var color = await ExtractDominantColorAsync(imageUrl);
                            DominantColorExtracted?.Invoke(color);
                        }
                    }
                }
                catch { }

                await Task.Delay(3000);
            }
        }

        private async Task<Color> ExtractDominantColorAsync(string imageUrl)
        {
            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(imageUrl);
                using var ms = new MemoryStream(bytes);
                using var bitmap = new Bitmap(ms);

                long totalR = 0, totalG = 0, totalB = 0;
                int count = 0;

                for (int x = 0; x < bitmap.Width; x += 5)
                {
                    for (int y = 0; y < bitmap.Height; y += 5)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        totalR += pixel.R;
                        totalG += pixel.G;
                        totalB += pixel.B;
                        count++;
                    }
                }

                if (count == 0) return Color.Purple;

                return Color.FromArgb((byte)(totalR / count), (byte)(totalG / count), (byte)(totalB / count));
            }
            catch
            {
                return Color.Purple;
            }
        }
    }
}
