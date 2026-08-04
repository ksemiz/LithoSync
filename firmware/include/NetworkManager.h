#pragma once

// =============================================================================
//  NetworkManager.h  —  WiFiManager Captive Portal + mDNS
// =============================================================================

#include <WiFiManager.h>

class NetworkManager {
public:
    NetworkManager();

    // WiFi kurulum — Captive Portal ile otomatik bağlantı
    // Döner true: bağlandı, false: başarısız (ESP restart edilir)
    bool begin();

    // ── Durum ─────────────────────────────────────────────────────────────────
    bool   isConnected()  const;
    String getIPAddress() const;
    String getSSID()      const;
    String getMACAddress() const;

    // WiFi ayarlarını sil → AP moduna geç (fabrika ayarları)
    void resetSettings();

private:
    WiFiManager _wm;
};
