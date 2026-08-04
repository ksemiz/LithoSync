#pragma once

// =============================================================================
//  HttpApiServer.h  —  ESPAsyncWebServer tabanlı REST API
//
//  Endpoint'ler:
//    GET  /status          → Mevcut durum JSON
//    POST /setMode         → {"mode": 0-3}
//    POST /setColor        → {"r":255,"g":0,"b":0}          (global)
//    POST /setLedColor     → {"index":0,"r":255,"g":0,"b":0} (bireysel)
//    POST /setBrightness   → {"brightness": 0-255}
//    GET  /checkUpdate     → OTA kontrolünü tetikler
//    POST /reset           → WiFi ayarlarını sil
// =============================================================================

#include <ESPAsyncWebServer.h>
#include "LedController.h"
#include "OtaManager.h"
#include "NetworkManager.h"

class HttpApiServer {
public:
    HttpApiServer(LedController& led, OtaManager& ota, NetworkManager& net);
    void begin();

private:
    AsyncWebServer  _server;
    LedController&  _led;
    OtaManager&     _ota;
    NetworkManager& _net;

    void _setupRoutes();

    // Yardımcı yanıt fonksiyonları
    static void _jsonOk(AsyncWebServerRequest* req,
                        const String& extraFields = "");
    static void _jsonError(AsyncWebServerRequest* req,
                           int code, const String& message);

    // POST body handler tipi
    using BodyHandler = std::function<void(
        AsyncWebServerRequest*, uint8_t*, size_t, size_t, size_t)>;

    BodyHandler _makeBodyHandler(
        std::function<void(AsyncWebServerRequest*, JsonDocument&)> handler);
};
