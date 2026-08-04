// =============================================================================
//  NetworkManager.cpp  —  WiFiManager Captive Portal + mDNS implementasyonu
// =============================================================================

#include "NetworkManager.h"
#include "Config.h"
#include <Arduino.h>
#include <ESPmDNS.h>

NetworkManager::NetworkManager() {}

bool NetworkManager::begin() {
    Serial.println("[NET] WiFiManager başlatılıyor...");
    Serial.printf("[NET] AP: %s / %s\n", AP_SSID, AP_PASSWORD);

    // WiFiManager yapılandırması
    _wm.setTitle("IoT LED Controller");
    _wm.setConfigPortalTimeout(180);    // 3 dk sonra portal kapanır
    _wm.setConnectTimeout(30);          // Bağlantı denemesi için 30 sn
    _wm.setBreakAfterConfig(true);      // Config alındıktan sonra devam et

    // Daha önce kayıtlı ağa bağlanmaya çalış;
    // başarısız olursa Captive Portal aç
    bool connected = _wm.autoConnect(AP_SSID, AP_PASSWORD);

    if (!connected) {
        Serial.println("[NET] Bağlantı başarısız! ESP yeniden başlatılıyor...");
        delay(3000);
        ESP.restart();
        return false;
    }

    Serial.printf("[NET] WiFi bağlandı!\n");
    Serial.printf("[NET] SSID : %s\n", WiFi.SSID().c_str());
    Serial.printf("[NET] IP   : %s\n", WiFi.localIP().toString().c_str());
    Serial.printf("[NET] RSSI : %d dBm\n", WiFi.RSSI());

    // mDNS kaydı → http://iot-led.local erişimi
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
    Serial.println("[NET] WiFi ayarları siliниyor, AP moduna geçiliyor...");
    _wm.resetSettings();
    delay(500);
    ESP.restart();
}
