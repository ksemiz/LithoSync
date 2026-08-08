#pragma once

// =============================================================================
//  NetworkManager.h  —  WiFiManager Captive Portal + mDNS
// =============================================================================

#include <Arduino.h>

// Forward declaration — WiFiManager.h sadece .cpp dosyasında dahil edilir
class WiFiManager;

class NetworkManager {
public:
    NetworkManager();
    ~NetworkManager();

    // WiFi kurulum — Captive Portal ile otomatik bağlantı
    bool begin();

    // Periyodik işlemler (bağlantı kontrolü vb.)
    void tick();

    // ── Durum ─────────────────────────────────────────────────────────────────
    bool   isConnected()  const;
    String getIPAddress() const;
    String getSSID()      const;
    String getMACAddress() const;

    // WiFi ayarlarını sil → AP moduna geç (fabrika ayarları)
    void resetSettings();

private:
    WiFiManager* _wm;
};
