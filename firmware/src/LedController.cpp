// =============================================================================
//  LedController.cpp  —  FastLED animasyonları ve LED mod implementasyonları
// =============================================================================

#include "LedController.h"
#include <Arduino.h>

// ─── Kurucu ───────────────────────────────────────────────────────────────────
LedController::LedController()
    : _mode(MODE_STATIC),
      _brightness(DEFAULT_BRIGHTNESS),
      _animColor(CRGB::Red),
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
//  ── Animasyon: Knight Rider — Renk Destekli + Smooth Kuyruk ────────────────
// =============================================================================
void LedController::_tickKnightRider() {
    unsigned long now = millis();
    if (now - _krLastUpdate < KR_UPDATE_MS) return;
    _krLastUpdate = now;

    fill_solid(leds, NUM_LEDS, CRGB::Black);

    // Ana LED — tam parlak animasyon rengi
    if (_krPos >= 0 && _krPos < NUM_LEDS) {
        leds[_krPos] = _animColor;
    }

    // Ön aydınlatma (gittigı yönde 1 LED çok sönük)
    int8_t frontPos = _krPos + _krDir;
    if (frontPos >= 0 && frontPos < NUM_LEDS) {
        leds[frontPos] = CRGB(
            _animColor.r / 8,
            _animColor.g / 8,
            _animColor.b / 8);
    }

    // Kuyruk efekti — Quadratic fade ile cok daha smooth
    const int TAIL = 5;
    for (int t = 1; t <= TAIL; t++) {
        int8_t tailPos = _krPos - (_krDir * t);
        if (tailPos >= 0 && tailPos < NUM_LEDS) {
            float ratio = (float)(TAIL - t + 1) / (TAIL + 1);
            ratio = ratio * ratio; // quadratic fade
            leds[tailPos] = CRGB(
                (uint8_t)(_animColor.r * ratio),
                (uint8_t)(_animColor.g * ratio),
                (uint8_t)(_animColor.b * ratio));
        }
    }

    FastLED.show();

    _krPos += _krDir;
    if (_krPos >= NUM_LEDS) { _krDir = -1; _krPos = NUM_LEDS - 2; }
    else if (_krPos < 0)    { _krDir =  1; _krPos = 1; }
}

// =============================================================================
//  ── Animasyon: Thunder / Şimşek — Renk Destekli + Çoklu Flash ─────────────
// =============================================================================
void LedController::_tickThunder() {
    unsigned long now = millis();

    switch (_thunderPhase) {

        case ThunderPhase::WAIT:
            if (now >= _thunderNextEvent) {
                _thunderLed = random(0, NUM_LEDS);

                // Kullanıcı rengi veya varsayılan elektrik rengi
                if (_animColor.r == 255 && _animColor.g == 0 && _animColor.b == 0) {
                    // Varsayılan (kirmizi) ise rassal elektrik renkleri kullan
                    _thunderColor = (random(3) == 0)
                        ? CRGB(255, 255, 255)   // beyaz
                        : (random(2) == 0)
                            ? CRGB(80, 130, 255)    // elektrik mavisi
                            : CRGB(200, 100, 255);  // mor
                } else {
                    _thunderColor = _animColor;
                }

                // Çoklu Flash: 1-3 hızlı yanip sönme
                int flashCount = random(1, 4);
                for (int f = 0; f < flashCount; f++) {
                    // Tüm LED'leri farklı yoğunluklarda yak
                    for (int i = 0; i < NUM_LEDS; i++) {
                        uint8_t bright = (i == _thunderLed) ? 255
                                       : (abs(i - (int)_thunderLed) == 1) ? 140
                                       : (abs(i - (int)_thunderLed) == 2) ? 50
                                       : 15;
                        leds[i] = CRGB(
                            (_thunderColor.r * bright) >> 8,
                            (_thunderColor.g * bright) >> 8,
                            (_thunderColor.b * bright) >> 8);
                    }
                    FastLED.show();
                    delay(random(20, 70));  // flash süresi
                    fill_solid(leds, NUM_LEDS, CRGB::Black);
                    FastLED.show();
                    if (f < flashCount - 1) delay(random(30, 100));
                }

                // Son flash — fade için ayarla
                leds[_thunderLed] = _thunderColor;
                FastLED.show();
                _thunderPhase      = ThunderPhase::FLASH;
                _thunderFlashStart = millis();
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
