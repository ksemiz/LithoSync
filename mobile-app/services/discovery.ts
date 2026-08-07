// =============================================================================
//  discovery.ts  —  Mobil Ağda Otomatik ESP32 Cihaz Keşif Servisi
// =============================================================================

export interface DiscoveredDevice {
  ip: string;
  version?: string;
  mac?: string;
  ssid?: string;
}

// Hızlı timeout destekli HTTP kontrolü
async function probeIp(ip: string, timeoutMs = 900): Promise<DiscoveredDevice | null> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const res = await fetch(`http://${ip}/status`, {
      signal: controller.signal,
      headers: { Accept: 'application/json' },
    });
    if (!res.ok) return null;
    const data = await res.json();
    if (data && data.ok === true) {
      return {
        ip: data.ip || ip,
        version: data.version,
        mac: data.mac,
        ssid: data.ssid,
      };
    }
  } catch {
    // Timeout veya ulaşılamadı — geç
  } finally {
    clearTimeout(timer);
  }
  return null;
}

// ─────────────────────────────────────────────────────────────────────────────
// 1. Aşama: mDNS veya AP Varsayılan IP'si
// ─────────────────────────────────────────────────────────────────────────────
export async function tryFastDiscovery(): Promise<DiscoveredDevice | null> {
  // mDNS ve SoftAP varsayılan IP'si
  const quickCandidates = ['iot-led.local', '192.168.4.1'];

  for (const candidate of quickCandidates) {
    const dev = await probeIp(candidate, 1200);
    if (dev) return dev;
  }
  return null;
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. Aşama: Akıllı Subnet Taraması (Eşzamanlı 25'li paketler halinde)
// ─────────────────────────────────────────────────────────────────────────────
export async function scanSubnet(
  onProgress?: (progress: number, currentSubnet: string) => void,
  abortSignal?: AbortSignal
): Promise<DiscoveredDevice | null> {
  // Yaygın ev router alt ağları
  const subnets = ['192.168.1', '192.168.0', '192.168.4', '10.0.0'];

  for (let sIdx = 0; sIdx < subnets.length; sIdx++) {
    const prefix = subnets[sIdx];
    if (abortSignal?.aborted) return null;

    onProgress?.((sIdx / subnets.length) * 100, `${prefix}.x taranıyor...`);

    // 1-254 arası IP'leri 25'li gruplar halinde eşzamanlı sorgula (hızlı tarama)
    const hostList: string[] = [];
    for (let i = 1; i <= 254; i++) {
      hostList.push(`${prefix}.${i}`);
    }

    const CHUNK_SIZE = 25;
    for (let i = 0; i < hostList.length; i += CHUNK_SIZE) {
      if (abortSignal?.aborted) return null;

      const chunk = hostList.slice(i, i + CHUNK_SIZE);
      const promises = chunk.map(ip => probeIp(ip, 800));

      const results = await Promise.all(promises);
      const found = results.find(d => d !== null);
      if (found) {
        onProgress?.(100, `Cihaz bulundu: ${found.ip}`);
        return found;
      }
    }
  }

  onProgress?.(100, 'Tarama tamamlandı');
  return null;
}

// ─────────────────────────────────────────────────────────────────────────────
// Tam Otomatik Arama Pipeline'ı
// ─────────────────────────────────────────────────────────────────────────────
export async function autoDiscoverDevice(
  onProgress?: (progress: number, status: string) => void,
  abortSignal?: AbortSignal
): Promise<DiscoveredDevice | null> {
  onProgress?.(10, 'mDNS ile aranıyor (iot-led.local)...');

  // 1. Adım: Hızlı mDNS ve AP testi
  const fastResult = await tryFastDiscovery();
  if (fastResult) {
    onProgress?.(100, `Cihaz bulundu: ${fastResult.ip}`);
    return fastResult;
  }

  if (abortSignal?.aborted) return null;

  // 2. Adım: Subnet Sweep (Yerel Ağ Taraması)
  onProgress?.(25, 'Yerel WiFi ağı taranıyor...');
  const subnetResult = await scanSubnet(onProgress, abortSignal);
  return subnetResult;
}
