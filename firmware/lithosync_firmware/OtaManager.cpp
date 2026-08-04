// =============================================================================
//  OtaManager.cpp  —  GitHub OTA güncelleme implementasyonu
//
//  Akış:
//    1. OTA_VERSION_URL → version.json indir  {"version":"x.y.z","url":"...bin"}
//    2. Semver karşılaştır → yeni versiyon varsa devam et
//    3. binUrl'den .bin stream et → Update.h ile flash'a yaz
//    4. Başarılıysa ESP.restart()
// =============================================================================

#include "OtaManager.h"
#include "Config.h"
#include <HTTPClient.h>
#include <Update.h>
#include <ArduinoJson.h>
#include <WiFi.h>

OtaManager::OtaManager() : _lastCheck(0), _busy(false) {}

// ─── Periyodik tick ───────────────────────────────────────────────────────────
void OtaManager::tick() {
    if (_busy) return;

    unsigned long now = millis();
    // İlk kontrol: başlangıçtan 30 sn sonra, sonra her OTA_CHECK_INTERVAL'de bir
    if (_lastCheck == 0) {
        if (now < 30000) return;  // İlk 30 sn bekle (bağlantı stabilizasyonu)
    } else {
        if (now - _lastCheck < OTA_CHECK_INTERVAL) return;
    }

    _lastCheck = now;
    Serial.println("[OTA] Periyodik güncelleme kontrolü...");
    String result = checkAndUpdate();
    Serial.printf("[OTA] Sonuç: %s\n", result.c_str());
}

// ─── Manuel / API tetiklemeli kontrol ────────────────────────────────────────
String OtaManager::checkAndUpdate() {
    if (_busy) return "error:busy";
    _busy = true;

    String latestVersion, binUrl;
    if (!_fetchVersionInfo(latestVersion, binUrl)) {
        _busy = false;
        return "error:version_fetch_failed";
    }

    Serial.printf("[OTA] Mevcut: %s — GitHub: %s\n",
                  CURRENT_VERSION, latestVersion.c_str());

    if (!_isNewerVersion(latestVersion, CURRENT_VERSION)) {
        Serial.println("[OTA] Güncel — güncelleme gerekmez.");
        _busy = false;
        return "up_to_date";
    }

    Serial.printf("[OTA] Yeni versiyon bulundu: %s → İndiriliyor...\n",
                  latestVersion.c_str());

    if (!_performUpdate(binUrl)) {
        _busy = false;
        return "error:update_failed";
    }

    // Buraya ulaşılırsa güncelleme başarılı → restart
    Serial.println("[OTA] Güncelleme başarılı! Yeniden başlatılıyor...");
    delay(500);
    ESP.restart();
    return "updated";  // Ulaşılmaz
}

// ─── version.json indir ve parse et ──────────────────────────────────────────
bool OtaManager::_fetchVersionInfo(String& latestVersion, String& binUrl) {
    if (WiFi.status() != WL_CONNECTED) return false;

    HTTPClient http;
    http.begin(OTA_VERSION_URL);
    http.setTimeout(10000);

    int code = http.GET();
    if (code != 200) {
        Serial.printf("[OTA] version.json HTTP hatası: %d\n", code);
        http.end();
        return false;
    }

    String payload = http.getString();
    http.end();

    // JSON parse
    JsonDocument doc;
    DeserializationError err = deserializeJson(doc, payload);
    if (err) {
        Serial.printf("[OTA] JSON parse hatası: %s\n", err.c_str());
        return false;
    }

    latestVersion = doc["version"].as<String>();
    binUrl        = doc["url"].as<String>();

    if (latestVersion.isEmpty() || binUrl.isEmpty()) {
        Serial.println("[OTA] version.json eksik alanlar.");
        return false;
    }
    return true;
}

// ─── Semver karşılaştırması: newVer > currentVer → true ──────────────────────
bool OtaManager::_isNewerVersion(const String& newVer, const String& currentVer) {
    // Basit semver parse: "1.2.3" → [1, 2, 3]
    auto parseVer = [](const String& v, int out[3]) {
        int idx = 0, start = 0;
        for (int i = 0; i <= (int)v.length() && idx < 3; i++) {
            if (i == (int)v.length() || v[i] == '.') {
                out[idx++] = v.substring(start, i).toInt();
                start = i + 1;
            }
        }
    };

    int nv[3] = {0}, cv[3] = {0};
    parseVer(newVer, nv);
    parseVer(currentVer, cv);

    for (int i = 0; i < 3; i++) {
        if (nv[i] > cv[i]) return true;
        if (nv[i] < cv[i]) return false;
    }
    return false;  // Eşit
}

// ─── .bin dosyasını indir ve flash'a yaz ─────────────────────────────────────
bool OtaManager::_performUpdate(const String& binUrl) {
    HTTPClient http;
    http.begin(binUrl);
    http.setTimeout(60000);  // Binary indirme için 60 sn timeout

    int code = http.GET();
    if (code != 200) {
        Serial.printf("[OTA] .bin HTTP hatası: %d\n", code);
        http.end();
        return false;
    }

    int contentLength = http.getSize();
    Serial.printf("[OTA] Binary boyutu: %d bayt\n", contentLength);

    if (contentLength <= 0) {
        Serial.println("[OTA] Geçersiz binary boyutu.");
        http.end();
        return false;
    }

    if (!Update.begin(contentLength)) {
        Serial.printf("[OTA] Update.begin hatası: %s\n",
                      Update.errorString());
        http.end();
        return false;
    }

    WiFiClient* stream = http.getStreamPtr();
    size_t written = Update.writeStream(*stream);
    http.end();

    if (written != (size_t)contentLength) {
        Serial.printf("[OTA] Yazma hatası: beklenen=%d yazılan=%d\n",
                      contentLength, (int)written);
        return false;
    }

    if (!Update.end(true)) {
        Serial.printf("[OTA] Update.end hatası: %s\n", Update.errorString());
        return false;
    }

    return true;
}
