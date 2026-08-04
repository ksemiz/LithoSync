# IoT LED Controller — Proje README

## Donanım
- **Mikrodenetleyici**: ESP32-C3 Super Mini
- **LED**: 6× WS2812B (Neopixel), GPIO 8

---

## Firmware Kurulumu (PlatformIO)

### 1. Bağımlılıklar
```
platformio.ini dosyasındaki lib_deps otomatik indirilir.
```

### 2. OTA URL Ayarı
`include/Config.h` dosyasında şu satırı kendi repo adresinizle güncelleyin:
```cpp
#define OTA_VERSION_URL "https://raw.githubusercontent.com/YOUR_USER/YOUR_REPO/main/firmware/version.json"
```

### 3. Yükleme
```bash
pio run --target upload
pio device monitor
```

### 4. İlk Başlatma
- ESP32 **"IoT-LED-Setup"** adlı bir Wi-Fi ağı yayınlar
- Şifre: `12345678`
- Telefon/PC ile bağlanıp `192.168.4.1` adresine gidin
- Wi-Fi bilgilerinizi girin
- Cihaz bağlandıktan sonra Serial Monitor'de IP adresini göreceksiniz
- `http://iot-led.local` adresinden de erişebilirsiniz

---

## REST API Referansı

| Method | Endpoint | Gövde | Açıklama |
|--------|----------|-------|----------|
| GET | `/status` | — | Cihaz durumu |
| POST | `/setMode` | `{"mode": 0-3}` | Mod değiştir |
| POST | `/setColor` | `{"r":255,"g":0,"b":0}` | Global renk |
| POST | `/setLedColor` | `{"index":0,"r":255,"g":0,"b":0}` | Bireysel LED |
| POST | `/setBrightness` | `{"brightness": 128}` | Parlaklık |
| GET | `/checkUpdate` | — | OTA kontrol |
| POST | `/reset` | — | WiFi sıfırla |

---

## UDP Protokolü (Mod 3 — Ambilight)

```
Paket boyutu: 18 bayt
Byte  0-2 : LED 0 → R, G, B
Byte  3-5 : LED 1 → R, G, B
...
Byte 15-17: LED 5 → R, G, B
```

---

## Masaüstü Uygulaması Kurulumu

### Gereksinimler
- .NET 8 SDK
- Windows 10/11

### Bağımlılıklar (otomatik NuGet)
- `CommunityToolkit.Mvvm` — MVVM framework
- `NAudio` — WASAPI ses analizi
- `SpotifyAPI.Web` — Spotify OAuth
- `ColorThief.Standard` — Albüm renk çıkarma

### Çalıştırma
```bash
cd desktop-app/IoTLedController
dotnet run
```

### Spotify Ayarı
1. https://developer.spotify.com/dashboard adresinden uygulama oluşturun
2. Redirect URI olarak `http://localhost:5543/callback` ekleyin
3. Client ID'yi uygulamada Spotify sekmesine girin

---

## LED Modları

| Mod | Ad | Açıklama |
|-----|----|----------|
| 0 | **Statik** | Global veya bireysel LED renk/parlaklık ayarı |
| 1 | **Knight Rider** | Kırmızı kayan ışık + kuyruk efekti |
| 2 | **Thunder** | Rastgele mavi/beyaz şimşek çakmaları |
| 3 | **UDP/Ambilight** | Dışarıdan gelen UDP paketini direkt LED'e yazar |

---

## OTA Güncelleme Akışı

```
1. version.json → {"version":"1.0.1","url":"...firmware.bin"}
2. Semver karşılaştırma: yeni versiyon > mevcut versiyon?
3. Binary stream → ESP32 Update.h ile flash'a yaz
4. ESP.restart()
```

GitHub repo yapısı:
```
YOUR_REPO/
└── firmware/
    ├── version.json    ← versiyonu ve .bin URL'sini içerir
    └── firmware.bin    ← PlatformIO build çıktısı (.pio/build/esp32c3supermini/firmware.bin)
```
