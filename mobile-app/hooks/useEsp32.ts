// =============================================================================
//  useEsp32.ts  —  ESP32 bağlantı, otomatik keşif ve mobil güncelleme yönetimi
// =============================================================================

import { useState, useCallback, useRef, useEffect } from 'react';
import { Esp32Api, DeviceStatus, RgbColor } from '@/constants/api';
import { autoDiscoverDevice } from '@/services/discovery';
import { checkForMobileUpdate, MobileRelease } from '@/services/updater';

export type ConnectionState = 'disconnected' | 'searching' | 'connecting' | 'connected' | 'error';

export interface Esp32State {
  connectionState: ConnectionState;
  status: DeviceStatus | null;
  error: string | null;
  ip: string;
  api: Esp32Api | null;
  searchStatus: string;
  searchProgress: number;
  availableUpdate: MobileRelease | null;
}

export function useEsp32() {
  const [state, setState] = useState<Esp32State>({
    connectionState: 'disconnected',
    status: null,
    error: null,
    ip: '',
    api: null,
    searchStatus: '',
    searchProgress: 0,
    availableUpdate: null,
  });

  const apiRef = useRef<Esp32Api | null>(null);
  const searchAbortRef = useRef<AbortController | null>(null);

  // ── Doğrudan IP ile Bağlan ────────────────────────────────────────────────
  const connect = useCallback(async (ip: string) => {
    setState(prev => ({
      ...prev,
      connectionState: 'connecting',
      error: null,
      ip,
      searchStatus: 'Bağlanıyor...',
    }));

    const api = new Esp32Api(ip);
    apiRef.current = api;

    try {
      const status = await api.getStatus();
      setState(prev => ({
        ...prev,
        connectionState: 'connected',
        status,
        api,
        error: null,
        searchStatus: 'Bağlandı',
        searchProgress: 100,
      }));
      return true;
    } catch (err: any) {
      const msg = err?.message?.includes('Abort') ? 'Zaman aşımı (5s)' : err?.message ?? 'Bağlantı hatası';
      setState(prev => ({
        ...prev,
        connectionState: 'error',
        error: msg,
        api: null,
        searchStatus: 'Bağlantı kurulamadı',
      }));
      return false;
    }
  }, []);

  // ── Otomatik Cihaz Keşfet ve Bağlan ───────────────────────────────────────
  const autoDiscover = useCallback(async () => {
    searchAbortRef.current?.abort();
    const abortController = new AbortController();
    searchAbortRef.current = abortController;

    setState(prev => ({
      ...prev,
      connectionState: 'searching',
      error: null,
      searchStatus: 'Ağda LithoSync aranıyor...',
      searchProgress: 5,
    }));

    try {
      const found = await autoDiscoverDevice((progress, statusText) => {
        setState(prev => ({
          ...prev,
          searchProgress: Math.round(progress),
          searchStatus: statusText,
        }));
      }, abortController.signal);

      if (found && !abortController.signal.aborted) {
        setState(prev => ({
          ...prev,
          ip: found.ip,
          searchStatus: `Cihaz bulundu (${found.ip}), bağlanılıyor...`,
        }));
        return await connect(found.ip);
      } else if (!abortController.signal.aborted) {
        setState(prev => ({
          ...prev,
          connectionState: 'disconnected',
          searchStatus: 'Cihaz ağda bulunamadı',
          error: 'LithoSync cihazı bulunamadı. Cihazın açık ve aynı Wi-Fi ağında olduğundan emin olun.',
        }));
        return false;
      }
    } catch (err: any) {
      if (!abortController.signal.aborted) {
        setState(prev => ({
          ...prev,
          connectionState: 'error',
          searchStatus: 'Arama hatası',
          error: err?.message || 'Arama sırasında hata oluştu',
        }));
      }
      return false;
    }
    return false;
  }, [connect]);

  // ── Bağlantıyı kes ─────────────────────────────────────────────────────────
  const disconnect = useCallback(() => {
    searchAbortRef.current?.abort();
    apiRef.current = null;
    setState(prev => ({
      ...prev,
      connectionState: 'disconnected',
      status: null,
      error: null,
      ip: '',
      api: null,
      searchStatus: '',
      searchProgress: 0,
    }));
  }, []);

  // ── Durumu yenile ──────────────────────────────────────────────────────────
  const refreshStatus = useCallback(async () => {
    if (!apiRef.current) return;
    try {
      const status = await apiRef.current.getStatus();
      setState(prev => ({ ...prev, status }));
    } catch { /* sessizce geç */ }
  }, []);

  // ── GitHub Mobil Güncelleme Kontrolü ──────────────────────────────────────
  const checkUpdates = useCallback(async () => {
    const release = await checkForMobileUpdate();
    if (release && release.hasUpdate) {
      setState(prev => ({ ...prev, availableUpdate: release }));
    }
    return release;
  }, []);

  // ── Uygulama Açılışında Otomatik Keşif ve Güncelleme Kontrolü ───────────────
  useEffect(() => {
    // 1. Otomatik cihaz araması başlat
    autoDiscover();

    // 2. 3 saniye sonra arka planda güncelleme kontrol et
    const timer = setTimeout(() => {
      checkUpdates();
    }, 3000);

    return () => {
      clearTimeout(timer);
      searchAbortRef.current?.abort();
    };
  }, [autoDiscover, checkUpdates]);

  // ── Mod ayarla ─────────────────────────────────────────────────────────────
  const setMode = useCallback(async (mode: number) => {
    if (!apiRef.current) return;
    await apiRef.current.setMode(mode);
    setState(prev => prev.status
      ? { ...prev, status: { ...prev.status, mode } }
      : prev
    );
  }, []);

  // ── Global renk ────────────────────────────────────────────────────────────
  const setGlobalColor = useCallback(async (color: RgbColor) => {
    if (!apiRef.current) return;
    await apiRef.current.setGlobalColor(color);
    setState(prev => prev.status
      ? { ...prev, status: { ...prev.status, leds: Array(6).fill(color) } }
      : prev
    );
  }, []);

  // ── Bireysel LED rengi ──────────────────────────────────────────────────────
  const setLedColor = useCallback(async (index: number, color: RgbColor) => {
    if (!apiRef.current) return;
    await apiRef.current.setLedColor(index, color);
    setState(prev => {
      if (!prev.status) return prev;
      const leds = [...prev.status.leds];
      leds[index] = color;
      return { ...prev, status: { ...prev.status, leds } };
    });
  }, []);

  // ── Parlaklık ──────────────────────────────────────────────────────────────
  const setBrightness = useCallback(async (brightness: number) => {
    if (!apiRef.current) return;
    await apiRef.current.setBrightness(brightness);
    setState(prev => prev.status
      ? { ...prev, status: { ...prev.status, brightness } }
      : prev
    );
  }, []);

  return {
    ...state,
    connect,
    autoDiscover,
    disconnect,
    refreshStatus,
    checkUpdates,
    setMode,
    setGlobalColor,
    setLedColor,
    setBrightness,
    isConnected: state.connectionState === 'connected',
    isSearching: state.connectionState === 'searching',
  };
}
