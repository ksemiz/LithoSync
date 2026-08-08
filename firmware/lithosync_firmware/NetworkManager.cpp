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

    // 1. WiFi ayarları — resetSettings() KALDIRILD (her açılışta credentials siliniyordu)
    WiFi.persistent(true);            // Credentials NVS'te kalıcı saklansın
    WiFi.setAutoReconnect(true);      // Kopmada otomatik yeniden bağlansın
    WiFi.mode(WIFI_STA);
    WiFi.setSleep(false);             // WiFi uyku modunu kapat (bağlantı stabilite)

    // TX gücünü ellemeyin, ESP32-C3'te stabilite sorunlarına yol açabilir
    delay(200);

    // 2. Ön tanımlı SSID ve Şifre ile bağlanmayı dene (placeholder değilse)
    String defaultSsid = DEFAULT_WIFI_SSID;
    String defaultPass = DEFAULT_WIFI_PASS;

    bool defaultConnected = false;

    if (defaultSsid.length() > 0 &&
        defaultSsid != "YOUR_WIFI_SSID" &&
        defaultSsid != "") {
        Serial.printf("[NET] Ön tanımlı Wi-Fi ağı deneniyor: %s\n", defaultSsid.c_str());
        WiFi.disconnect(false);   // önceki bağlantıyı temizle ama credentials silme
        delay(100);
        WiFi.begin(defaultSsid.c_str(), defaultPass.c_str());

        // 25sn — zayıf sinyal için ek süre (eski 15sn çok kısaydı)
        unsigned long startAttempt = millis();
        while (WiFi.status() != WL_CONNECTED && millis() - startAttempt < 25000) {
            delay(500);
            Serial.print(".");
        }
        Serial.println();

        if (WiFi.status() == WL_CONNECTED) {
            defaultConnected = true;
            Serial.printf("[NET] Ön tanımlı Wi-Fi ağına başarıyla bağlandı! RSSI: %d dBm\n", WiFi.RSSI());
        } else {
            Serial.printf("[NET] Ön tanımlı ağa bağlanılamadı (durum: %d). Captive Portal açılıyor...\n", WiFi.status());
            WiFi.disconnect(true);
            delay(200);
        }
    } else {

        // Placeholder kaldıysa — WiFiManager'ın NVS'deki kayıtlı bilgileri kullanmasına izin ver
        Serial.println("[NET] Hardcoded SSID yok, WiFiManager kaydedilmiş bilgileri kullanacak.");
    }

    // 3. Ön tanımlı ağa bağlanılamadıysa (veya placeholder kaldıysa) Captive Portal (Hotspot) aç
    if (!defaultConnected) {
        Serial.printf("[NET] AP Modu Başlatılıyor: %s / %s\n", AP_SSID, AP_PASSWORD);

        // !! KRİTİK: ESP32-C3'te AP açıkken ağ taraması yapabilmek için AP+STA ikili modu gerekli
        WiFi.mode(WIFI_AP_STA);
        delay(200);

        // Ön tarama: Ağ listesi önceden taranmazsa WiFiManager "no networks found" gösteriyor
        Serial.println("[NET] Ön tarama yapılıyor...");
        int n = WiFi.scanNetworks(false, true);  // false=blocking, true=hidden de dahil
        Serial.printf("[NET] %d ağ bulundu.\n", n);
        delay(100);

        _wm->setTitle("LithoSync LED Controller");
        _wm->setConfigPortalTimeout(180);    // 3 dk sonra portal kapanır
        _wm->setConnectTimeout(30);          // Bağlantı denemesi için 30 sn
        _wm->setCleanConnect(true);          // BSSID temizle, Wi-Fi Range Extender geçişlerini kolaylaştırır
        _wm->setMinimumSignalQuality(-1);    // Tüm ağları göster, sinyal filtresini devre dışı bırak
        _wm->setShowStaticFields(false);     // Gereksiz teknik alanları gizle
        _wm->setShowDnsFields(false);        // DNS alanını gizle

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

        // Son kullanıcı odaklı Captive Portal tasarımı
        _wm->setCustomHeadElement(R"rawhtml(
<style>
  @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700;800&display=swap');
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    background: linear-gradient(160deg, #0B0C18 0%, #0F1020 100%);
    color: #E8E9F3;
    font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
    min-height: 100vh;
    padding: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
  }
  .ls-brand {
    width: 100%;
    background: rgba(13,14,26,0.95);
    border-bottom: 1px solid rgba(108,99,255,0.3);
    padding: 18px 24px;
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 28px;
  }
  .ls-brand-dot {
    width: 12px; height: 12px;
    border-radius: 50%;
    background: linear-gradient(135deg, #9D98FF, #6C63FF);
    box-shadow: 0 0 12px rgba(108,99,255,0.7);
  }
  .ls-brand-name {
    font-size: 20px;
    font-weight: 800;
    background: linear-gradient(135deg, #9D98FF, #6C63FF);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    letter-spacing: -0.3px;
  }
  .ls-brand-sub { font-size: 12px; color: #7B7D99; margin-left: auto; }
  .ls-card {
    background: rgba(19,20,31,0.9);
    border: 1px solid rgba(255,255,255,0.07);
    border-radius: 20px;
    padding: 28px 24px;
    width: calc(100% - 32px);
    max-width: 400px;
    box-shadow: 0 20px 60px rgba(0,0,0,0.5);
    margin-bottom: 16px;
  }
  .ls-steps { display: flex; flex-direction: column; gap: 14px; margin-bottom: 24px; }
  .ls-step { display: flex; align-items: flex-start; gap: 14px; }
  .ls-step-num {
    width: 30px; height: 30px;
    border-radius: 50%;
    background: linear-gradient(135deg, #6C63FF, #4A44B0);
    display: flex; align-items: center; justify-content: center;
    font-size: 13px; font-weight: 700;
    flex-shrink: 0; color: #fff;
    box-shadow: 0 4px 12px rgba(108,99,255,0.4);
  }
  .ls-step-text { font-size: 14px; color: #C8C9D8; line-height: 1.5; padding-top: 4px; }
  .ls-step-text strong { color: #E8E9F3; }
  .ls-title { font-size: 17px; font-weight: 700; color: #E8E9F3; margin-bottom: 6px; }
  .ls-subtitle { font-size: 13px; color: #7B7D99; margin-bottom: 20px; line-height: 1.5; }
  input[type='text'], input[type='password'], select {
    background: #0E0F1C !important; color: #E8E9F3 !important;
    border: 1.5px solid rgba(255,255,255,0.1) !important;
    border-radius: 12px !important; padding: 14px 16px !important;
    margin-bottom: 14px !important; width: 100% !important;
    font-size: 15px !important; font-family: inherit !important;
    transition: border-color 0.2s !important; outline: none !important;
  }
  input[type='text']:focus, input[type='password']:focus, select:focus {
    border-color: #6C63FF !important;
  }
  select option { background: #13141F; }
  button, input[type='submit'] {
    background: linear-gradient(135deg, #6C63FF 0%, #4A44B0 100%) !important;
    color: #FFFFFF !important; border: none !important;
    border-radius: 12px !important; font-weight: 700 !important;
    font-size: 16px !important; font-family: inherit !important;
    padding: 16px !important; width: 100% !important;
    cursor: pointer !important; margin-top: 4px !important;
    box-shadow: 0 8px 24px rgba(108,99,255,0.4) !important;
  }
  a { color: #6C63FF !important; text-decoration: none; }
  div.c { display: none !important; }
  div.q { border-bottom: 1px solid rgba(255,255,255,0.06) !important; padding: 12px 0 !important; font-size: 14px !important; color: #C8C9D8 !important; }
  div.q b { color: #9D98FF !important; }
  .ls-info {
    background: rgba(108,99,255,0.08);
    border: 1px solid rgba(108,99,255,0.2);
    border-radius: 12px; padding: 14px 16px;
    font-size: 13px; color: #9D98FF; line-height: 1.6; margin-top: 16px;
    display: flex; align-items: flex-start; gap: 10px;
  }
  .ls-info-icon { font-size: 18px; flex-shrink: 0; }
</style>
<script>
  document.addEventListener('DOMContentLoaded', function() {
    var h1 = document.querySelector('h1');
    if (h1) h1.textContent = 'Wi-Fi Kurulumu';
    var h2 = document.querySelector('h2');
    if (h2) h2.textContent = 'Cihazınızı ev ağınıza bağlayın';
    var btn = document.querySelector('input[type="submit"]');
    if (btn) btn.value = 'Bağlantıyı Kaydet';
    var labels = document.querySelectorAll('label');
    labels.forEach(function(l) {
      if (l.textContent.indexOf('SSID') !== -1) l.textContent = 'Wi-Fi Ağınız';
      if (l.textContent.indexOf('Password') !== -1) l.textContent = 'Wi-Fi Şifreniz';
    });
  });
</script>
<div class="ls-brand">
  <div class="ls-brand-dot"></div>
  <div class="ls-brand-name">LithoSync</div>
  <div class="ls-brand-sub">LED Kurulum Asistanı</div>
</div>
<div style="width:calc(100% - 32px);max-width:400px;margin:0 auto 20px;">
  <div class="ls-card">
    <div class="ls-title">📶 Ev Wi-Fi Ağınıza Bağlanın</div>
    <div class="ls-subtitle">LithoSync cihazınız internet bağlantısı gerektirir. Aşağıdan ev/işyeri Wi-Fi ağınızı seçip şifrenizi girin.</div>
    <div class="ls-steps">
      <div class="ls-step">
        <div class="ls-step-num">1</div>
        <div class="ls-step-text">Aşağıdaki listeden <strong>ev Wi-Fi ağınızı</strong> seçin</div>
      </div>
      <div class="ls-step">
        <div class="ls-step-num">2</div>
        <div class="ls-step-text">Wi-Fi <strong>şifrenizi</strong> girin</div>
      </div>
      <div class="ls-step">
        <div class="ls-step-num">3</div>
        <div class="ls-step-text"><strong>"Bağlantıyı Kaydet"</strong> butonuna basın</div>
      </div>
    </div>
    <div class="ls-info">
      <span class="ls-info-icon">✨</span>
      <span>Bağlantı kurulduktan sonra LED'ler <strong style="color:#2ECC71">yeşil</strong> yanıp sönecek ve cihaz hazır olacaktır.</span>
    </div>
  </div>
</div>
)rawhtml");

        bool connected = _wm->autoConnect(AP_SSID, AP_PASSWORD);

        if (!connected) {
            Serial.println("[NET] Bağlantı başarısız! Yeniden başlatılıyor...");
            for (int i = 0; i < 5; i++) {
                ledCtrl.setGlobalColor(CRGB::Red);
                delay(150);
                ledCtrl.setGlobalColor(CRGB::Black);
                delay(150);
            }
            ESP.restart();
            return false;
        }

        // Bağlantı kuruldu — AP modunu temizle, sadece STA modunda kal
        WiFi.softAPdisconnect(true);
        WiFi.mode(WIFI_STA);
        delay(100);

        // Başarılı bağlantı — YEŞİL LED sinyali ver
        for (int i = 0; i < 3; i++) {
            ledCtrl.setGlobalColor(CRGB(0, 200, 0));
            delay(150);
            ledCtrl.setGlobalColor(CRGB::Black);
            delay(150);
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

void NetworkManager::tick() {
    // Otomatik yeniden bağlanma ESP32'nin kendi donanım/SDK seviyesinde (WiFi.setAutoReconnect(true)) yapılıyor.
    // Burada manuel olarak WiFi.disconnect() çağırmak, Captive Portal'ı bozabilir veya kopmaları tetikleyebilir.
}
