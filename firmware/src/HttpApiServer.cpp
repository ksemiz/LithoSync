// =============================================================================
//  HttpApiServer.cpp  —  REST API endpoint implementasyonları
// =============================================================================

#include "HttpApiServer.h"
#include "Config.h"
#include <ArduinoJson.h>
#include <Arduino.h>

// ─── CORS + JSON content-type yardımcısı ─────────────────────────────────────
static AsyncWebServerResponse* _makeJsonResponse(AsyncWebServerRequest* req,
                                                  const String& body,
                                                  int code = 200) {
    auto* resp = req->beginResponse(code, "application/json", body);
    resp->addHeader("Access-Control-Allow-Origin", "*");
    resp->addHeader("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
    resp->addHeader("Access-Control-Allow-Headers", "Content-Type");
    return resp;
}

// ─── Kurucu ───────────────────────────────────────────────────────────────────
HttpApiServer::HttpApiServer(LedController& led, OtaManager& ota, NetworkManager& net)
    : _server(HTTP_PORT), _led(led), _ota(ota), _net(net)
{}

void HttpApiServer::begin() {
    _setupRoutes();
    _server.begin();
    Serial.printf("[HTTP] REST API sunucusu port %d'de başlatıldı.\n", HTTP_PORT);
}

// ─── Yardımcılar ──────────────────────────────────────────────────────────────
void HttpApiServer::_jsonOk(AsyncWebServerRequest* req, const String& extraFields) {
    String body = "{\"ok\":true";
    if (!extraFields.isEmpty()) body += "," + extraFields;
    body += "}";
    req->send(_makeJsonResponse(req, body));
}

void HttpApiServer::_jsonError(AsyncWebServerRequest* req,
                               int code, const String& message) {
    String body = "{\"ok\":false,\"error\":\"" + message + "\"}";
    req->send(_makeJsonResponse(req, body, code));
}

// ─── Body handler factory ─────────────────────────────────────────────────────
HttpApiServer::BodyHandler HttpApiServer::_makeBodyHandler(
    std::function<void(AsyncWebServerRequest*, JsonDocument&)> handler)
{
    return [handler](AsyncWebServerRequest* req,
                     uint8_t* data, size_t len,
                     size_t /*index*/, size_t /*total*/) {
        JsonDocument doc;
        DeserializationError err = deserializeJson(doc, data, len);
        if (err) {
            _jsonError(req, 400, String("JSON parse hatası: ") + err.c_str());
            return;
        }
        handler(req, doc);
    };
}

// =============================================================================
//  Route tanımları
// =============================================================================
void HttpApiServer::_setupRoutes() {

    // ── OPTIONS (CORS pre-flight) ─────────────────────────────────────────────
    _server.onNotFound([](AsyncWebServerRequest* req) {
        if (req->method() == HTTP_OPTIONS) {
            auto* resp = req->beginResponse(204);
            resp->addHeader("Access-Control-Allow-Origin", "*");
            resp->addHeader("Access-Control-Allow-Methods", "GET,POST,OPTIONS");
            resp->addHeader("Access-Control-Allow-Headers", "Content-Type");
            req->send(resp);
        } else {
            req->send(404, "application/json", "{\"ok\":false,\"error\":\"Not found\"}");
        }
    });

    // ── GET / (Mobile & Web Dashboard) ────────────────────────────────────────
    _server.on("/", HTTP_GET, [](AsyncWebServerRequest* req) {
        static const char INDEX_HTML[] PROGMEM = R"rawliteral(
<!DOCTYPE html>
<html lang="tr">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>LithoSync LED Controller</title>
  <style>
    body { background: #0D0E1A; color: #E8E9F3; font-family: system-ui, -apple-system, sans-serif; margin: 0; padding: 20px; display: flex; flex-direction: column; align-items: center; }
    .card { background: #13141F; border: 1px solid #252640; border-radius: 16px; padding: 24px; width: 100%; max-width: 420px; box-shadow: 0 10px 30px rgba(0,0,0,0.5); box-sizing: border-box; }
    h1 { font-size: 20px; font-weight: 800; color: #6C63FF; text-align: center; margin: 0 0 8px 0; }
    p.sub { font-size: 12px; color: #7B7D99; text-align: center; margin: 0 0 20px 0; }
    .modes { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-bottom: 20px; }
    .mode-btn { background: #1A1B2E; border: 1px solid #252640; color: #E8E9F3; padding: 14px; border-radius: 12px; font-weight: 600; cursor: pointer; transition: 0.2s; }
    .mode-btn:hover { border-color: #6C63FF; }
    label { font-size: 13px; font-weight: 600; color: #7B7D99; display: block; margin-top: 14px; margin-bottom: 6px; }
    input[type=range] { width: 100%; margin: 8px 0; accent-color: #6C63FF; }
    input[type=color] { width: 100%; height: 50px; border: 1px solid #252640; border-radius: 12px; cursor: pointer; background: #1A1B2E; padding: 4px; box-sizing: border-box; }
  </style>
</head>
<body>
  <div class="card">
    <h1>💡 LithoSync LED</h1>
    <p class="sub">Mobil & Web Kontrol Paneli</p>
    <div class="modes">
      <button class="mode-btn" onclick="setMode(0)">🎨 Sabit Renk</button>
      <button class="mode-btn" onclick="setMode(1)">🌈 Gökkuşağı</button>
      <button class="mode-btn" onclick="setMode(2)">🫁 Nefes</button>
      <button class="mode-btn" onclick="setMode(3)">⚡ UDP Ambi</button>
    </div>
    <label>Parlaklık Seviyesi</label>
    <input type="range" min="0" max="255" id="bright" onchange="setBright(this.value)">
    <label>Global LED Rengi</label>
    <input type="color" id="col" value="#6C63FF" onchange="setColor(this.value)">
  </div>
  <script>
    function setMode(m){ fetch('/setMode',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({mode:m})}); }
    function setBright(b){ fetch('/setBrightness',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({brightness:parseInt(b)})}); }
    function setColor(hex){
      let r=parseInt(hex.substr(1,2),16), g=parseInt(hex.substr(3,2),16), b=parseInt(hex.substr(5,2),16);
      fetch('/setColor',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({r:r,g:g,b:b})});
    }
  </script>
</body>
</html>
)rawliteral";
        req->send(200, "text/html", INDEX_HTML);
    });

    // ── GET /status ───────────────────────────────────────────────────────────
    _server.on("/status", HTTP_GET, [this](AsyncWebServerRequest* req) {
        String ledsJson = "[";
        for (int i = 0; i < NUM_LEDS; i++) {
            CRGB c = _led.getLedColor(i);
            if (i > 0) ledsJson += ",";
            ledsJson += "{\"r\":" + String(c.r) +
                        ",\"g\":" + String(c.g) +
                        ",\"b\":" + String(c.b) + "}";
        }
        ledsJson += "]";

        String body = String("{\"ok\":true")
            + ",\"version\":\""   + String(CURRENT_VERSION) + "\""
            + ",\"mode\":"        + String(_led.getMode())
            + ",\"brightness\":"  + String(_led.getBrightness())
            + ",\"ip\":\""        + _net.getIPAddress() + "\""
            + ",\"ssid\":\""      + _net.getSSID() + "\""
            + ",\"mac\":\""       + _net.getMACAddress() + "\""
            + ",\"uptime\":"      + String(millis() / 1000)
            + ",\"leds\":"        + ledsJson +
            "}";
        req->send(_makeJsonResponse(req, body));
    });

    // ── POST /setMode ─────────────────────────────────────────────────────────
    _server.on("/setMode", HTTP_POST,
        [](AsyncWebServerRequest*) {},
        nullptr,
        _makeBodyHandler([this](AsyncWebServerRequest* req, JsonDocument& doc) {
            if (!doc["mode"].is<int>()) {
                _jsonError(req, 400, "mode alanı gerekli (0-3)");
                return;
            }
            int mode = doc["mode"].as<int>();
            if (mode < 0 || mode > MODE_UDP) {
                _jsonError(req, 400, "Geçersiz mod (0-3 arası olmalı)");
                return;
            }
            _led.setMode((uint8_t)mode);
            _jsonOk(req, "\"mode\":" + String(mode));
        })
    );

    // ── POST /setColor (global) ───────────────────────────────────────────────
    _server.on("/setColor", HTTP_POST,
        [](AsyncWebServerRequest*) {},
        nullptr,
        _makeBodyHandler([this](AsyncWebServerRequest* req, JsonDocument& doc) {
            if (!doc["r"].is<int>() || !doc["g"].is<int>() || !doc["b"].is<int>()) {
                _jsonError(req, 400, "r, g, b alanları gerekli");
                return;
            }
            CRGB color(doc["r"].as<uint8_t>(),
                       doc["g"].as<uint8_t>(),
                       doc["b"].as<uint8_t>());
            _led.setGlobalColor(color);
            _jsonOk(req);
        })
    );

    // ── POST /setLedColor (bireysel) ──────────────────────────────────────────
    _server.on("/setLedColor", HTTP_POST,
        [](AsyncWebServerRequest*) {},
        nullptr,
        _makeBodyHandler([this](AsyncWebServerRequest* req, JsonDocument& doc) {
            if (!doc["index"].is<int>()) {
                _jsonError(req, 400, "index alanı gerekli (0-5)");
                return;
            }
            int idx = doc["index"].as<int>();
            if (idx < 0 || idx >= NUM_LEDS) {
                _jsonError(req, 400, "Geçersiz LED index (0-" +
                                     String(NUM_LEDS - 1) + ")");
                return;
            }
            CRGB color(doc["r"].as<uint8_t>(),
                       doc["g"].as<uint8_t>(),
                       doc["b"].as<uint8_t>());
            _led.setLedColor((uint8_t)idx, color);
            _jsonOk(req, "\"index\":" + String(idx));
        })
    );

    // ── POST /setBrightness ───────────────────────────────────────────────────
    _server.on("/setBrightness", HTTP_POST,
        [](AsyncWebServerRequest*) {},
        nullptr,
        _makeBodyHandler([this](AsyncWebServerRequest* req, JsonDocument& doc) {
            if (!doc["brightness"].is<int>()) {
                _jsonError(req, 400, "brightness alanı gerekli (0-255)");
                return;
            }
            int b = doc["brightness"].as<int>();
            b = constrain(b, 0, 255);
            _led.setBrightness((uint8_t)b);
            _jsonOk(req, "\"brightness\":" + String(b));
        })
    );

    // ── GET /checkUpdate ──────────────────────────────────────────────────────
    _server.on("/checkUpdate", HTTP_GET, [this](AsyncWebServerRequest* req) {
        if (_ota.isBusy()) {
            _jsonError(req, 503, "OTA güncelleme zaten devam ediyor");
            return;
        }
        // Yanıtı hemen gönder, OTA arka planda çalışacak
        _jsonOk(req, "\"message\":\"OTA kontrolü başlatıldı\"");
        // Not: OTA async değil, restart olacak — bu endpoint başlangıç sinyali
        String result = _ota.checkAndUpdate();
        Serial.printf("[OTA] Sonuç: %s\n", result.c_str());
    });

    // ── POST /reset ───────────────────────────────────────────────────────────
    _server.on("/reset", HTTP_POST,
        [this](AsyncWebServerRequest* req) {
            _jsonOk(req, "\"message\":\"WiFi ayarları siliniyor, yeniden başlatılıyor\"");
            delay(500);
            _net.resetSettings();
        }
    );

    Serial.println("[HTTP] Tüm route'lar tanımlandı.");
}
