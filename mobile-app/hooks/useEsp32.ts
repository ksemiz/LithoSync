// =============================================================================
//  useEsp32.ts  —  ESP32 bağlantı ve durum yönetimi hook'u
// =============================================================================

import { useState, useCallback, useRef } from 'react';
import { Esp32Api, DeviceStatus, RgbColor } from '@/constants/api';

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'error';

export interface Esp32State {
  connectionState: ConnectionState;
  status: DeviceStatus | null;
  error: string | null;
  ip: string;
  api: Esp32Api | null;
}

export function useEsp32() {
  const [state, setState] = useState<Esp32State>({
    connectionState: 'disconnected',
    status: null,
    error: null,
    ip: '',
    api: null,
  });

  const apiRef = useRef<Esp32Api | null>(null);

  // ── Bağlan ─────────────────────────────────────────────────────────────────
  const connect = useCallback(async (ip: string) => {
    setState(prev => ({ ...prev, connectionState: 'connecting', error: null, ip }));

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
      }));
      return true;
    } catch (err: any) {
      const msg = err?.message?.includes('Abort') ? 'Zaman aşımı (5s)' : err?.message ?? 'Bağlantı hatası';
      setState(prev => ({
        ...prev,
        connectionState: 'error',
        error: msg,
        api: null,
      }));
      return false;
    }
  }, []);

  // ── Bağlantıyı kes ─────────────────────────────────────────────────────────
  const disconnect = useCallback(() => {
    apiRef.current = null;
    setState({
      connectionState: 'disconnected',
      status: null,
      error: null,
      ip: '',
      api: null,
    });
  }, []);

  // ── Durumu yenile ──────────────────────────────────────────────────────────
  const refreshStatus = useCallback(async () => {
    if (!apiRef.current) return;
    try {
      const status = await apiRef.current.getStatus();
      setState(prev => ({ ...prev, status }));
    } catch { /* sessizce geç */ }
  }, []);

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
    disconnect,
    refreshStatus,
    setMode,
    setGlobalColor,
    setLedColor,
    setBrightness,
    isConnected: state.connectionState === 'connected',
  };
}
