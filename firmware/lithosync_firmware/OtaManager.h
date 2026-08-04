#pragma once

// =============================================================================
//  OtaManager.h  —  GitHub Raw URL üzerinden OTA (Over-The-Air) güncelleme
// =============================================================================

#include <Arduino.h>

class OtaManager {
public:
    OtaManager();

    // Periyodik OTA kontrolü — loop() içinde zaman koşullu çağrılır
    void tick();

    // Manuel tetikleme (REST API isteğiyle çağrılabilir)
    // Döner: "up_to_date" | "updated" | "error:<mesaj>"
    String checkAndUpdate();

    bool isBusy() const { return _busy; }

private:
    unsigned long _lastCheck;
    bool          _busy;

    // GitHub'dan version.json indir ve parse et
    // Döner: true → yeni versiyon var, binUrl doldurulur
    bool _fetchVersionInfo(String& latestVersion, String& binUrl);

    // Semver karşılaştırması: döner true → newVer > currentVer
    bool _isNewerVersion(const String& newVer, const String& currentVer);

    // URL'den .bin indir ve Update.h ile flash'a yaz
    bool _performUpdate(const String& binUrl);
};
