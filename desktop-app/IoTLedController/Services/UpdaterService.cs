using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace IoTLedController.Services;

// ─── GitHub Release JSON modeli ──────────────────────────────────────────────
public record GitHubRelease(
    [property: JsonPropertyName("tag_name")]  string TagName,
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("body")]      string Body,
    [property: JsonPropertyName("assets")]    GitHubAsset[] Assets
);

public record GitHubAsset(
    [property: JsonPropertyName("name")]                 string Name,
    [property: JsonPropertyName("browser_download_url")] string DownloadUrl,
    [property: JsonPropertyName("size")]                 long   Size
);

// ─────────────────────────────────────────────────────────────────────────────
public class UpdaterService
{
    // !! DEĞİŞTİR: GitHub kullanıcı adı ve repo adı !!
    private const string GH_OWNER = "ksemiz";
    private const string GH_REPO  = "LithoSync";

    // GitHub API URL'si — releases/latest otomatik en son release'i döndürür
    private static readonly string ApiUrl =
        $"https://api.github.com/repos/{GH_OWNER}/{GH_REPO}/releases/latest";

    // Mevcut uygulamanın versiyonu (csproj'dan <Version> okunur)
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion?.Split('+')[0]   // git hash kısmını at
                ?? "0.0.0";

    private readonly HttpClient _http;

    public UpdaterService()
    {
        _http = new HttpClient();
        // GitHub API için User-Agent zorunlu
        _http.DefaultRequestHeaders.UserAgent
             .ParseAdd($"LithoSync/{CurrentVersion}");
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    // ── Versiyon Kontrolü ────────────────────────────────────────────────────
    /// <returns>Yeni release varsa GitHubRelease nesnesi, yoksa null</returns>
    public async Task<GitHubRelease?> CheckForUpdateAsync()
    {
        try
        {
            var release = await _http.GetFromJsonAsync<GitHubRelease>(ApiUrl);
            if (release is null) return null;

            // Tag örneği: "v1.2.0" veya "1.2.0"
            var latestStr  = release.TagName.TrimStart('v');
            var currentStr = CurrentVersion.TrimStart('v');

            if (Version.TryParse(latestStr,  out var latest) &&
                Version.TryParse(currentStr, out var current) &&
                latest > current)
            {
                return release;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UPDATER] Versiyon kontrol hatası: {ex.Message}");
        }
        return null;
    }

    // ── İndirme + Kendini Güncelleme ─────────────────────────────────────────
    /// <summary>
    /// Yeni EXE'yi indirir ve bir bat dosyası aracılığıyla kendini değiştirip yeniden başlatır.
    /// WPF uygulaması çalışırken kendi EXE'sini değiştiremez — bat geçici çözüm.
    /// </summary>
    public async Task DownloadAndUpdateAsync(GitHubRelease release,
        IProgress<(int Percent, string Status)>? progress = null)
    {
        // EXE asset'ini bul (.exe ile biten ilk dosya)
        var asset = Array.Find(release.Assets,
            a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            MessageBox.Show(
                "Bu sürüm için indirilebilir EXE bulunamadı.\n" +
                "Lütfen GitHub sayfasından manuel olarak indirin.",
                "Güncelleme Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var currentExe = Process.GetCurrentProcess().MainModule?.FileName
                         ?? Environment.ProcessPath
                         ?? throw new InvalidOperationException("EXE yolu alınamadı.");

        var tempDir     = Path.Combine(Path.GetTempPath(), "LithoSyncUpdate");
        var tempExe     = Path.Combine(tempDir, asset.Name);
        var updaterBat  = Path.Combine(tempDir, "updater.bat");

        Directory.CreateDirectory(tempDir);

        // ─ İndirme ─────────────────────────────────────────────────────────
        progress?.Report((0, "İndirme başlıyor..."));
        using var resp = await _http.GetAsync(asset.DownloadUrl,
                             HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        var total     = asset.Size;
        var received  = 0L;
        var buffer    = new byte[81920];

        await using var src  = await resp.Content.ReadAsStreamAsync();
        await using var dest = File.Create(tempExe);

        int read;
        while ((read = await src.ReadAsync(buffer)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, read));
            received += read;
            if (total > 0)
            {
                var pct = (int)(received * 100 / total);
                progress?.Report((pct, $"İndiriliyor... %{pct}"));
            }
        }

        progress?.Report((100, "Güncelleme hazırlanıyor..."));

        // ─ Bat dosyasını oluştur ────────────────────────────────────────────
        // Bu bat, uygulamanın kapanmasını bekler, EXE'yi değiştirir, yeniden başlatır.
        var pid = Environment.ProcessId;
        var bat = $"""
@echo off
echo [UPDATER] Uygulama kapanıyor bekleniyor (PID {pid})...
:wait
tasklist /FI "PID eq {pid}" 2>NUL | find /I "{pid}" >NUL
if not errorlevel 1 (
    timeout /t 1 /nobreak >NUL
    goto wait
)
echo [UPDATER] EXE değiştiriliyor...
move /Y "{tempExe}" "{currentExe}"
echo [UPDATER] Yeniden başlatılıyor...
start "" "{currentExe}"
del "%~f0"
""";
        await File.WriteAllTextAsync(updaterBat, bat);

        // ─ Bat'ı arka planda başlat, uygulamayı kapat ──────────────────────
        Process.Start(new ProcessStartInfo
        {
            FileName        = "cmd.exe",
            Arguments       = $"/c \"{updaterBat}\"",
            CreateNoWindow  = true,
            UseShellExecute = false,
        });

        Application.Current.Shutdown();
    }
}
