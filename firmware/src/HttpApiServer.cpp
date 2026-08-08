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
  <title>LithoSync Premium</title>
  <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;600;800&display=swap" rel="stylesheet">
  <style>
    :root { 
      --bg: #05050A; 
      --surface: rgba(20,20,30,0.6); 
      --accent: #6C63FF; 
      --accent-glow: rgba(108,99,255,0.6); 
      --text: #F0F0F5; 
      --sub: #9E9EB5;
      --border: rgba(255,255,255,0.05);
    }
    body { 
      margin: 0; padding: 0; 
      background-color: var(--bg); 
      color: var(--text); 
      font-family: 'Outfit', sans-serif; 
      display: flex; justify-content: center; 
      min-height: 100vh; overflow-x: hidden;
      -webkit-font-smoothing: antialiased;
    }
    
    /* Background Orbs */
    .orb { position: fixed; border-radius: 50%; filter: blur(80px); opacity: 0.5; z-index: -1; animation: float 15s ease-in-out infinite alternate; }
    .orb-1 { width: 300px; height: 300px; background: #6C63FF; top: -100px; left: -100px; }
    .orb-2 { width: 250px; height: 250px; background: #FF3366; bottom: -50px; right: -50px; animation-delay: -5s; }
    @keyframes float { 0% { transform: translate(0, 0) scale(1); } 100% { transform: translate(30px, 50px) scale(1.1); } }

    .app-container {
      width: 100%; max-width: 480px; 
      padding: 30px 20px; box-sizing: border-box;
      display: flex; flex-direction: column; gap: 24px;
    }

    .header { text-align: center; margin-bottom: 10px; }
    .header h1 { 
      font-size: 32px; font-weight: 800; margin: 0 0 4px 0;
      background: linear-gradient(135deg, #FFF, #9D98FF);
      -webkit-background-clip: text; -webkit-text-fill-color: transparent;
      letter-spacing: -1px;
    }
    .header p { color: var(--sub); font-size: 14px; margin: 0; font-weight: 300; }

    /* Glass Panels */
    .panel {
      background: var(--surface);
      backdrop-filter: blur(20px); -webkit-backdrop-filter: blur(20px);
      border: 1px solid var(--border);
      border-radius: 24px;
      padding: 24px;
      box-shadow: 0 30px 60px rgba(0,0,0,0.4), inset 0 1px 0 rgba(255,255,255,0.1);
    }
    
    .panel-title { font-size: 13px; text-transform: uppercase; letter-spacing: 1.5px; color: var(--sub); font-weight: 600; margin: 0 0 16px 0; }

    /* LED Live View */
    .led-bar { 
      display: flex; justify-content: space-between; gap: 6px; 
      background: rgba(0,0,0,0.5); padding: 10px; 
      border-radius: 16px; border: 1px solid var(--border);
      box-shadow: inset 0 4px 10px rgba(0,0,0,0.5);
    }
    .led { 
      flex: 1; height: 12px; border-radius: 6px; background: #1A1A24;
      transition: all 0.3s ease; box-shadow: 0 0 0 transparent;
    }

    /* Grid Modes */
    .mode-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .mode-btn {
      background: rgba(255,255,255,0.03); border: 1px solid var(--border);
      border-radius: 16px; padding: 18px 12px; 
      text-align: center; color: var(--sub); font-weight: 600; font-size: 14px;
      cursor: pointer; transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
      display: flex; flex-direction: column; align-items: center; gap: 8px;
    }
    .mode-btn i { font-size: 24px; font-style: normal; }
    .mode-btn:hover { background: rgba(255,255,255,0.06); }
    .mode-btn.active {
      background: linear-gradient(135deg, rgba(108,99,255,0.2), rgba(255,51,102,0.1));
      border-color: rgba(108,99,255,0.4); color: #FFF;
      box-shadow: 0 10px 20px rgba(108,99,255,0.15);
      transform: translateY(-2px);
    }

    /* Controls */
    .control-row { display: flex; flex-direction: column; gap: 12px; margin-top: 24px; }
    .slider-header { display: flex; justify-content: space-between; font-size: 14px; font-weight: 600; }
    .slider-val { color: var(--accent); font-weight: 800; }
    
    input[type=range] {
      -webkit-appearance: none; width: 100%; height: 6px; 
      background: rgba(255,255,255,0.1); border-radius: 3px; outline: none;
    }
    input[type=range]::-webkit-slider-thumb {
      -webkit-appearance: none; width: 20px; height: 20px; border-radius: 50%;
      background: #FFF; box-shadow: 0 0 15px var(--accent-glow); cursor: pointer;
      transition: transform 0.1s;
    }
    input[type=range]::-webkit-slider-thumb:active { transform: scale(1.2); }

    .color-picker-wrapper { position: relative; width: 100%; height: 60px; border-radius: 16px; overflow: hidden; border: 1px solid var(--border); }
    input[type=color] { position: absolute; top: -10px; left: -10px; width: calc(100% + 20px); height: calc(100% + 20px); border: none; cursor: pointer; background: transparent; }

    /* Danger Button */
    .btn-danger {
      background: rgba(255,51,102,0.1); border: 1px solid rgba(255,51,102,0.2);
      color: #FF3366; border-radius: 16px; padding: 18px; font-weight: 600; font-size: 14px;
      width: 100%; cursor: pointer; transition: all 0.2s;
      font-family: 'Outfit', sans-serif;
    }
    .btn-danger:hover { background: rgba(255,51,102,0.2); }
    .btn-danger:active { transform: scale(0.98); }
    
    .status-dot { display: inline-block; width: 8px; height: 8px; background: #2ECC71; border-radius: 50%; margin-right: 6px; box-shadow: 0 0 8px #2ECC71; }
  </style>
</head>
<body>
  <div class="orb orb-1"></div>
  <div class="orb orb-2"></div>

  <div class="app-container">
    <div class="header">
      <h1>LithoSync</h1>
      <p><span class="status-dot"></span>Cihaza Bağlanıldı</p>
    </div>

    <!-- Live Preview -->
    <div class="panel">
      <h2 class="panel-title">Canlı Önizleme</h2>
      <div class="led-bar" id="strip">
        <div class="led"></div><div class="led"></div><div class="led"></div>
        <div class="led"></div><div class="led"></div><div class="led"></div>
      </div>
    </div>

    <!-- Mode Selector -->
    <div class="panel">
      <h2 class="panel-title">Aydınlatma Modu</h2>
      <div class="mode-grid">
        <div class="mode-btn" id="m0" onclick="setMode(0)"><i>🎨</i> Statik Renk</div>
        <div class="mode-btn" id="m1" onclick="setMode(1)"><i>⚡</i> Karaşimşek</div>
        <div class="mode-btn" id="m2" onclick="setMode(2)"><i>🌩️</i> Şimşek Efekti</div>
        <div class="mode-btn" id="m3" onclick="setMode(3)"><i>🖥️</i> UDP / AmbiLight</div>
      </div>
      
      <div class="control-row">
        <div class="slider-header"><span>Parlaklık</span><span class="slider-val" id="bval">%80</span></div>
        <input type="range" min="0" max="255" id="bright" oninput="updateB(this.value)" onchange="setB(this.value)">
      </div>

      <div class="control-row">
        <div class="slider-header"><span>Global Renk</span></div>
        <div class="color-picker-wrapper">
          <input type="color" id="col" value="#6C63FF" onchange="setC(this.value)">
        </div>
      </div>
    </div>

    <button class="btn-danger" onclick="resetW()">Farklı Wi-Fi Ağına Bağlan (Sıfırla)</button>
  </div>

  <script>
    let activeMode = 0;
    function fetchS() {
      fetch('/status').then(r=>r.json()).then(d=>{
        if(d.ok){
          activeMode = d.mode;
          document.querySelectorAll('.mode-btn').forEach((el,i)=>el.classList.toggle('active', i===d.mode));
          document.getElementById('bright').value = d.brightness;
          document.getElementById('bval').innerText = Math.round(d.brightness/2.55) + '%';
          if(d.leds && d.leds.length===6){
            let dots = document.querySelectorAll('.led');
            d.leds.forEach((c,i)=>{
              dots[i].style.background = `rgb(${c.r},${c.g},${c.b})`;
              dots[i].style.boxShadow = `0 0 12px rgba(${c.r},${c.g},${c.b},0.6)`;
            });
          }
        }
      }).catch(()=>{});
    }

    function setMode(m){ fetch('/setMode',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({mode:m})}).then(()=>fetchS()); }
    function updateB(v){ document.getElementById('bval').innerText = Math.round(v/2.55) + '%'; }
    function setB(b){ fetch('/setBrightness',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({brightness:parseInt(b)})}); }
    function setC(hex){
      let r=parseInt(hex.substr(1,2),16), g=parseInt(hex.substr(3,2),16), b=parseInt(hex.substr(5,2),16);
      fetch('/setColor',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({r:r,g:g,b:b})}).then(()=>fetchS());
    }
    function resetW(){
      if(confirm("Wi-Fi ayarları silinecek ve cihaz kurulum moduna geçecek. Onaylıyor musunuz?")){
        fetch('/reset',{method:'POST'}).then(()=>alert("Cihaz sıfırlandı. Lütfen telefondan cihazın ağına bağlanın."));
      }
    }

    fetchS();
    setInterval(fetchS, 2000);
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
