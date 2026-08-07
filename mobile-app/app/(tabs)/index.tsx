// =============================================================================
//  index.tsx  —  Mobil Ana Ekran (Otomatik Arama + GitHub Güncelleme)
// =============================================================================

import React, { useState, useCallback, createContext, useContext } from 'react';
import {
  View, Text, TextInput, TouchableOpacity, StyleSheet,
  ScrollView, ActivityIndicator, Alert, KeyboardAvoidingView, Platform,
} from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { Ionicons } from '@expo/vector-icons';
import * as Haptics from 'expo-haptics';
import { Colors, Gradients } from '@/constants/Colors';
import { useEsp32 } from '@/hooks/useEsp32';
import { LedPreview } from '@/components/LedPreview';
import { startMobileUpdate } from '@/services/updater';

// ─── Global ESP32 Context (tüm tab'lar erişebilir) ───────────────────────────
export const Esp32Context = createContext<ReturnType<typeof useEsp32> | null>(null);
export const useEsp32Context = () => {
  const ctx = useContext(Esp32Context);
  if (!ctx) throw new Error('Esp32Context bulunamadı');
  return ctx;
};

// ─── Ana Ekran ───────────────────────────────────────────────────────────────
export default function ConnectScreen() {
  const esp32 = useEsp32();
  const [manualIp, setManualIp] = useState('192.168.1.100');
  const [showManual, setShowManual] = useState(false);

  const handleManualConnect = useCallback(async () => {
    if (!manualIp.trim()) return;
    Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
    const ok = await esp32.connect(manualIp.trim());
    if (ok) Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
    else     Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
  }, [manualIp, esp32]);

  const handleAutoSearch = useCallback(() => {
    Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium);
    esp32.autoDiscover();
  }, [esp32]);

  const handleApplyUpdate = useCallback(async () => {
    if (!esp32.availableUpdate) return;
    Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
    await startMobileUpdate(esp32.availableUpdate);
  }, [esp32.availableUpdate]);

  return (
    <Esp32Context.Provider value={esp32}>
      <LinearGradient colors={['#0D0E1A', '#13141F']} style={styles.root}>
        <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : 'height'} style={styles.kav}>
          <ScrollView contentContainerStyle={styles.scroll} showsVerticalScrollIndicator={false}>

            {/* ── Üst Başlık ─────────────────────────────────────────── */}
            <View style={styles.header}>
              <View style={styles.logoRow}>
                <LinearGradient colors={Gradients.accent} style={styles.logoDot} />
                <Text style={styles.logoText}>LithoSync</Text>
              </View>
              <Text style={styles.subtitle}>Kablosuz LED & Ambilight Kontrolü</Text>
            </View>

            {/* ── GitHub Yeni Sürüm Bildirimi ─────────────────────────── */}
            {esp32.availableUpdate && (
              <TouchableOpacity
                activeOpacity={0.85}
                style={styles.updateBanner}
                onPress={handleApplyUpdate}
              >
                <LinearGradient
                  colors={['rgba(108,99,255,0.25)', 'rgba(74,68,176,0.25)']}
                  style={styles.updateBannerGrad}
                >
                  <View style={styles.updateIconWrap}>
                    <Ionicons name="cloud-download" size={22} color={Colors.accent} />
                  </View>
                  <View style={styles.updateTextWrap}>
                    <Text style={styles.updateTitle}>
                      Yeni Sürüm Mevcut: {esp32.availableUpdate.version}
                    </Text>
                    <Text style={styles.updateSub}>
                      İndirmek ve yüklemek için dokunun
                    </Text>
                  </View>
                  <Ionicons name="chevron-forward" size={18} color={Colors.accent} />
                </LinearGradient>
              </TouchableOpacity>
            )}

            {/* ── Cihaz Durum Kartı ──────────────────────────────────── */}
            <View style={styles.card}>
              <View style={styles.cardHeaderRow}>
                <Text style={styles.cardTitle}>Cihaz Bağlantısı</Text>
                {esp32.isConnected && (
                  <View style={styles.liveBadge}>
                    <View style={styles.liveDot} />
                    <Text style={styles.liveText}>Aktif</Text>
                  </View>
                )}
              </View>

              {!esp32.isConnected ? (
                <>
                  {/* Otomatik Arama Durumu */}
                  <View style={styles.searchBox}>
                    <View style={styles.searchIconRow}>
                      {esp32.isSearching ? (
                        <ActivityIndicator size="small" color={Colors.accent} style={{ marginRight: 8 }} />
                      ) : (
                        <Ionicons name="radio-outline" size={20} color={Colors.accent} style={{ marginRight: 8 }} />
                      )}
                      <Text style={styles.searchStatusText}>
                        {esp32.searchStatus || 'Cihaz aranıyor...'}
                      </Text>
                    </View>

                    {esp32.isSearching && (
                      <View style={styles.progressBarBg}>
                        <View
                          style={[
                            styles.progressBarFill,
                            { width: `${Math.max(5, esp32.searchProgress)}%` },
                          ]}
                        />
                      </View>
                    )}
                  </View>

                  {/* Butonlar */}
                  <TouchableOpacity
                    style={styles.autoSearchBtn}
                    onPress={handleAutoSearch}
                    disabled={esp32.isSearching}
                  >
                    <LinearGradient colors={Gradients.accent} style={styles.btnGrad}>
                      <Ionicons name="search" size={18} color="#FFF" style={{ marginRight: 8 }} />
                      <Text style={styles.btnText}>
                        {esp32.isSearching ? 'Ağ Taranıyor...' : '🔍 Cihazı Otomatik Ara'}
                      </Text>
                    </LinearGradient>
                  </TouchableOpacity>

                  {esp32.error && (
                    <View style={styles.errorBadge}>
                      <Ionicons name="alert-circle" size={16} color={Colors.danger} />
                      <Text style={styles.errorText}>{esp32.error}</Text>
                    </View>
                  )}

                  {/* Manuel IP Accordion */}
                  <TouchableOpacity
                    style={styles.accordionToggle}
                    onPress={() => setShowManual(!showManual)}
                  >
                    <Text style={styles.accordionText}>
                      {showManual ? '▲ Manuel IP Girişini Gizle' : '▼ Gelişmiş: Manuel IP Gir'}
                    </Text>
                  </TouchableOpacity>

                  {showManual && (
                    <View style={styles.manualBox}>
                      <Text style={styles.fieldLabel}>Statik IP / mDNS</Text>
                      <View style={styles.inputRow}>
                        <TextInput
                          style={styles.input}
                          value={manualIp}
                          onChangeText={setManualIp}
                          placeholder="192.168.1.100 veya iot-led.local"
                          placeholderTextColor={Colors.textMuted}
                          autoCapitalize="none"
                        />
                        <TouchableOpacity
                          style={styles.manualBtn}
                          onPress={handleManualConnect}
                        >
                          <Text style={styles.manualBtnText}>Bağlan</Text>
                        </TouchableOpacity>
                      </View>
                    </View>
                  )}
                </>
              ) : (
                <>
                  {/* Bağlı Durum Kartı */}
                  <View style={styles.connectedBox}>
                    <View style={styles.deviceRow}>
                      <Ionicons name="hardware-chip" size={18} color={Colors.accent} />
                      <Text style={styles.deviceInfoText}>
                        IP: <Text style={styles.highlightText}>{esp32.status?.ip || esp32.ip}</Text>
                      </Text>
                    </View>
                    {esp32.status?.ssid ? (
                      <View style={styles.deviceRow}>
                        <Ionicons name="wifi" size={18} color={Colors.accent} />
                        <Text style={styles.deviceInfoText}>
                          Wi-Fi: <Text style={styles.highlightText}>{esp32.status.ssid}</Text>
                        </Text>
                      </View>
                    ) : null}
                    <View style={styles.deviceRow}>
                      <Ionicons name="git-branch" size={18} color={Colors.accent} />
                      <Text style={styles.deviceInfoText}>
                        Firmware: <Text style={styles.highlightText}>v{esp32.status?.version || '1.0.0'}</Text>
                      </Text>
                    </View>
                  </View>

                  <View style={styles.previewSection}>
                    <Text style={styles.previewLabel}>Canlı LED Durumu</Text>
                    <LedPreview
                      colors={esp32.status?.leds ?? Array(6).fill({ r: 40, g: 40, b: 60 })}
                    />
                  </View>

                  <View style={styles.btnRow}>
                    <TouchableOpacity
                      style={styles.reScanBtn}
                      onPress={handleAutoSearch}
                    >
                      <Ionicons name="refresh" size={16} color={Colors.accent} style={{ marginRight: 6 }} />
                      <Text style={styles.reScanText}>Yeniden Tara</Text>
                    </TouchableOpacity>

                    <TouchableOpacity
                      style={styles.disconnectBtn}
                      onPress={esp32.disconnect}
                    >
                      <Ionicons name="power" size={16} color={Colors.danger} style={{ marginRight: 6 }} />
                      <Text style={styles.disconnectText}>Bağlantıyı Kes</Text>
                    </TouchableOpacity>
                  </View>
                </>
              )}
            </View>

          </ScrollView>
        </KeyboardAvoidingView>
      </LinearGradient>
    </Esp32Context.Provider>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1 },
  kav: { flex: 1 },
  scroll: { padding: 24, paddingBottom: 40 },

  header: { marginBottom: 20, marginTop: 10 },
  logoRow: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  logoDot: { width: 12, height: 12, borderRadius: 6 },
  logoText: { fontSize: 26, fontWeight: '800', color: Colors.text, letterSpacing: -0.5 },
  subtitle: { fontSize: 13, color: Colors.textMuted, marginTop: 4 },

  updateBanner: {
    marginBottom: 20,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: 'rgba(108,99,255,0.4)',
    overflow: 'hidden',
  },
  updateBannerGrad: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 16,
  },
  updateIconWrap: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: 'rgba(108,99,255,0.2)',
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 12,
  },
  updateTextWrap: { flex: 1 },
  updateTitle: { fontSize: 14, fontWeight: '700', color: Colors.text },
  updateSub: { fontSize: 12, color: Colors.accent, marginTop: 2 },

  card: {
    backgroundColor: Colors.cardBg,
    borderRadius: 20,
    borderWidth: 1,
    borderColor: Colors.border,
    padding: 20,
  },
  cardHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 16,
  },
  cardTitle: { fontSize: 17, fontWeight: '700', color: Colors.text },
  liveBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(46, 204, 113, 0.15)',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 20,
  },
  liveDot: { width: 6, height: 6, borderRadius: 3, backgroundColor: Colors.success, marginRight: 6 },
  liveText: { fontSize: 11, fontWeight: '700', color: Colors.success },

  searchBox: {
    backgroundColor: 'rgba(255,255,255,0.03)',
    borderRadius: 12,
    padding: 14,
    marginBottom: 16,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.06)',
  },
  searchIconRow: { flexDirection: 'row', alignItems: 'center' },
  searchStatusText: { fontSize: 13, color: Colors.textSecondary, flex: 1 },
  progressBarBg: {
    height: 4,
    backgroundColor: 'rgba(255,255,255,0.08)',
    borderRadius: 2,
    marginTop: 10,
    overflow: 'hidden',
  },
  progressBarFill: { height: '100%', backgroundColor: Colors.accent, borderRadius: 2 },

  autoSearchBtn: { borderRadius: 12, overflow: 'hidden', marginBottom: 12 },
  btnGrad: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 14,
  },
  btnText: { color: '#FFF', fontSize: 15, fontWeight: '700' },

  errorBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(231, 76, 60, 0.1)',
    borderRadius: 10,
    padding: 12,
    marginBottom: 14,
    gap: 8,
  },
  errorText: { color: Colors.danger, fontSize: 12, flex: 1 },

  accordionToggle: { alignItems: 'center', paddingVertical: 8 },
  accordionText: { fontSize: 12, color: Colors.textMuted, fontWeight: '600' },

  manualBox: { marginTop: 10, paddingTop: 12, borderTopWidth: 1, borderTopColor: Colors.border },
  fieldLabel: { fontSize: 11, fontWeight: '700', color: Colors.textMuted, textTransform: 'uppercase', marginBottom: 8 },
  inputRow: { flexDirection: 'row', gap: 10 },
  input: {
    flex: 1,
    backgroundColor: Colors.inputBg,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: Colors.border,
    paddingHorizontal: 14,
    paddingVertical: 10,
    color: Colors.text,
    fontSize: 13,
  },
  manualBtn: {
    backgroundColor: 'rgba(108,99,255,0.15)',
    paddingHorizontal: 16,
    borderRadius: 10,
    justifyContent: 'center',
    alignItems: 'center',
  },
  manualBtnText: { color: Colors.accent, fontSize: 13, fontWeight: '700' },

  connectedBox: {
    backgroundColor: 'rgba(255,255,255,0.03)',
    borderRadius: 12,
    padding: 14,
    marginBottom: 16,
    gap: 8,
  },
  deviceRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  deviceInfoText: { fontSize: 13, color: Colors.textSecondary },
  highlightText: { color: Colors.text, fontWeight: '700' },

  previewSection: { marginBottom: 16 },
  previewLabel: { fontSize: 11, fontWeight: '700', color: Colors.textMuted, textTransform: 'uppercase', marginBottom: 8 },

  btnRow: { flexDirection: 'row', gap: 10 },
  reScanBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(108,99,255,0.1)',
    borderRadius: 10,
    paddingVertical: 12,
  },
  reScanText: { color: Colors.accent, fontSize: 13, fontWeight: '600' },
  disconnectBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(231, 76, 60, 0.1)',
    borderRadius: 10,
    paddingVertical: 12,
  },
  disconnectText: { color: Colors.danger, fontSize: 13, fontWeight: '600' },
});
