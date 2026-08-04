#pragma once

// =============================================================================
//  HttpApiServer.h  —  ESPAsyncWebServer tabanlı REST API
// =============================================================================

#include <Arduino.h>
#include <functional>
#include <ArduinoJson.h>
#include <ESPAsyncWebServer.h>
#include "LedController.h"
#include "OtaManager.h"
#include "NetworkManager.h"

class HttpApiServer {
public:
    HttpApiServer(LedController& led, OtaManager& ota, NetworkManager& net);
    void begin();

    // POST body handler tipi
    using BodyHandler = std::function<void(
        AsyncWebServerRequest*, uint8_t*, size_t, size_t, size_t)>;

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

    BodyHandler _makeBodyHandler(
        std::function<void(AsyncWebServerRequest*, JsonDocument&)> handler);
};
