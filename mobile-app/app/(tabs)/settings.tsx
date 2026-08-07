// =============================================================================
//  settings.tsx  —  Ayarlar Ekranı (Mobil Güncelleme + Cihaz OTA)
// =============================================================================

import React, { useState } from 'react';
import {
  View, Text, StyleSheet, ScrollView, TouchableOpacity,
  Alert, Linking, Switch, ActivityIndicator,
} from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import Animated, { FadeInDown } from 'react-native-reanimated';
import { Ionicons } from '@expo/vector-icons';
import * as Haptics from 'expo-haptics';
import { Colors } from '@/constants/Colors';
import { useEsp32Context } from './index';
import { CURRENT_MOBILE_VERSION, checkForMobileUpdate, startMobileUpdate, MobileRelease } from '@/services/updater';

export default function SettingsScreen() {
  const esp32 = useEsp32Context();
  const [autoRefresh, setAutoRefresh] = useState(false);
  const [checkingUpdate, setCheckingUpdate] = useState(false);
  const [latestRelease, setLatestRelease] = useState<MobileRelease | null>(esp32.availableUpdate);

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
    Alert.alert('OTA Firmware', 'ESP32 firmware güncelleme kontrolü başlatıldı.');
  };

  const handleCheckMobileUpdate = async () => {
    setCheckingUpdate(true);
    Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
    const release = await checkForMobileUpdate();
    setCheckingUpdate(false);

    if (release) {
      setLatestRelease(release);
      if (release.hasUpdate) {
        Alert.alert(
          '🎉 Yeni Sürüm Mevcut!',
          `Mobil Uygulama ${release.version} yayında.\n\nYenilikler:\n${release.body || 'Performans iyileştirmeleri ve hata düzeltmeleri.'}`,
          [
            { text: 'Daha Sonra', style: 'cancel' },
            { text: 'İndir ve Güncelle', onPress: () => startMobileUpdate(release) },
          ]
        );
      } else {
        Alert.alert('✅ Güncel', `Mobil uygulamanız en son sürümde (v${CURRENT_MOBILE_VERSION}).`);
      }
    } else {
      Alert.alert('Bağlantı Hatası', 'GitHub Releases kontrol edilemedi. İnternet bağlantınızı kontrol edin.');
    }
  };

  return (
    <LinearGradient colors={['#0D0E1A', '#13141F']} style={styles.root}>
      <ScrollView contentContainerStyle={styles.scroll} showsVerticalScrollIndicator={false}>

        {/* ── Başlık ──────────────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown} style={styles.header}>
          <Text style={styles.title}>Ayarlar</Text>
        </Animated.View>

        {/* ── Mobil Uygulama Güncellemesi ────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(30)} style={styles.section}>
          <Text style={styles.sectionLabel}>MOBİL UYGULAMA</Text>
          <View style={styles.card}>
            <Row
              icon="phone-portrait-outline"
              label="Mevcut Sürüm"
              value={`v${CURRENT_MOBILE_VERSION}`}
            />
            <Divider />
            <ActionRow
              icon="cloud-download-outline"
              label="GitHub Güncelleme Kontrol"
              sub={latestRelease?.hasUpdate ? `Yeni sürüm mevcut: ${latestRelease.version}` : 'GitHub Releases API ile kontrol et'}
              color={Colors.accent}
              onPress={handleCheckMobileUpdate}
              loading={checkingUpdate}
            />
            {latestRelease?.hasUpdate && (
              <>
                <Divider />
                <ActionRow
                  icon="arrow-down-circle"
                  label="APK İndir ve Kur"
                  sub={`${latestRelease.version} sürümünü doğrudan indir`}
                  color={Colors.success}
                  onPress={() => startMobileUpdate(latestRelease)}
                />
              </>
            )}
          </View>
        </Animated.View>

        {/* ── Cihaz Bilgisi ──────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(60)} style={styles.section}>
          <Text style={styles.sectionLabel}>ESP32 CİHAZ</Text>
          <View style={styles.card}>
            <Row
              icon="hardware-chip-outline"
              label="Model"
              value="ESP32-C3 Super Mini"
            />
            <Divider />
            <Row
              icon="git-branch-outline"
              label="Firmware Sürümü"
              value={esp32.isConnected ? `v${esp32.status?.version || '1.0.0'}` : '—'}
            />
            <Divider />
            <Row
              icon="wifi-outline"
              label="IP Adresi"
              value={esp32.isConnected ? esp32.status?.ip ?? '—' : '—'}
            />
            <Divider />
            <Row
              icon="cellular-outline"
              label="Wi-Fi SSID"
              value={esp32.isConnected ? esp32.status?.ssid ?? '—' : '—'}
            />
            <Divider />
            <Row
              icon="finger-print-outline"
              label="MAC Adresi"
              value={esp32.isConnected ? esp32.status?.mac ?? '—' : '—'}
              mono
            />
          </View>
        </Animated.View>

        {/* ── Uygulama Tercihleri ────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(90)} style={styles.section}>
          <Text style={styles.sectionLabel}>TERCİHLER</Text>
          <View style={styles.card}>
            <View style={styles.row}>
              <View style={styles.rowLeft}>
                <Ionicons name="refresh" size={18} color={Colors.accent} />
                <View>
                  <Text style={styles.rowLabel}>Otomatik Yenile</Text>
                  <Text style={styles.rowSub}>5 sn'de bir canlı LED durumunu güncelle</Text>
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
        <Animated.View entering={FadeInDown.delay(120)} style={styles.section}>
          <Text style={styles.sectionLabel}>CİHAZ İŞLEMLERİ</Text>
          <View style={styles.card}>
            <ActionRow
              icon="cloud-upload-outline"
              label="ESP32 OTA Güncelleme Kontrol"
              sub="GitHub'dan yeni firmware.bin kontrol et"
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

        {/* ── GitHub Repo Linki ──────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(150)} style={styles.section}>
          <Text style={styles.sectionLabel}>HAKKINDA</Text>
          <View style={styles.card}>
            <ActionRow
              icon="logo-github"
              label="GitHub Deposu"
              sub="github.com/ksemiz/LithoSync"
              color={Colors.text}
              onPress={() => Linking.openURL('https://github.com/ksemiz/LithoSync')}
            />
          </View>
        </Animated.View>

      </ScrollView>
    </LinearGradient>
  );
}

function Row({ icon, label, value, mono }: { icon: any; label: string; value: string; mono?: boolean }) {
  return (
    <View style={styles.row}>
      <View style={styles.rowLeft}>
        <Ionicons name={icon} size={18} color={Colors.textMuted} />
        <Text style={styles.rowLabel}>{label}</Text>
      </View>
      <Text style={[styles.rowValue, mono && styles.mono]}>{value}</Text>
    </View>
  );
}

function ActionRow({
  icon, label, sub, color, onPress, disabled, loading,
}: {
  icon: any; label: string; sub?: string; color: string;
  onPress: () => void; disabled?: boolean; loading?: boolean;
}) {
  return (
    <TouchableOpacity
      style={[styles.row, disabled && styles.rowDisabled]}
      onPress={onPress}
      disabled={disabled || loading}
      activeOpacity={0.7}
    >
      <View style={styles.rowLeft}>
        {loading ? (
          <ActivityIndicator size="small" color={color} style={{ width: 18, height: 18 }} />
        ) : (
          <Ionicons name={icon} size={18} color={disabled ? Colors.textMuted : color} />
        )}
        <View>
          <Text style={[styles.rowLabel, { color: disabled ? Colors.textMuted : color }]}>{label}</Text>
          {sub ? <Text style={styles.rowSub}>{sub}</Text> : null}
        </View>
      </View>
      <Ionicons name="chevron-forward" size={16} color={Colors.textMuted} />
    </TouchableOpacity>
  );
}

function Divider() {
  return <View style={styles.divider} />;
}

const styles = StyleSheet.create({
  root: { flex: 1 },
  scroll: { padding: 24, paddingBottom: 60 },
  header: { marginBottom: 24, marginTop: 10 },
  title: { fontSize: 28, fontWeight: '800', color: Colors.text, letterSpacing: -0.5 },
  section: { marginBottom: 20 },
  sectionLabel: {
    fontSize: 11,
    fontWeight: '700',
    color: Colors.textMuted,
    letterSpacing: 0.8,
    marginBottom: 8,
    marginLeft: 4,
  },
  card: {
    backgroundColor: Colors.cardBg,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: Colors.border,
    paddingVertical: 4,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 14,
  },
  rowDisabled: { opacity: 0.4 },
  rowLeft: { flexDirection: 'row', alignItems: 'center', gap: 12, flex: 1 },
  rowLabel: { fontSize: 14, fontWeight: '600', color: Colors.text },
  rowSub: { fontSize: 11, color: Colors.textMuted, marginTop: 2 },
  rowValue: { fontSize: 13, color: Colors.textSecondary, fontWeight: '500' },
  mono: { fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace', fontSize: 11 },
  divider: { height: 1, backgroundColor: Colors.border, marginHorizontal: 16 },
});
