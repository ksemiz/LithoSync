// =============================================================================
//  api.ts  —  ESP32 REST API yardımcıları
//  Tüm endpoint'ler Config.h'daki HTTP_PORT:80 üzerinden çağrılır
// =============================================================================

export interface DeviceStatus {
  ok: boolean;
  version: string;
  mode: number;
  brightness: number;
  ip: string;
  ssid: string;
  mac: string;
  uptime: number;
  leds: Array<{ r: number; g: number; b: number }>;
}

export interface RgbColor {
  r: number;
  g: number;
  b: number;
}

const TIMEOUT_MS = 5000;

// Timeout destekli fetch sarmalayıcı
async function fetchWithTimeout(url: string, options?: RequestInit): Promise<Response> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), TIMEOUT_MS);
  try {
    const res = await fetch(url, { ...options, signal: controller.signal });
    return res;
  } finally {
    clearTimeout(timer);
  }
}

// ─── API Sınıfı ───────────────────────────────────────────────────────────────
export class Esp32Api {
  private baseUrl: string;

  constructor(ip: string, port = 80) {
    this.baseUrl = `http://${ip}:${port === 80 ? '' : port}`.replace(/:$/, '');
  }

  // Bağlantı testi + cihaz durumu
  async getStatus(): Promise<DeviceStatus> {
    const res = await fetchWithTimeout(`${this.baseUrl}/status`);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return res.json();
  }

  // Mod değiştir (0=Static, 1=Knight, 2=Thunder, 3=UDP)
  async setMode(mode: number): Promise<void> {
    await fetchWithTimeout(`${this.baseUrl}/setMode`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ mode }),
    });
  }

  // Tüm LED'leri aynı renge ayarla
  async setGlobalColor(color: RgbColor): Promise<void> {
    await fetchWithTimeout(`${this.baseUrl}/setColor`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(color),
    });
  }

  // Tek LED rengini ayarla
  async setLedColor(index: number, color: RgbColor): Promise<void> {
    await fetchWithTimeout(`${this.baseUrl}/setLedColor`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ index, ...color }),
    });
  }

  // Parlaklık ayarla (0-255)
  async setBrightness(brightness: number): Promise<void> {
    await fetchWithTimeout(`${this.baseUrl}/setBrightness`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ brightness }),
    });
  }

  // OTA güncelleme tetikle
  async checkUpdate(): Promise<void> {
    await fetchWithTimeout(`${this.baseUrl}/checkUpdate`);
  }

  // WiFi sıfırla
  async reset(): Promise<void> {
    await fetchWithTimeout(`${this.baseUrl}/reset`, { method: 'POST' });
  }
}

// ─── Yardımcı: HSV → RGB dönüşümü ───────────────────────────────────────────
export function hsvToRgb(h: number, s: number, v: number): RgbColor {
  // h: 0-360, s: 0-1, v: 0-1
  const c = v * s;
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
  const m = v - c;

  let r = 0, g = 0, b = 0;
  if (h < 60)       { r = c; g = x; b = 0; }
  else if (h < 120) { r = x; g = c; b = 0; }
  else if (h < 180) { r = 0; g = c; b = x; }
  else if (h < 240) { r = 0; g = x; b = c; }
  else if (h < 300) { r = x; g = 0; b = c; }
  else              { r = c; g = 0; b = x; }

  return {
    r: Math.round((r + m) * 255),
    g: Math.round((g + m) * 255),
    b: Math.round((b + m) * 255),
  };
}

// ─── Yardımcı: RGB → hex string ──────────────────────────────────────────────
export function rgbToHex({ r, g, b }: RgbColor): string {
  return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
}

// ─── Mod isimleri ─────────────────────────────────────────────────────────────
export const MODE_NAMES = ['Statik', 'Knight Rider', 'Şimşek', 'UDP / Ambilight'];
export const MODE_ICONS = ['🎨', '⚡', '🌩️', '📡'];
export const MODE_DESCRIPTIONS = [
  'Her LED için özel renk ve parlaklık ayarla',
  'Kırmızı kayan ışık — klasik Karaşimşek efekti',
  'Rastgele mavi/beyaz şimşek çakmaları',
  'PC uygulamasından gelen gerçek zamanlı renk verisi',
];
