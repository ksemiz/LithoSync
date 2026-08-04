#pragma once

// =============================================================================
//  LedController.h  —  FastLED sarmalayıcı + tüm LED mod mantığı
// =============================================================================

#include <FastLED.h>
#include "Config.h"

class LedController {
public:
    LedController();

    // Başlatma — setup() içinde bir kez çağrılır
    void begin();

    // Animasyon döngüsü — loop() içinde her iterasyonda çağrılır
    void update();

    // ── Mod ──────────────────────────────────────────────────────────────────
    void    setMode(uint8_t mode);
    uint8_t getMode()       const { return _mode;       }

    // ── Global ve Bireysel Renk ───────────────────────────────────────────────
    void    setGlobalColor(CRGB color);               // Tüm LED'ler
    void    setLedColor(uint8_t index, CRGB color);   // Tek LED
    CRGB    getLedColor(uint8_t index)  const;

    // ── Parlaklık ─────────────────────────────────────────────────────────────
    void    setBrightness(uint8_t brightness);
    uint8_t getBrightness() const { return _brightness; }

    // ── UDP / Ambilight veri yazma (Mod 3) ───────────────────────────────────
    // data: NUM_LEDS * 3 bayt (R, G, B sırası), toplam 18 bayt
    void writeUdpData(const uint8_t* data, size_t len);

    // ── Ham erişim (status endpoint için) ────────────────────────────────────
    CRGB leds[NUM_LEDS];

private:
    uint8_t _mode;
    uint8_t _brightness;

    // ── Knight Rider durumu ───────────────────────────────────────────────────
    int8_t        _krPos;
    int8_t        _krDir;
    unsigned long _krLastUpdate;
    void          _tickKnightRider();

    // ── Thunder durumu ────────────────────────────────────────────────────────
    enum class ThunderPhase : uint8_t { WAIT, FLASH, DIM };
    ThunderPhase  _thunderPhase;
    uint8_t       _thunderLed;
    CRGB          _thunderColor;
    unsigned long _thunderNextEvent;
    unsigned long _thunderFlashStart;
    void          _tickThunder();
};
