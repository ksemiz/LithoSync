# LithoSync — IoT LED & AmbiLight Controller

## Proje Yapısı
- **firmware/**: ESP32-C3 Super Mini + 6× WS2812B (GPIO 8) C++ gömülü yazılımı (PlatformIO)
- **desktop-app/**: C# .NET 8 WPF PC uygulaması (AmbiLight + NAudio FFT + Spotify OAuth2)
- **mobile-app/**: Expo React Native & TypeScript iOS / Android mobil uygulaması

---

## 1. Donanım & Firmware (ESP32-C3)
- **Mikrodenetleyici**: ESP32-C3 Super Mini
- **LED**: 6× WS2812B (Neopixel), GPIO 8
- **Wi-Fi Kurulumu**: Captive Portal (İlk açılışta `IoT-LED-Setup` AP yayını, şifre: `12345678`, IP: `192.168.4.1`)
- **Modlar**:
  - `Mod 0`: Statik (Global / Bireysel renk)
  - `Mod 1`: Knight Rider (Karaşimşek)
  - `Mod 2`: Thunder (Şimşek efekti)
  - `Mod 3`: UDP / Ambilight (Gerçek zamanlı dış renk akışı)

---

## 2. Masaüstü Uygulaması (C# WPF .NET 8)
- Ekran görüntüsü analizi ile 30 FPS **AmbiLight** (GDI+ BitmapData unsafe pointer)
- System Audio FFT analizi ile ritim tabanlı **Ses Analizi** (NAudio WASAPI Loopback)
- **Spotify Web API** OAuth2 PKCE entegrasyonu ve ColorThief albüm kapağı renk haritası

---

## 3. Mobil Uygulama (Expo / React Native)
LithoSync Mobil Uygulaması, ev Wi-Fi ağınızdayken ESP32-C3 cihazınızı akıllı bir uzaktan kumanda gibi yönetmenizi sağlar.

### Özellikler
- 🎨 **HSV Renk Çarkı**: Canlı renk seçimi ve 6 LED için bireysel renk belirleme
- ⚡ **Mod Seçici**: Statik, Knight Rider, Şimşek ve UDP modları arasında tek tıkla geçiş
- 💡 **Parlaklık Kontrolü**: Yumuşak parlaklık ayarı (0-255)
- 📡 **Canlı LED Önizleme**: ESP32'deki LED renklerini ve durumunu anlık olarak ekranda görün
- 🔄 **OTA & WiFi Sıfırlama**: Cihazı kablosuz olarak güncelleme ve sıfırlama

### Mobil Uygulamayı Çalıştırma (Expo Go)
1. Telefonunuza App Store veya Google Play Store'dan **Expo Go** uygulamasını yükleyin.
2. Terminalde mobil uygulama klasörüne gidin ve başlatın:
   ```bash
   cd mobile-app
   npx expo start
   ```
3. Terminalde çıkan **QR Kodunu** telefonunuzun kamerası (veya Expo Go uygulaması) ile okutun. Uygulama saniyeler içinde telefonunuzda açılacaktır!

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
