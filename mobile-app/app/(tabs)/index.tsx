// =============================================================================
//  index.tsx  —  Bağlantı Ekranı
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
import { MODE_NAMES, MODE_ICONS } from '@/constants/api';

// ─── Global ESP32 Context (tüm tab'lar erişebilir) ───────────────────────────
export const Esp32Context = createContext<ReturnType<typeof useEsp32> | null>(null);
export const useEsp32Context = () => {
  const ctx = useContext(Esp32Context);
  if (!ctx) throw new Error('Esp32Context bulunamadı');
  return ctx;
};

// ─── Ana Bağlantı Ekranı ─────────────────────────────────────────────────────
export default function ConnectScreen() {
  const esp32 = useEsp32();
  const [inputIp, setInputIp] = useState('192.168.1.');

  const handleConnect = useCallback(async () => {
    if (!inputIp.trim()) return;
    Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
    const ok = await esp32.connect(inputIp.trim());
    if (ok) Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
    else     Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
  }, [inputIp, esp32]);

  const handleDisconnect = useCallback(() => {
    Alert.alert('Bağlantıyı Kes', 'ESP32 bağlantısını kesmek istiyor musunuz?', [
      { text: 'İptal', style: 'cancel' },
      { text: 'Evet', onPress: () => esp32.disconnect(), style: 'destructive' },
    ]);
  }, [esp32]);

  const handleReset = useCallback(() => {
    Alert.alert('WiFi Sıfırla', 'Cihazın WiFi ayarları silinecek ve AP moduna geçecek. Emin misiniz?', [
      { text: 'İptal', style: 'cancel' },
      { text: 'Sıfırla', style: 'destructive', onPress: () => esp32.api?.reset() },
    ]);
  }, [esp32]);

  return (
    <Esp32Context.Provider value={esp32}>
      <LinearGradient colors={['#0D0E1A', '#13141F']} style={styles.root}>
        <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : 'height'} style={styles.kav}>
          <ScrollView contentContainerStyle={styles.scroll} showsVerticalScrollIndicator={false}>

            {/* ── Başlık ──────────────────────────────────────────────── */}
            <View style={styles.header}>
              <View style={styles.logoRow}>
                <LinearGradient colors={Gradients.accent} style={styles.logoDot} />
                <Text style={styles.logoText}>LithoSync</Text>
              </View>
              <Text style={styles.subtitle}>IoT LED Controller</Text>
            </View>

            {/* ── Bağlantı Kartı ──────────────────────────────────────── */}
            <View style={styles.card}>
              <Text style={styles.cardTitle}>ESP32 Bağlantısı</Text>

              {!esp32.isConnected ? (
                <>
                  <Text style={styles.fieldLabel}>IP Adresi</Text>
                  <View style={styles.inputRow}>
                    <TextInput
                      style={styles.input}
                      value={inputIp}
                      onChangeText={setInputIp}
                      placeholder="192.168.1.100"
                      placeholderTextColor={Colors.textMuted}
                      keyboardType="decimal-pad"
                      returnKeyType="done"
                      onSubmitEditing={handleConnect}
                      autoCapitalize="none"
                    />
                    <TouchableOpacity
                      style={[styles.connectBtn, esp32.connectionState === 'connecting' && styles.btnDisabled]}
                      onPress={handleConnect}
                      disabled={esp32.connectionState === 'connecting'}
                    >
                      <LinearGradient colors={Gradients.accent} style={styles.connectBtnGrad}>
                        {esp32.connectionState === 'connecting'
                          ? <ActivityIndicator color="#FFF" size="small" />
                          : <Ionicons name="wifi" size={20} color="#FFF" />
                        }
                      </LinearGradient>
                    </TouchableOpacity>
                  </View>

                  {esp32.error && (
                    <View style={styles.errorBadge}>
                      <Ionicons name="alert-circle" size={14} color={Colors.danger} />
                      <Text style={styles.errorText}>{esp32.error}</Text>
                    </View>
                  )}

                  <Text style={styles.hint}>
                    💡 ESP32'nin IP adresini router admin panelinden veya seri monitörden bulabilirsiniz.{'\n'}
                    mDNS destekleniyorsa: <Text style={styles.hintAccent}>iot-led.local</Text> yazın.
                  </Text>
                </>
              ) : (
                <>
                  {/* ── Bağlı Durum ─────────────────────────────────── */}
                  <View style={styles.connectedBadge}>
                    <View style={styles.connectedDot} />
                    <Text style={styles.connectedText}>Bağlandı — {esp32.status?.ip}</Text>
                  </View>

                  {/* Cihaz Bilgileri */}
                  <View style={styles.infoGrid}>
                    <InfoItem icon="code-slash" label="Versiyon" value={`v${esp32.status?.version ?? '?'}`} />
                    <InfoItem icon="wifi"       label="SSID"     value={esp32.status?.ssid ?? '?'} />
                    <InfoItem icon="flash"      label="Mod"      value={`${MODE_ICONS[esp32.status?.mode ?? 0]} ${MODE_NAMES[esp32.status?.mode ?? 0]}`} />
                    <InfoItem icon="time"       label="Uptime"   value={`${Math.floor((esp32.status?.uptime ?? 0) / 60)} dk`} />
                  </View>

                  {/* LED Önizleme */}
                  <Text style={styles.fieldLabel}>LED Durumu</Text>
                  <LedPreview
                    leds={esp32.status?.leds ?? Array(6).fill({ r: 0, g: 0, b: 0 })}
                    size="md"
                  />

                  {/* Aksiyon butonları */}
                  <View style={styles.actionRow}>
                    <TouchableOpacity style={styles.refreshBtn} onPress={esp32.refreshStatus}>
                      <Ionicons name="refresh" size={16} color={Colors.accent} />
                      <Text style={styles.refreshText}>Yenile</Text>
                    </TouchableOpacity>
                    <TouchableOpacity style={styles.otaBtn} onPress={() => esp32.api?.checkUpdate()}>
                      <Ionicons name="cloud-download-outline" size={16} color={Colors.info} />
                      <Text style={[styles.refreshText, { color: Colors.info }]}>OTA</Text>
                    </TouchableOpacity>
                    <TouchableOpacity style={styles.disconnectBtn} onPress={handleDisconnect}>
                      <Ionicons name="close-circle-outline" size={16} color={Colors.danger} />
                      <Text style={[styles.refreshText, { color: Colors.danger }]}>Kes</Text>
                    </TouchableOpacity>
                  </View>
                </>
              )}
            </View>

            {/* ── Hızlı Bilgi Kartı ───────────────────────────────────── */}
            {!esp32.isConnected && (
              <View style={[styles.card, styles.infoCard]}>
                <Text style={styles.cardTitle}>Kurulum</Text>
                <Step num={1} text="ESP32'yi USB ile bilgisayara bağlayın ve firmware'i yükleyin." />
                <Step num={2} text="LED'lerin mavi yandığını görünce telefon WiFi'ından 'IoT-LED-Setup' ağına bağlanın (Şifre: 12345678)." />
                <Step num={3} text="Açılan captive portal'dan ev WiFi bilgilerinizi girin." />
                <Step num={4} text="LED'ler 3 kez yeşil yanıp söndükten sonra IP adresini buraya girin." />
              </View>
            )}
          </ScrollView>
        </KeyboardAvoidingView>
      </LinearGradient>
    </Esp32Context.Provider>
  );
}

