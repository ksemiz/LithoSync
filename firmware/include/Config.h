#pragma once

// =============================================================================
//  Config.h  —  Proje geneli sabitler
//  ESP32-C3 Super Mini + 6x WS2812B @ GPIO 8
// =============================================================================

// ─── Donanım ─────────────────────────────────────────────────────────────────
#define LED_PIN             8          // WS2812B veri pini
#define NUM_LEDS            6          // LED sayısı
#define LED_TYPE            WS2812B
#define COLOR_ORDER         GRB        // WS2812B renk sırası
#define DEFAULT_BRIGHTNESS  80         // Başlangıç parlaklığı (0-255)

// ─── Ağ ──────────────────────────────────────────────────────────────────────
#define HTTP_PORT    80
#define UDP_PORT     4210
#define AP_SSID      "IoT-LED-Setup"   // Captive Portal AP adı
#define AP_PASSWORD  "12345678"        // Captive Portal şifresi
#define HOSTNAME     "iot-led"         // mDNS: http://iot-led.local

// ─── Ön Tanımlı Wi-Fi (Öncelikli Bağlantı) ──────────────────────────────────
#define DEFAULT_WIFI_SSID     "YOUR_WIFI_SSID"      // Varsayılan Wi-Fi Adı (Placeholder)
#define DEFAULT_WIFI_PASS     "YOUR_WIFI_PASSWORD"  // Varsayılan Wi-Fi Şifresi (Placeholder)


// ─── OTA Güncelleme ───────────────────────────────────────────────────────────
#define CURRENT_VERSION    "1.0.0"
#define OTA_VERSION_URL    "https://raw.githubusercontent.com/ksemiz/LithoSync/main/firmware/version.json"
#define OTA_CHECK_INTERVAL (30UL * 60UL * 1000UL)  // Otomatik kontrol: 30 dk

// ─── LED Mod Sabitleri ────────────────────────────────────────────────────────
#define MODE_STATIC        0   // Statik renk (global / bireysel)
#define MODE_KNIGHT_RIDER  1   // Karaşimşek animasyonu
#define MODE_THUNDER       2   // Şimşek efekti
#define MODE_UDP           3   // UDP / Ambilight (harici veri)

// ─── Animasyon Zamanaşımları ──────────────────────────────────────────────────
// Knight Rider
#define KR_UPDATE_MS    50    // Güncelleme aralığı (ms)
#define KR_TAIL_LEN     3     // Kuyruk uzunluğu (soldurma)

// Thunder / Şimşek
#define THUNDER_MIN_WAIT_MS   600    // Şimşekler arası min bekleme
#define THUNDER_MAX_WAIT_MS   4000   // Şimşekler arası max bekleme
#define THUNDER_FLASH_MS      25     // Çakma süresi
#define THUNDER_DIM_MS        180    // Sönme süresi
