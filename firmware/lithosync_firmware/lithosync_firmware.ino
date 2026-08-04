// =============================================================================
//  lithosync_firmware.ino  —  ESP32-C3 Super Mini | LithoSync Firmware
//  Arduino IDE Uyumlu Sketch Dosyası
//
//  Donanım  : ESP32-C3 Super Mini
//  LED      : 6x WS2812B @ GPIO 8
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
    Serial.println("║   LithoSync LED Controller v" CURRENT_VERSION "  ║");
    Serial.println("║   ESP32-C3 Super Mini + WS2812B  ║");
    Serial.println("╚══════════════════════════════════╝\n");

    // 1. LED Başlat
    ledCtrl.begin();
    ledCtrl.setGlobalColor(CRGB(0, 0, 30)); // Mavi = WiFi bekleniyor

    // 2. Wi-Fi bağlantısı (Captive Portal)
    if (!netMgr.begin()) {
        return;
    }

    // 3. Bağlantı başarılı — 3 kez yeşil yanıp sön
    for (int i = 0; i < 3; i++) {
        ledCtrl.setGlobalColor(CRGB(0, 50, 0));
        delay(200);
        ledCtrl.setGlobalColor(CRGB::Black);
        delay(200);
    }

    // 4. REST API & UDP başlat
    httpSrv.begin();
    udpSrv.begin();

    // 5. Başlangıç modunu sıfırla
    ledCtrl.setMode(MODE_STATIC);
    ledCtrl.setGlobalColor(CRGB::Black);

    Serial.printf("\n[SYS] Hazır! IP: %s  UDP:%d  HTTP:%d\n",
                  netMgr.getIPAddress().c_str(), UDP_PORT, HTTP_PORT);
    Serial.printf("[SYS] mDNS: http://%s.local\n\n", HOSTNAME);
}

// ─── loop() ───────────────────────────────────────────────────────────────────
void loop() {
    ledCtrl.update();
    otaMgr.tick();
    yield();
}
