// =============================================================================
//  main.cpp  —  ESP32-C3 Super Mini  |  IoT LED Controller
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

static unsigned long lastRedBlink = 0;
static bool redState = false;

void setup() {
    Serial.begin(115200);
    delay(1000);

    Serial.println("\n╔══════════════════════════════════╗");
    Serial.println("║   IoT LED Controller v" CURRENT_VERSION "      ║");
    Serial.println("║   ESP32-C3 Super Mini + WS2812B  ║");
    Serial.println("╚══════════════════════════════════╝\n");

    // 1. LED Başlat
    ledCtrl.begin();

    // 2. Wi-Fi bağlantısı (Bağlantı yoksa kırmızı yanıp söner)
    if (!netMgr.begin()) {
        return;
    }

    // 3. Bağlantı başarılı — yeşil sinyal
    for (int i = 0; i < 3; i++) {
        ledCtrl.setGlobalColor(CRGB(0, 80, 0));
        delay(150);
        ledCtrl.setGlobalColor(CRGB::Black);
        delay(150);
    }

    // 4. REST API başlat
    httpSrv.begin();

    // 5. UDP sunucusu başlat
    udpSrv.begin();

    // 6. Başlangıç LED modu
    ledCtrl.setMode(MODE_STATIC);
    ledCtrl.setGlobalColor(CRGB::Black);

    Serial.printf("\n[SYS] Hazır! IP: %s  UDP:%d  HTTP:%d\n",
                  netMgr.getIPAddress().c_str(), UDP_PORT, HTTP_PORT);
    Serial.printf("[SYS] mDNS: http://%s.local\n\n", HOSTNAME);
}

void loop() {
    // İnternet/Wi-Fi koparsa KIRMIZI YANIP SÖN
    if (!netMgr.isConnected()) {
        if (millis() - lastRedBlink >= 400) {
            lastRedBlink = millis();
            redState = !redState;
            ledCtrl.setGlobalColor(redState ? CRGB(255, 0, 0) : CRGB::Black);
        }
        yield();
        return;
    }

    // LED animasyon döngüsü
    ledCtrl.update();

    // Periyodik OTA kontrolü
    otaMgr.tick();

    yield();
}
