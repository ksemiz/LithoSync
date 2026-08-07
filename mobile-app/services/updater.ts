// =============================================================================
//  updater.ts  —  GitHub Releases Tabanlı Mobil Otomatik Güncelleme Servisi
// =============================================================================

import * as Linking from 'expo-linking';
import appConfig from '../app.json';

export interface MobileRelease {
  version: string;
  name: string;
  body: string;
  publishedAt: string;
  htmlUrl: string;
  apkDownloadUrl?: string;
  apkSize?: number;
  hasUpdate: boolean;
}

const GH_OWNER = 'ksemiz';
const GH_REPO = 'LithoSync';
const API_URL = `https://api.github.com/repos/${GH_OWNER}/${GH_REPO}/releases/latest`;

// app.json'daki mevcut uygulama sürümü
export const CURRENT_MOBILE_VERSION = appConfig.expo.version || '1.0.0';

// Basit semver karşılaştırıcı (örn: "1.0.1" > "1.0.0")
function isNewerVersion(latest: string, current: string): boolean {
  const cleanLatest = latest.replace(/^[vV]/, '').trim();
  const cleanCurrent = current.replace(/^[vV]/, '').trim();

  const lParts = cleanLatest.split('.').map(n => parseInt(n, 10) || 0);
  const cParts = cleanCurrent.split('.').map(n => parseInt(n, 10) || 0);

  for (let i = 0; i < Math.max(lParts.length, cParts.length); i++) {
    const l = lParts[i] || 0;
    const c = cParts[i] || 0;
    if (l > c) return true;
    if (l < c) return false;
  }
  return false;
}

export async function checkForMobileUpdate(): Promise<MobileRelease | null> {
  try {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 6000);

    const res = await fetch(API_URL, {
      signal: controller.signal,
      headers: {
        Accept: 'application/vnd.github.v3+json',
        'User-Agent': `LithoSync-Mobile/${CURRENT_MOBILE_VERSION}`,
      },
    });

    clearTimeout(timer);
    if (!res.ok) return null;

    const data = await res.json();
    const latestVersion = data.tag_name || data.name || '';
    const hasUpdate = isNewerVersion(latestVersion, CURRENT_MOBILE_VERSION);

    // .apk uzantılı asset varsa linkini al
    let apkDownloadUrl: string | undefined;
    let apkSize: number | undefined;

    if (Array.isArray(data.assets)) {
      const apkAsset = data.assets.find((a: any) =>
        typeof a.name === 'string' && a.name.toLowerCase().endsWith('.apk')
      );
      if (apkAsset) {
        apkDownloadUrl = apkAsset.browser_download_url;
        apkSize = apkAsset.size;
      }
    }

    return {
      version: latestVersion,
      name: data.name || latestVersion,
      body: data.body || '',
      publishedAt: data.published_at,
      htmlUrl: data.html_url,
      apkDownloadUrl: apkDownloadUrl || data.html_url,
      apkSize,
      hasUpdate,
    };
  } catch (err) {
    console.log('[UPDATER] Mobil güncelleme kontrolü başarısız:', err);
    return null;
  }
}

export async function startMobileUpdate(release: MobileRelease): Promise<void> {
  const url = release.apkDownloadUrl || release.htmlUrl;
  if (url) {
    await Linking.openURL(url);
  }
}
