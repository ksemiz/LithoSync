// =============================================================================
//  HttpApiServer.cpp  —  ESPAsyncWebServer REST API + Modern Dark Glass Dashboard
// =============================================================================

#include "HttpApiServer.h"
#include "Config.h"
#include <Arduino.h>

HttpApiServer::HttpApiServer(LedController& led, OtaManager& ota, NetworkManager& net)
    : _server(HTTP_PORT), _led(led), _ota(ota), _net(net)
{}

void HttpApiServer::begin() {
    _setupRoutes();
    _server.begin();
    Serial.printf("[HTTP] Web Server port %d üzerinde başlatıldı.\n", HTTP_PORT);
}

void HttpApiServer::_jsonOk(AsyncWebServerRequest* req, const String& extraFields) {
    String json = "{\"ok\":true";
    if (extraFields.length() > 0) {
        json += "," + extraFields;
    }
    json += "}";
    req->send(200, "application/json", json);
}

void HttpApiServer::_jsonError(AsyncWebServerRequest* req, int code, const String& message) {
    String json = "{\"ok\":false,\"error\":\"" + message + "\"}";
    req->send(code, "application/json", json);
}

HttpApiServer::BodyHandler HttpApiServer::_makeBodyHandler(
    std::function<void(AsyncWebServerRequest*, JsonDocument&)> handler)
{
    return [this, handler](AsyncWebServerRequest* req, uint8_t* data,
                           size_t len, size_t index, size_t total)
    {
        if (index != 0 || len != total) {
            _jsonError(req, 400, "Parçalı paket desteklenmiyor");
            return;
        }

        JsonDocument doc;
        DeserializationError err = deserializeJson(doc, data, len);
        if (err) {
            _jsonError(req, 400, String("JSON ayrıştırma hatası: ") + err.c_str());
            return;
        }

        handler(req, doc);
    };
}