// ── Yardımcı bileşenler ────────────────────────────────────────────────────────
function InfoItem({ icon, label, value }: { icon: React.ComponentProps<typeof Ionicons>['name']; label: string; value: string }) {
  return (
    <View style={infoStyles.item}>
      <Ionicons name={icon} size={14} color={Colors.accent} />
      <Text style={infoStyles.label}>{label}</Text>
      <Text style={infoStyles.value} numberOfLines={1}>{value}</Text>
    </View>
  );
}

function Step({ num, text }: { num: number; text: string }) {
  return (
    <View style={stepStyles.row}>
      <LinearGradient colors={Gradients.accent} style={stepStyles.badge}>
        <Text style={stepStyles.num}>{num}</Text>
      </LinearGradient>
      <Text style={stepStyles.text}>{text}</Text>
    </View>
  );
}

// ── Stiller ────────────────────────────────────────────────────────────────────
const styles = StyleSheet.create({
  root:     { flex: 1 },
  kav:      { flex: 1 },
  scroll:   { padding: 20, paddingTop: 60, paddingBottom: 120 },
  header:   { marginBottom: 28, alignItems: 'center' },
  logoRow:  { flexDirection: 'row', alignItems: 'center', gap: 10, marginBottom: 6 },
  logoDot:  { width: 14, height: 14, borderRadius: 7 },
  logoText: { fontSize: 28, fontWeight: '800', color: Colors.textPrimary, letterSpacing: -0.5 },
  subtitle: { color: Colors.textSecondary, fontSize: 13 },

  card: {
    backgroundColor: Colors.bgCard,
    borderRadius: 16,
    padding: 20,
    marginBottom: 16,
    borderWidth: 1,
    borderColor: Colors.border,
  },
  infoCard: { backgroundColor: Colors.bgPanel },
  cardTitle: { fontSize: 16, fontWeight: '700', color: Colors.textPrimary, marginBottom: 16 },
  fieldLabel: { color: Colors.textSecondary, fontSize: 11, fontWeight: '600', marginBottom: 8, textTransform: 'uppercase', letterSpacing: 0.5 },

  inputRow: { flexDirection: 'row', gap: 10, marginBottom: 12 },
  input: {
    flex: 1,
    backgroundColor: Colors.bgDeep,
    borderWidth: 1,
    borderColor: Colors.border,
    borderRadius: 10,
    padding: 12,
    color: Colors.textPrimary,
    fontSize: 15,
    fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace',
  },
  connectBtn:     { borderRadius: 10, overflow: 'hidden' },
  btnDisabled:    { opacity: 0.6 },
  connectBtnGrad: { width: 48, height: 48, justifyContent: 'center', alignItems: 'center' },

  errorBadge: { flexDirection: 'row', alignItems: 'center', gap: 6, marginBottom: 12, backgroundColor: 'rgba(231,76,60,0.1)', padding: 10, borderRadius: 8 },
  errorText:  { color: Colors.danger, fontSize: 12, flex: 1 },

  hint:      { color: Colors.textMuted, fontSize: 12, lineHeight: 18 },
  hintAccent: { color: Colors.accent, fontWeight: '600' },

  connectedBadge: { flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 20, backgroundColor: 'rgba(46,204,113,0.1)', padding: 10, borderRadius: 8 },
  connectedDot:   { width: 8, height: 8, borderRadius: 4, backgroundColor: Colors.success },
  connectedText:  { color: Colors.success, fontWeight: '600', fontSize: 13 },

  infoGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 10, marginBottom: 20 },

  actionRow:    { flexDirection: 'row', gap: 10, marginTop: 16 },
  refreshBtn:   { flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, backgroundColor: Colors.accentSoft, borderRadius: 8, padding: 10, borderWidth: 1, borderColor: Colors.accent },
  otaBtn:       { flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, backgroundColor: 'rgba(52,152,219,0.1)', borderRadius: 8, padding: 10, borderWidth: 1, borderColor: Colors.info },
  disconnectBtn: { flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, backgroundColor: 'rgba(231,76,60,0.1)', borderRadius: 8, padding: 10, borderWidth: 1, borderColor: Colors.danger },
  refreshText:  { color: Colors.accent, fontSize: 12, fontWeight: '600' },
});

const infoStyles = StyleSheet.create({
  item:  { flex: 1, minWidth: '45%', backgroundColor: Colors.bgDeep, borderRadius: 10, padding: 12, gap: 4, borderWidth: 1, borderColor: Colors.border },
  label: { color: Colors.textMuted, fontSize: 10, fontWeight: '600', textTransform: 'uppercase' },
  value: { color: Colors.textPrimary, fontSize: 13, fontWeight: '600' },
});

const stepStyles = StyleSheet.create({
  row:   { flexDirection: 'row', gap: 12, marginBottom: 14, alignItems: 'flex-start' },
  badge: { width: 24, height: 24, borderRadius: 12, justifyContent: 'center', alignItems: 'center', flexShrink: 0 },
  num:   { color: '#FFF', fontSize: 12, fontWeight: '700' },
  text:  { color: Colors.textSecondary, fontSize: 13, lineHeight: 20, flex: 1 },
});
