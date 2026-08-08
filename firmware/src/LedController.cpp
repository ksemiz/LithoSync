// =============================================================================
//  LedController.cpp  —  FastLED animasyonları ve LED mod implementasyonları
// =============================================================================

#include "LedController.h"
#include <Arduino.h>

// ─── Kurucu ───────────────────────────────────────────────────────────────────
LedController::LedController()
    : _mode(MODE_STATIC),
      _brightness(DEFAULT_BRIGHTNESS),
      _krPos(0), _krDir(1), _krLastUpdate(0),
      _thunderPhase(ThunderPhase::WAIT),
      _thunderLed(0),
      _thunderColor(CRGB::White),
      _thunderNextEvent(0),
      _thunderFlashStart(0)
{}

// ─── begin() ──────────────────────────────────────────────────────────────────
void LedController::begin() {
    FastLED.addLeds<LED_TYPE, LED_PIN, COLOR_ORDER>(leds, NUM_LEDS)
           .setCorrection(TypicalLEDStrip);
    FastLED.setBrightness(_brightness);
    fill_solid(leds, NUM_LEDS, CRGB::Black);
    FastLED.show();
    Serial.printf("[LED] FastLED başlatıldı. Pin=%d, LED=%d, Parlaklık=%d\n",
                  LED_PIN, NUM_LEDS, _brightness);
}

// ─── update() — loop() içinden çağrılır ───────────────────────────────────────
void LedController::update() {
    switch (_mode) {
        case MODE_STATIC:       /* Statik: harici güncelleme */  break;
        case MODE_KNIGHT_RIDER: _tickKnightRider();              break;
        case MODE_THUNDER:      _tickThunder();                  break;
        case MODE_UDP:          /* UDP: writeUdpData() ile */    break;
    }
}

// ─── setMode() ────────────────────────────────────────────────────────────────
void LedController::setMode(uint8_t mode) {
    if (mode > MODE_UDP) {
        Serial.printf("[LED] Geçersiz mod: %d\n", mode);
        return;
    }
    _mode = mode;
    fill_solid(leds, NUM_LEDS, CRGB::Black);
    FastLED.show();

    if (mode == MODE_KNIGHT_RIDER) {
        _krPos = 0;
        _krDir = 1;
        _krLastUpdate = 0;
    }
    if (mode == MODE_THUNDER) {
        _thunderPhase     = ThunderPhase::WAIT;
        _thunderNextEvent = millis() + random(THUNDER_MIN_WAIT_MS, THUNDER_MAX_WAIT_MS);
    }
    Serial.printf("[LED] Mod ayarlandı: %d\n", mode);
}

// ─── Renk & Parlaklık ─────────────────────────────────────────────────────────
void LedController::setGlobalColor(CRGB color) {
    fill_solid(leds, NUM_LEDS, color);
    FastLED.show();
}

void LedController::setLedColor(uint8_t index, CRGB color) {
    if (index >= NUM_LEDS) return;
    leds[index] = color;
    FastLED.show();
}

CRGB LedController::getLedColor(uint8_t index) const {
    return (index < NUM_LEDS) ? leds[index] : CRGB::Black;
}

void LedController::setBrightness(uint8_t brightness) {
    _brightness = brightness;
    FastLED.setBrightness(brightness);
    FastLED.show();
    Serial.printf("[LED] Parlaklık: %d\n", brightness);
}

// ─── UDP / Ambilight veri yazma ───────────────────────────────────────────────
void LedController::writeUdpData(const uint8_t* data, size_t len) {
    if (_mode != MODE_UDP) return;

    // Her LED için 3 bayt: R, G, B
    size_t count = min(len / 3, (size_t)NUM_LEDS);
    for (size_t i = 0; i < count; i++) {
        leds[i] = CRGB(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
    }
    FastLED.show();
}

// =============================================================================
//  ── Animasyon: Knight Rider / Karaşimşek ─────────────────────────────────────
// =============================================================================
void LedController::_tickKnightRider() {
    unsigned long now = millis();
    if (now - _krLastUpdate < KR_UPDATE_MS) return;
    _krLastUpdate = now;

    fill_solid(leds, NUM_LEDS, CRGB::Black);

    // Ana LED — tam kırmızı
    if (_krPos >= 0 && _krPos < NUM_LEDS) {
        leds[_krPos] = CRGB::Red;
    }

    // Kuyruk efekti (arkaya doğru kademeli sönme)
    for (int8_t t = 1; t <= KR_TAIL_LEN; t++) {
        int8_t tailPos = _krPos - (_krDir * t);
        if (tailPos >= 0 && tailPos < NUM_LEDS) {
            // Doğrusal parlaklık azaltma
            uint8_t bright = 255 - (uint8_t)(t * (255 / (KR_TAIL_LEN + 1)));
            leds[tailPos] = CRGB(bright, 0, 0);
        }
    }

    FastLED.show();

    // Pozisyon güncelle ve sınır kontrolü
    _krPos += _krDir;
    if (_krPos >= NUM_LEDS) {
        _krDir = -1;
        _krPos  = NUM_LEDS - 2;
    } else if (_krPos < 0) {
        _krDir = 1;
        _krPos = 1;
    }
}

// =============================================================================
//  ── Animasyon: Thunder / Şimşek ──────────────────────────────────────────────
// =============================================================================
void LedController::_tickThunder() {
    unsigned long now = millis();

    switch (_thunderPhase) {

        case ThunderPhase::WAIT:
            if (now >= _thunderNextEvent) {
                // Rastgele LED seç
                _thunderLed = random(0, NUM_LEDS);

                // Renk: soğuk beyaz veya mavi
                _thunderColor = (random(2) == 0)
                    ? CRGB(210, 225, 255)   // soğuk beyaz
                    : CRGB(80,  130, 255);  // elektrik mavisi

                fill_solid(leds, NUM_LEDS, CRGB::Black);
                leds[_thunderLed] = _thunderColor;

                // Komşu LED'leri çok sönük yak (yayılma efekti)
                if (_thunderLed > 0)
                    leds[_thunderLed - 1] = CRGB(
                        _thunderColor.r / 5,
                        _thunderColor.g / 5,
                        _thunderColor.b / 5);
                if (_thunderLed < NUM_LEDS - 1)
                    leds[_thunderLed + 1] = CRGB(
                        _thunderColor.r / 5,
                        _thunderColor.g / 5,
                        _thunderColor.b / 5);

                FastLED.show();
                _thunderPhase     = ThunderPhase::FLASH;
                _thunderFlashStart = now;
            }
            break;

        case ThunderPhase::FLASH:
            if (now - _thunderFlashStart >= THUNDER_FLASH_MS) {
                _thunderPhase = ThunderPhase::DIM;
            }
            break;

        case ThunderPhase::DIM: {
            unsigned long elapsed = now - _thunderFlashStart - THUNDER_FLASH_MS;
            if (elapsed >= THUNDER_DIM_MS) {
                fill_solid(leds, NUM_LEDS, CRGB::Black);
                FastLED.show();
                _thunderPhase     = ThunderPhase::WAIT;
                _thunderNextEvent = now + random(THUNDER_MIN_WAIT_MS, THUNDER_MAX_WAIT_MS);
            } else {
                // Kademeli sönme
                uint8_t bright = (uint8_t)map(elapsed, 0, THUNDER_DIM_MS, 255, 0);
                fill_solid(leds, NUM_LEDS, CRGB::Black);
                leds[_thunderLed] = _thunderColor;
                leds[_thunderLed].nscale8(bright);
                FastLED.show();
            }
            break;
        }
    }
}