void HttpApiServer::_setupRoutes() {

    // CORS pre-flight
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

    // ── GET / (Lüks Dark Glassmorphism Mobil & Web Kontrol Paneli) ────────────
    _server.on("/", HTTP_GET, [](AsyncWebServerRequest* req) {
        static const char INDEX_HTML[] PROGMEM = R"rawliteral(
<!DOCTYPE html>
<html lang="tr">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
  <title>LithoSync Premium LED</title>
  <style>
    :root { --bg: #0B0C10; --card: rgba(26,27,46,0.7); --accent: #6C63FF; --accent-glow: rgba(108,99,255,0.4); --text: #E8E9F3; --sub: #7B7D99; }
    body { background: var(--bg); color: var(--text); font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 0; padding: 16px; display: flex; flex-direction: column; align-items: center; min-height: 100vh; box-sizing: border-box; }
    .container { width: 100%; max-width: 440px; }
    .card { background: var(--card); backdrop-filter: blur(16px); -webkit-backdrop-filter: blur(16px); border: 1px solid rgba(255,255,255,0.08); border-radius: 20px; padding: 24px; box-shadow: 0 20px 40px rgba(0,0,0,0.6); margin-bottom: 16px; }
    .header { text-align: center; margin-bottom: 24px; }
    .title { font-size: 24px; font-weight: 800; background: linear-gradient(135deg, #9D98FF, #6C63FF); -webkit-background-clip: text; -webkit-text-fill-color: transparent; margin: 0 0 6px 0; letter-spacing: -0.5px; }
    .subtitle { font-size: 13px; color: var(--sub); margin: 0; }
    
    /* LED Görselleştirici */
    .led-strip { display: flex; justify-content: space-between; gap: 8px; margin: 20px 0; background: rgba(0,0,0,0.4); padding: 12px; border-radius: 14px; border: 1px solid rgba(255,255,255,0.05); }
    .led-dot { flex: 1; height: 16px; border-radius: 8px; background: #252640; box-shadow: 0 0 8px rgba(0,0,0,0.5); transition: background 0.3s, box-shadow 0.3s; }
    
    .section-title { font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 1px; color: var(--sub); margin: 20px 0 10px 0; }
    
    /* Mod Seçimi Grid */
    .modes { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .mode-card { background: rgba(255,255,255,0.03); border: 1px solid rgba(255,255,255,0.08); border-radius: 14px; padding: 16px 12px; text-align: center; cursor: pointer; transition: all 0.25s ease; font-weight: 600; font-size: 14px; color: var(--text); }
    .mode-card:active { transform: scale(0.96); }
    .mode-card.active { background: linear-gradient(135deg, rgba(108,99,255,0.25), rgba(74,68,176,0.15)); border-color: var(--accent); box-shadow: 0 0 15px var(--accent-glow); color: #FFF; }
    .mode-icon { font-size: 22px; display: block; margin-bottom: 6px; }
    
    /* Kontroller */
    .control-group { margin-top: 20px; }
    .slider-header { display: flex; justify-content: space-between; font-size: 13px; font-weight: 600; margin-bottom: 8px; }
    input[type=range] { width: 100%; height: 8px; border-radius: 4px; background: #252640; outline: none; accent-color: var(--accent); cursor: pointer; }
    input[type=color] { width: 100%; height: 54px; border: 1px solid rgba(255,255,255,0.1); border-radius: 14px; cursor: pointer; background: #1A1B2E; padding: 4px; box-sizing: border-box; margin-top: 6px; }
    
    /* Wi-Fi Sıfırla Butonu */
    .btn-reset { background: rgba(231,76,60,0.12); border: 1px solid rgba(231,76,60,0.3); color: #E74C3C; border-radius: 12px; padding: 14px; font-weight: 700; width: 100%; cursor: pointer; margin-top: 24px; transition: 0.2s; }
    .btn-reset:active { background: rgba(231,76,60,0.3); }
  </style>
</head>
<body>
  <div class="container">
    <div class="card">
      <div class="header">
        <h1 class="title">✨ LithoSync LED</h1>
        <p class="subtitle">Canlı Kontrol Paneli</p>
      </div>

      <!-- LED Canlı Önizleme Çubuğu -->
      <div class="led-strip" id="strip">
        <div class="led-dot"></div><div class="led-dot"></div><div class="led-dot"></div>
        <div class="led-dot"></div><div class="led-dot"></div><div class="led-dot"></div>
      </div>

      <div class="section-title">Çalışma Modu</div>
      <div class="modes">
        <div class="mode-card" id="m0" onclick="setMode(0)"><span class="mode-icon">🎨</span>Sabit Renk</div>
        <div class="mode-card" id="m1" onclick="setMode(1)"><span class="mode-icon">🌈</span>Gökkuşağı</div>
        <div class="mode-card" id="m2" onclick="setMode(2)"><span class="mode-icon">🫁</span>Nefes / Şimşek</div>
        <div class="mode-card" id="m3" onclick="setMode(3)"><span class="mode-icon">⚡</span>UDP AmbiLight</div>
      </div>

      <div class="control-group">
        <div class="slider-header">
          <span>Parlaklık</span>
          <span id="bval" style="color:var(--accent)">%80</span>
        </div>
        <input type="range" min="0" max="255" id="bright" oninput="updateBrightLabel(this.value)" onchange="setBright(this.value)">
      </div>

      <div class="control-group">
        <div class="slider-header"><span>Global LED Rengi</span></div>
        <input type="color" id="col" value="#6C63FF" onchange="setColor(this.value)">
      </div>

      <button class="btn-reset" onclick="resetWifi()">⚙️ Farklı Wi-Fi'a Bağla (Sıfırla)</button>
    </div>
  </div>

  <script>
    let activeMode = 0;
    function fetchStatus() {
      fetch('/status').then(r=>r.json()).then(d=>{
        if(d.ok){
          activeMode = d.mode;
          document.querySelectorAll('.mode-card').forEach((el,i)=>el.classList.toggle('active', i===d.mode));
          document.getElementById('bright').value = d.brightness;
          document.getElementById('bval').innerText = Math.round(d.brightness/2.55) + '%';
          if(d.leds && d.leds.length===6){
            let dots = document.querySelectorAll('.led-dot');
            d.leds.forEach((c,i)=>{
              dots[i].style.background = `rgb(${c.r},${c.g},${c.b})`;
              dots[i].style.boxShadow = `0 0 10px rgb(${c.r},${c.g},${c.b})`;
            });
          }
        }
      }).catch(()=>{});
    }

    function setMode(m){
      fetch('/setMode',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({mode:m})}).then(()=>fetchStatus());
    }
    function updateBrightLabel(v){ document.getElementById('bval').innerText = Math.round(v/2.55) + '%'; }
    function setBright(b){
      fetch('/setBrightness',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({brightness:parseInt(b)})});
    }
    function setColor(hex){
      let r=parseInt(hex.substr(1,2),16), g=parseInt(hex.substr(3,2),16), b=parseInt(hex.substr(5,2),16);
      fetch('/setColor',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({r:r,g:g,b:b})}).then(()=>fetchStatus());
    }
    function resetWifi(){
      if(confirm("Wi-Fi ayarları silinip cihaz yeniden başlatılacak. Emin misiniz?")){
        fetch('/reset',{method:'POST'}).then(()=>alert("Wi-Fi sıfırlandı! 'IoT-LED-Setup' ağına bağlanabilirsiniz."));
      }
    }

    fetchStatus();
    setInterval(fetchStatus, 3000);
  </script>
</body>
</html>
)rawliteral";
        req->send(200, "text/html", INDEX_HTML);
    });

    // GET /status
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
        req->send(200, "application/json", body);
    });

    // POST /reset (Wi-Fi sıfırlama)
    _server.on("/reset", HTTP_POST, [this](AsyncWebServerRequest* req) {
        _jsonOk(req, "\"message\":\"WiFi reset initiated\"");
        req->onDisconnect([this]() {
            delay(500);
            _net.resetSettings();
        });
    });

    // POST /setMode
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

    // POST /setColor (global)
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

    // POST /setLedColor (bireysel)
    _server.on("/setLedColor", HTTP_POST,
        [](AsyncWebServerRequest*) {},
        nullptr,
        _makeBodyHandler([this](AsyncWebServerRequest* req, JsonDocument& doc) {
            if (!doc["index"].is<int>() || !doc["r"].is<int>() ||
                !doc["g"].is<int>()     || !doc["b"].is<int>()) {
                _jsonError(req, 400, "index, r, g, b alanları gerekli");
                return;
            }
            int idx = doc["index"].as<int>();
            if (idx < 0 || idx >= NUM_LEDS) {
                _jsonError(req, 400, "Geçersiz LED indeksi");
                return;
            }
            CRGB color(doc["r"].as<uint8_t>(),
                       doc["g"].as<uint8_t>(),
                       doc["b"].as<uint8_t>());
            _led.setLedColor((uint8_t)idx, color);
            _jsonOk(req);
        })
    );

    // POST /setBrightness
    _server.on("/setBrightness", HTTP_POST,
        [](AsyncWebServerRequest*) {},
        nullptr,
        _makeBodyHandler([this](AsyncWebServerRequest* req, JsonDocument& doc) {
            if (!doc["brightness"].is<int>()) {
                _jsonError(req, 400, "brightness alanı gerekli");
                return;
            }
            int b = doc["brightness"].as<int>();
            if (b < 0 || b > 255) {
                _jsonError(req, 400, "Parlaklık 0-255 arası olmalı");
                return;
            }
            _led.setBrightness((uint8_t)b);
            _jsonOk(req);
        })
    );

    // GET /checkUpdate
    _server.on("/checkUpdate", HTTP_GET, [this](AsyncWebServerRequest* req) {
        String res = _ota.checkAndUpdate();
        _jsonOk(req, "\"result\":\"" + res + "\"");
    });
}
