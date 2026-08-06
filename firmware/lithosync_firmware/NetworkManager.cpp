// =============================================================================
//  NetworkManager.cpp  —  WiFiManager Captive Portal + mDNS + Red Blink Callback
// =============================================================================

#include "NetworkManager.h"
#include "Config.h"
#include "LedController.h"
#include <WiFiManager.h>
#include <ESPmDNS.h>

extern LedController ledCtrl;

NetworkManager::NetworkManager() {
    _wm = new WiFiManager();
}

NetworkManager::~NetworkManager() {
    delete _wm;
}

bool NetworkManager::begin() {
    Serial.println("[NET] NetworkManager başlatılıyor...");

    // 1. Her başlatmada eski Wi-Fi ön belleğini / kayıtlarını temizle
    _wm->resetSettings();
    WiFi.mode(WIFI_STA);
    WiFi.setAutoReconnect(true);
    WiFi.setSleep(false);
    delay(100);

    // 2. Ön tanımlı SSID ve Şifre ile bağlanmayı dene
    String defaultSsid = DEFAULT_WIFI_SSID;
    String defaultPass = DEFAULT_WIFI_PASS;

    bool defaultConnected = false;

    if (defaultSsid.length() > 0 && defaultSsid != "YOUR_WIFI_SSID") {
        Serial.printf("[NET] Ön tanımlı Wi-Fi ağı deneniyor: %s\n", defaultSsid.c_str());
        WiFi.begin(defaultSsid.c_str(), defaultPass.c_str());

        unsigned long startAttempt = millis();
        while (WiFi.status() != WL_CONNECTED && millis() - startAttempt < 10000) {
            delay(500);
            Serial.print(".");
        }
        Serial.println();

        if (WiFi.status() == WL_CONNECTED) {
            defaultConnected = true;
            Serial.println("[NET] Ön tanımlı Wi-Fi ağına başarıyla bağlandı!");
        } else {
            Serial.println("[NET] Ön tanımlı ağa bağlanılamadı. Captive Portal (Hotspot) açılıyor...");
            WiFi.disconnect(true);
            delay(100);
        }
    }

    // 3. Ön tanımlı ağa bağlanılamadıysa (veya placeholder kaldıysa) Captive Portal (Hotspot) aç
    if (!defaultConnected) {
        Serial.printf("[NET] AP Modu Başlatılıyor: %s / %s\n", AP_SSID, AP_PASSWORD);

        _wm->setTitle("LithoSync LED Controller");
        _wm->setConfigPortalTimeout(180);    // 3 dk sonra portal kapanır
        _wm->setConnectTimeout(30);          // Bağlantı denemesi için 30 sn
        _wm->setBreakAfterConfig(true);      // Config alındıktan sonra devam et

        // İnternet yokken / Kurulum modundayken KIRMIZI YANIP SÖNME callback
        _wm->setAPCallback([](WiFiManager* myWiFiManager) {
            Serial.println("[NET] İnternet bağlantısı yok! AP Modunda kırmızı yanıp sönüyor...");
            for (int i = 0; i < 6; i++) {
                ledCtrl.setGlobalColor(CRGB(255, 0, 0));
                delay(300);
                ledCtrl.setGlobalColor(CRGB::Black);
                delay(300);
            }
        });

        // Müşteri için Özel Dark Glassmorphism Captive Portal Tasarımı
        _wm->setCustomHeadElement(R"(
<style>
  body { background: #0D0E1A !important; color: #E8E9F3 !important; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif !important; margin: 0; padding: 20px; }
  h1 { font-size: 22px !important; font-weight: 800 !important; color: #6C63FF !important; text-align: center; margin-bottom: 20px; }
  h2 { font-size: 14px !important; color: #7B7D99 !important; text-align: center; font-weight: normal; margin-bottom: 24px; }
  div.c { background: #13141F !important; border: 1px solid #252640 !important; border-radius: 16px !important; padding: 24px !important; box-shadow: 0 10px 30px rgba(0,0,0,0.5); max-width: 400px; margin: 20px auto; }
  input[type='text'], input[type='password'], select { background: #1A1B2E !important; color: #E8E9F3 !important; border: 1px solid #252640 !important; border-radius: 10px !important; padding: 12px 14px !important; margin-bottom: 12px !important; width: 100% !important; box-sizing: border-box !important; }
  button, input[type='submit'] { background: linear-gradient(135deg, #6C63FF, #4A44B0) !important; color: #FFFFFF !important; border: none !important; border-radius: 10px !important; font-weight: 700 !important; padding: 14px !important; width: 100% !important; cursor: pointer; margin-top: 10px; }
  a { color: #6C63FF !important; text-decoration: none; }
  div.q { border-bottom: 1px solid #252640 !important; padding: 10px 0 !important; }
</style>
)");

        bool connected = _wm->autoConnect(AP_SSID, AP_PASSWORD);

        if (!connected) {
            Serial.println("[NET] Bağlantı başarısız! Kırmızı sinyal ile yeniden başlatılıyor...");
            for (int i = 0; i < 5; i++) {
                ledCtrl.setGlobalColor(CRGB::Red);
                delay(150);
                ledCtrl.setGlobalColor(CRGB::Black);
                delay(150);
            }
            ESP.restart();
            return false;
        }
    }

    Serial.printf("[NET] WiFi bağlandı!\n");
    Serial.printf("[NET] SSID : %s\n", WiFi.SSID().c_str());
    Serial.printf("[NET] IP   : %s\n", WiFi.localIP().toString().c_str());
    Serial.printf("[NET] RSSI : %d dBm\n", WiFi.RSSI());

    if (MDNS.begin(HOSTNAME)) {
        MDNS.addService("http", "tcp", HTTP_PORT);
        Serial.printf("[NET] mDNS: http://%s.local\n", HOSTNAME);
    } else {
        Serial.println("[NET] mDNS başlatılamadı.");
    }

    return true;
}

bool NetworkManager::isConnected() const {
    return WiFi.status() == WL_CONNECTED;
}

String NetworkManager::getIPAddress() const {
    return WiFi.localIP().toString();
}

String NetworkManager::getSSID() const {
    return WiFi.SSID();
}

String NetworkManager::getMACAddress() const {
    return WiFi.macAddress();
}

void NetworkManager::resetSettings() {
    Serial.println("[NET] WiFi ayarları siliniyor, AP moduna geçiliyor...");
    _wm->resetSettings();
    delay(500);
    ESP.restart();
}
