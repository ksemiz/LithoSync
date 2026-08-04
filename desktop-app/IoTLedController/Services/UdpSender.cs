using System.Net.Sockets;

namespace IoTLedController.Services;

// =============================================================================
//  UdpSender.cs  —  ESP32'ye UDP üzerinden LED renk verisi gönderici
//
//  Protokol: 18 bayt (6 LED × 3 channel: R, G, B)
// =============================================================================

public sealed class UdpSender : IDisposable
{
    private UdpClient?  _client;
    private string      _host;
    private int         _port;
    private bool        _disposed;
    private long        _sentPackets;
    private long        _failedPackets;

    public long SentPackets   => _sentPackets;
    public long FailedPackets => _failedPackets;
    public bool IsConnected   { get; private set; }

    public UdpSender(string host = "192.168.1.100", int port = 4210)
    {
        _host = host;
        _port = port;
    }

    // ── Bağlantı ──────────────────────────────────────────────────────────────
    public void Connect(string host, int port)
    {
        _host = host;
        _port = port;
        Reconnect();
    }

    public void Reconnect()
    {
        _client?.Close();
        _client?.Dispose();

        try
        {
            _client = new UdpClient();
            _client.Connect(_host, _port);
            IsConnected = true;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            System.Diagnostics.Debug.WriteLine($"[UDP] Bağlantı hatası: {ex.Message}");
        }
    }

    // ── Tek paket gönder ──────────────────────────────────────────────────────
    public bool Send(byte[] data)
    {
        if (_client is null || !IsConnected) return false;

        try
        {
            _client.Send(data, data.Length);
            Interlocked.Increment(ref _sentPackets);
            return true;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedPackets);
            System.Diagnostics.Debug.WriteLine($"[UDP] Gönderim hatası: {ex.Message}");
            IsConnected = false;
            return false;
        }
    }

    // ── LedColor dizisini 18 baytlık pakete dönüştür ve gönder ──────────────
    public bool SendColors(Models.LedColor[] colors)
    {
        if (colors.Length != 6) return false;

        var packet = new byte[18];
        for (int i = 0; i < 6; i++)
        {
            packet[i * 3]     = colors[i].R;
            packet[i * 3 + 1] = colors[i].G;
            packet[i * 3 + 2] = colors[i].B;
        }
        return Send(packet);
    }

    // ── Async gönderim ────────────────────────────────────────────────────────
    public async Task<bool> SendAsync(byte[] data)
    {
        if (_client is null || !IsConnected) return false;

        try
        {
            await _client.SendAsync(data, data.Length);
            Interlocked.Increment(ref _sentPackets);
            return true;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedPackets);
            System.Diagnostics.Debug.WriteLine($"[UDP] Async gönderim hatası: {ex.Message}");
            IsConnected = false;
            return false;
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsConnected = false;
        _client?.Close();
        _client?.Dispose();
    }
}
