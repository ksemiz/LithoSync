// =============================================================================
//  main.cpp  —  ESP32-C3 Super Mini  |  IoT LED Controller
//
//  Donanım : ESP32-C3 Super Mini
//  LED     : 6x WS2812B @ GPIO 8
//  Kütüphane: FastLED, WiFiManager, ESPAsyncWebServer, ArduinoJson
//
//  Başlangıç akışı:
//    1. Serial başlat
//    2. LedController başlat (FastLED)
//    3. NetworkManager başlat (WiFiManager Captive Portal)
//    4. HttpApiServer başlat (REST API)
//    5. UdpServer başlat (Ambilight)
//    6. OtaManager başlat (GitHub OTA)
//
//  loop() içinde:
//    - LedController.update()  → animasyon ticking
//    - OtaManager.tick()       → periyodik OTA kontrolü
// =============================================================================

#include <Arduino.h>
#include "Config.h"
#include "LedController.h"
#include "NetworkManager.h"
#include "HttpApiServer.h"
#include "UdpServer.h"
#include "OtaManager.h"

// ─── Global nesneler ──────────────────────────────────────────────────────────
LedController  ledCtrl;
NetworkManager netMgr;
OtaManager     otaMgr;
HttpApiServer  httpSrv(ledCtrl, otaMgr, netMgr);
UdpServer      udpSrv(ledCtrl);

// ─── setup() ──────────────────────────────────────────────────────────────────
void setup() {
    Serial.begin(115200);
    delay(1000);  // USB-CDC stabilizasyonu

    Serial.println("\n╔══════════════════════════════════╗");
    Serial.println("║   IoT LED Controller v" CURRENT_VERSION "      ║");
    Serial.println("║   ESP32-C3 Super Mini + WS2812B  ║");
    Serial.println("╚══════════════════════════════════╝\n");

    // 1. LED Başlat (ilk vizüel geri bildirim için)
    ledCtrl.begin();

    // Başlangıç rengi: WiFi bağlantı bekleme sinyali (mavi soluk)
    ledCtrl.setGlobalColor(CRGB(0, 0, 30));

    // 2. Wi-Fi bağlantısı (Captive Portal)
    if (!netMgr.begin()) {
        // begin() başarısız olursa ESP.restart() çağırır
        return;
    }

    // 3. Bağlantı başarılı — yeşil yanıp sön
    for (int i = 0; i < 3; i++) {
        ledCtrl.setGlobalColor(CRGB(0, 50, 0));
        delay(200);
        ledCtrl.setGlobalColor(CRGB::Black);
        delay(200);
    }

    // 4. REST API başlat
    httpSrv.begin();

    // 5. UDP sunucusu başlat
    udpSrv.begin();

    // 6. Başlangıç LED modunu sıfırla (Statik, siyah)
    ledCtrl.setMode(MODE_STATIC);
    ledCtrl.setGlobalColor(CRGB::Black);

    Serial.printf("\n[SYS] Hazır! IP: %s  UDP:%d  HTTP:%d\n",
                  netMgr.getIPAddress().c_str(), UDP_PORT, HTTP_PORT);
    Serial.printf("[SYS] mDNS: http://%s.local\n\n", HOSTNAME);
}

// ─── loop() ───────────────────────────────────────────────────────────────────
void loop() {
    // LED animasyon döngüsü
    ledCtrl.update();

    // Periyodik OTA kontrolü (30 dakika aralıklı)
    otaMgr.tick();

    // ESP-IDF görev zamanlayıcısına nefes aldır
    yield();
}
