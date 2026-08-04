// =============================================================================
//  settings.tsx  —  Ayarlar Ekranı
// =============================================================================

import React, { useState } from 'react';
import {
  View, Text, StyleSheet, ScrollView, TouchableOpacity,
  TextInput, Alert, Linking, Switch,
} from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import Animated, { FadeInDown } from 'react-native-reanimated';
import { Ionicons } from '@expo/vector-icons';
import * as Haptics from 'expo-haptics';
import { Colors, Gradients } from '@/constants/Colors';
import { useEsp32Context } from './index';

export default function SettingsScreen() {
  const esp32 = useEsp32Context();
  const [autoRefresh, setAutoRefresh] = useState(false);

  const handleWifiReset = () => {
    Alert.alert(
      '⚠️ WiFi Sıfırla',
      'Cihazın kayıtlı WiFi ayarları silinecek ve "IoT-LED-Setup" AP moduna geçecektir. Emin misiniz?',
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Sıfırla', style: 'destructive',
          onPress: async () => {
            await esp32.api?.reset();
            esp32.disconnect();
          },
        },
      ]
    );
  };

  const handleOtaCheck = async () => {
    if (!esp32.isConnected) return;
    Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
    await esp32.api?.checkUpdate();
    Alert.alert('OTA', 'Güncelleme kontrolü başlatıldı. Sonuç için seri monitörü takip edin.');
  };

  return (
    <LinearGradient colors={['#0D0E1A', '#13141F']} style={styles.root}>
      <ScrollView contentContainerStyle={styles.scroll} showsVerticalScrollIndicator={false}>

        {/* ── Başlık ──────────────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown} style={styles.header}>
          <Text style={styles.title}>Ayarlar</Text>
        </Animated.View>

        {/* ── Cihaz Bilgisi ──────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(50)} style={styles.section}>
          <Text style={styles.sectionLabel}>CİHAZ</Text>

          <View style={styles.card}>
            <Row
              icon="hardware-chip"
              label="Model"
              value="ESP32-C3 Super Mini"
            />
            <Divider />
            <Row
              icon="git-branch"
              label="Firmware"
              value={esp32.isConnected ? `v${esp32.status?.version}` : '—'}
            />
            <Divider />
            <Row
              icon="wifi"
              label="IP Adresi"
              value={esp32.isConnected ? esp32.status?.ip ?? '—' : '—'}
            />
            <Divider />
            <Row
              icon="cellular"
              label="SSID"
              value={esp32.isConnected ? esp32.status?.ssid ?? '—' : '—'}
            />
            <Divider />
            <Row
              icon="finger-print"
              label="MAC"
              value={esp32.isConnected ? esp32.status?.mac ?? '—' : '—'}
              mono
            />
          </View>
        </Animated.View>

        {/* ── Uygulama Tercihleri ────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(100)} style={styles.section}>
          <Text style={styles.sectionLabel}>UYGULAMA</Text>

          <View style={styles.card}>
            <View style={styles.row}>
              <View style={styles.rowLeft}>
                <Ionicons name="refresh" size={18} color={Colors.accent} />
                <View>
                  <Text style={styles.rowLabel}>Otomatik Yenile</Text>
                  <Text style={styles.rowSub}>5 sn'de bir durumu güncelle</Text>
                </View>
              </View>
              <Switch
                value={autoRefresh}
                onValueChange={setAutoRefresh}
                trackColor={{ false: Colors.border, true: Colors.accentDim }}
                thumbColor={autoRefresh ? Colors.accent : Colors.textMuted}
              />
            </View>
          </View>
        </Animated.View>

        {/* ── Cihaz İşlemleri ────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(150)} style={styles.section}>
          <Text style={styles.sectionLabel}>İŞLEMLER</Text>

          <View style={styles.card}>
            <ActionRow
              icon="cloud-download-outline"
              label="OTA Güncelleme Kontrol"
              sub="GitHub'dan yeni firmware kontrol et"
              color={Colors.info}
              onPress={handleOtaCheck}
              disabled={!esp32.isConnected}
            />
            <Divider />
            <ActionRow
              icon="wifi-outline"
              label="WiFi Ayarlarını Sıfırla"
              sub="Cihazı AP moduna geçir"
              color={Colors.warning}
              onPress={handleWifiReset}
              disabled={!esp32.isConnected}
            />
            <Divider />
            <ActionRow
              icon="close-circle-outline"
              label="Bağlantıyı Kes"
              sub="ESP32 bağlantısını sonlandır"
              color={Colors.danger}
              onPress={esp32.disconnect}
              disabled={!esp32.isConnected}
            />
          </View>
        </Animated.View>

        {/* ── Hakkında ────────────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(200)} style={styles.section}>
          <Text style={styles.sectionLabel}>HAKKINDA</Text>

          <View style={styles.card}>
            <ActionRow
              icon="logo-github"
              label="GitHub — LithoSync"
              sub="github.com/ksemiz/LithoSync"
              color={Colors.accent}
              onPress={() => Linking.openURL('https://github.com/ksemiz/LithoSync')}
            />
          </View>
        </Animated.View>

        {/* ── Footer ──────────────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(250)} style={styles.footer}>
          <LinearGradient colors={Gradients.accent} style={styles.footerDot} />
          <Text style={styles.footerText}>LithoSync v1.0.0</Text>
          <Text style={styles.footerSub}>ESP32-C3 + WS2812B IoT LED Controller</Text>
        </Animated.View>

      </ScrollView>
    </LinearGradient>
  );
}

// ─── Yardımcı bileşenler ──────────────────────────────────────────────────────
function Row({ icon, label, value, mono = false }: {
  icon: React.ComponentProps<typeof Ionicons>['name'];
  label: string; value: string; mono?: boolean;
}) {
  return (
    <View style={styles.row}>
      <View style={styles.rowLeft}>
        <Ionicons name={icon} size={18} color={Colors.accent} />
        <Text style={styles.rowLabel}>{label}</Text>
      </View>
      <Text style={[styles.rowValue, mono && styles.rowMono]} numberOfLines={1}>{value}</Text>
    </View>
  );
}

function ActionRow({ icon, label, sub, color, onPress, disabled }: {
  icon: React.ComponentProps<typeof Ionicons>['name'];
  label: string; sub: string; color: string;
  onPress: () => void; disabled?: boolean;
}) {
  return (
    <TouchableOpacity
      style={[styles.row, disabled && { opacity: 0.4 }]}
      onPress={onPress}
      disabled={disabled}
    >
      <View style={styles.rowLeft}>
        <Ionicons name={icon} size={18} color={color} />
        <View>
          <Text style={styles.rowLabel}>{label}</Text>
          {sub && <Text style={styles.rowSub}>{sub}</Text>}
        </View>
      </View>
      <Ionicons name="chevron-forward" size={16} color={Colors.textMuted} />
    </TouchableOpacity>
  );
}

function Divider() {
  return <View style={styles.divider} />;
}

// ─── Stiller ──────────────────────────────────────────────────────────────────
const styles = StyleSheet.create({
  root:   { flex: 1 },
  scroll: { padding: 20, paddingTop: 60, paddingBottom: 120 },

  header: { marginBottom: 24 },
  title:  { fontSize: 24, fontWeight: '800', color: Colors.textPrimary },

  section:      { marginBottom: 24 },
  sectionLabel: { fontSize: 11, fontWeight: '700', color: Colors.textMuted, letterSpacing: 1, textTransform: 'uppercase', marginBottom: 8, paddingLeft: 4 },

  card: {
    backgroundColor: Colors.bgCard,
    borderRadius: 14,
    overflow: 'hidden',
    borderWidth: 1,
    borderColor: Colors.border,
  },

  row:      { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', padding: 14, gap: 10 },
  rowLeft:  { flexDirection: 'row', alignItems: 'center', gap: 12, flex: 1 },
  rowLabel: { color: Colors.textPrimary, fontSize: 14, fontWeight: '500' },
  rowSub:   { color: Colors.textMuted, fontSize: 11, marginTop: 1 },
  rowValue: { color: Colors.textSecondary, fontSize: 13 },
  rowMono:  { fontFamily: 'monospace', fontSize: 11 },

  divider: { height: 1, backgroundColor: Colors.border, marginLeft: 46 },

  footer:    { alignItems: 'center', gap: 6, paddingTop: 8 },
  footerDot: { width: 10, height: 10, borderRadius: 5 },
  footerText: { color: Colors.textSecondary, fontSize: 13, fontWeight: '600' },
  footerSub:  { color: Colors.textMuted, fontSize: 11 },
});
