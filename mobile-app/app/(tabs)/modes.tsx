// =============================================================================
//  modes.tsx  —  LED Mod Seçim Ekranı
//  Statik / Knight Rider / Şimşek / UDP Ambilight
// =============================================================================

import React, { useCallback } from 'react';
import {
  View, Text, StyleSheet, ScrollView, TouchableOpacity,
} from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import Animated, {
  FadeInDown, useSharedValue, useAnimatedStyle,
  withSpring, withTiming,
} from 'react-native-reanimated';
import { Ionicons } from '@expo/vector-icons';
import * as Haptics from 'expo-haptics';
import { Colors, Gradients } from '@/constants/Colors';
import { LedPreview } from '@/components/LedPreview';
import { MODE_NAMES, MODE_ICONS, MODE_DESCRIPTIONS } from '@/constants/api';
import { useEsp32Context } from './index';

// Her mod için ek görsel bilgi
const MODE_COLORS = [
  ['#6C63FF', '#4A44B0'],   // Statik — mor
  ['#E74C3C', '#C0392B'],   // Knight Rider — kırmızı
  ['#3498DB', '#2980B9'],   // Şimşek — mavi
  ['#2ECC71', '#27AE60'],   // UDP — yeşil
] as [string, string][];

const MODE_PREVIEW_LEDS = [
  // Statik — gökkuşağı
  [
    { r: 255, g: 0,   b: 0   },
    { r: 255, g: 128, b: 0   },
    { r: 255, g: 255, b: 0   },
    { r: 0,   g: 255, b: 0   },
    { r: 0,   g: 128, b: 255 },
    { r: 128, g: 0,   b: 255 },
  ],
  // Knight Rider — kırmızı kayan
  [
    { r: 0,   g: 0, b: 0 },
    { r: 40,  g: 0, b: 0 },
    { r: 120, g: 0, b: 0 },
    { r: 255, g: 0, b: 0 },
    { r: 120, g: 0, b: 0 },
    { r: 0,   g: 0, b: 0 },
  ],
  // Şimşek — mavi/beyaz
  [
    { r: 0,   g: 0,   b: 0   },
    { r: 0,   g: 0,   b: 0   },
    { r: 80,  g: 130, b: 255 },
    { r: 210, g: 225, b: 255 },
    { r: 0,   g: 0,   b: 0   },
    { r: 0,   g: 0,   b: 0   },
  ],
  // UDP — yeşil nabız
  [
    { r: 0,  g: 80,  b: 0 },
    { r: 0,  g: 160, b: 0 },
    { r: 0,  g: 255, b: 0 },
    { r: 0,  g: 255, b: 0 },
    { r: 0,  g: 160, b: 0 },
    { r: 0,  g: 80,  b: 0 },
  ],
];

export default function ModesScreen() {
  const esp32 = useEsp32Context();
  const currentMode = esp32.status?.mode ?? 0;

  const handleModeSelect = useCallback(async (mode: number) => {
    if (!esp32.isConnected) return;
    Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium);
    await esp32.setMode(mode);
  }, [esp32]);

  if (!esp32.isConnected) {
    return (
      <LinearGradient colors={['#0D0E1A', '#13141F']} style={styles.notConn}>
        <Text style={{ fontSize: 64 }}>🎛️</Text>
        <Text style={styles.notConnTitle}>Bağlı Değil</Text>
        <Text style={styles.notConnSub}>Mod seçimi için ESP32'ye bağlanın.</Text>
      </LinearGradient>
    );
  }

  return (
    <LinearGradient colors={['#0D0E1A', '#13141F']} style={styles.root}>
      <ScrollView contentContainerStyle={styles.scroll} showsVerticalScrollIndicator={false}>

        {/* ── Başlık ──────────────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown} style={styles.header}>
          <Text style={styles.title}>LED Modları</Text>
          <View style={styles.currentBadge}>
            <Text style={styles.currentBadgeText}>
              Aktif: {MODE_ICONS[currentMode]} {MODE_NAMES[currentMode]}
            </Text>
          </View>
        </Animated.View>

        {/* ── Mod Kartları ────────────────────────────────────────────── */}
        {MODE_NAMES.map((name, i) => (
          <ModeCard
            key={i}
            index={i}
            name={name}
            icon={MODE_ICONS[i]}
            description={MODE_DESCRIPTIONS[i]}
            gradientColors={MODE_COLORS[i]}
            previewLeds={MODE_PREVIEW_LEDS[i]}
            isActive={currentMode === i}
            onPress={() => handleModeSelect(i)}
          />
        ))}

        {/* ── Bilgi Kutusu ─────────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(400)} style={styles.infoBox}>
          <Ionicons name="information-circle" size={20} color={Colors.info} />
          <Text style={styles.infoText}>
            <Text style={{ fontWeight: '700', color: Colors.textPrimary }}>UDP / Ambilight modu </Text>
            seçildiğinde ESP32 animasyon üretmez. Renkleri PC uygulamasındaki
            AmbiLight veya Ses Analizi servisi gönderir.
          </Text>
        </Animated.View>

      </ScrollView>
    </LinearGradient>
  );
}

// ─── Mod Kartı Bileşeni ───────────────────────────────────────────────────────
interface ModeCardProps {
  index: number;
  name: string;
  icon: string;
  description: string;
  gradientColors: [string, string];
  previewLeds: Array<{ r: number; g: number; b: number }>;
  isActive: boolean;
  onPress: () => void;
}

function ModeCard({ index, name, icon, description, gradientColors, previewLeds, isActive, onPress }: ModeCardProps) {
  const scale = useSharedValue(1);

  const animStyle = useAnimatedStyle(() => ({
    transform: [{ scale: scale.value }],
  }));

  return (
    <Animated.View entering={FadeInDown.delay(index * 80)} style={animStyle}>
      <TouchableOpacity
        onPress={onPress}
        onPressIn={() => { scale.value = withSpring(0.97); }}
        onPressOut={() => { scale.value = withSpring(1); }}
        activeOpacity={1}
      >
        <View style={[styles.modeCard, isActive && styles.modeCardActive]}>

          {/* Sol gradient şerit */}
          <LinearGradient
            colors={gradientColors}
            style={styles.modeStripe}
            start={{ x: 0, y: 0 }}
            end={{ x: 0, y: 1 }}
          />

          <View style={styles.modeContent}>
            {/* Üst kısım */}
            <View style={styles.modeHeader}>
              <View style={styles.modeIconBadge}>
                <Text style={styles.modeIconText}>{icon}</Text>
              </View>
              <View style={styles.modeMeta}>
                <Text style={styles.modeName}>{name}</Text>
                <Text style={styles.modeDesc}>{description}</Text>
              </View>
              {isActive && (
                <View style={styles.activeBadge}>
                  <LinearGradient colors={gradientColors} style={styles.activeBadgeGrad}>
                    <Text style={styles.activeBadgeText}>Aktif</Text>
                  </LinearGradient>
                </View>
              )}
            </View>

            {/* LED Önizleme */}
            <View style={styles.modePreview}>
              <LedPreview leds={previewLeds} size="sm" />
            </View>
          </View>
        </View>
      </TouchableOpacity>
    </Animated.View>
  );
}

// ─── Stiller ──────────────────────────────────────────────────────────────────
const styles = StyleSheet.create({
  root:   { flex: 1 },
  scroll: { padding: 20, paddingTop: 60, paddingBottom: 120 },

  header:       { marginBottom: 24 },
  title:        { fontSize: 24, fontWeight: '800', color: Colors.textPrimary, marginBottom: 10 },
  currentBadge: { alignSelf: 'flex-start', backgroundColor: Colors.accentSoft, borderRadius: 8, paddingHorizontal: 12, paddingVertical: 6, borderWidth: 1, borderColor: Colors.accent },
  currentBadgeText: { color: Colors.accent, fontWeight: '700', fontSize: 13 },

  modeCard: {
    flexDirection: 'row',
    backgroundColor: Colors.bgCard,
    borderRadius: 16,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: Colors.border,
    overflow: 'hidden',
  },
  modeCardActive: {
    borderColor: Colors.accent,
    shadowColor: Colors.accent,
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
    elevation: 4,
  },
  modeStripe:   { width: 4 },
  modeContent:  { flex: 1, padding: 16 },
  modeHeader:   { flexDirection: 'row', alignItems: 'flex-start', gap: 12, marginBottom: 12 },
  modeIconBadge: { width: 44, height: 44, borderRadius: 12, backgroundColor: Colors.bgDeep, justifyContent: 'center', alignItems: 'center', borderWidth: 1, borderColor: Colors.border },
  modeIconText: { fontSize: 22 },
  modeMeta:     { flex: 1 },
  modeName:     { fontSize: 15, fontWeight: '700', color: Colors.textPrimary, marginBottom: 3 },
  modeDesc:     { fontSize: 12, color: Colors.textSecondary, lineHeight: 17 },
  activeBadge:  { borderRadius: 8, overflow: 'hidden', alignSelf: 'flex-start' },
  activeBadgeGrad: { paddingHorizontal: 10, paddingVertical: 4 },
  activeBadgeText: { color: '#FFF', fontSize: 10, fontWeight: '700' },
  modePreview:  { backgroundColor: Colors.bgDeep, borderRadius: 10, paddingVertical: 8, borderWidth: 1, borderColor: Colors.border },

  infoBox: {
    flexDirection: 'row',
    gap: 10,
    backgroundColor: 'rgba(52,152,219,0.1)',
    borderRadius: 12,
    padding: 14,
    borderWidth: 1,
    borderColor: 'rgba(52,152,219,0.3)',
    alignItems: 'flex-start',
  },
  infoText: { color: Colors.textSecondary, fontSize: 12, lineHeight: 18, flex: 1 },

  notConn:      { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 40 },
  notConnTitle: { fontSize: 22, fontWeight: '700', color: Colors.textPrimary, marginTop: 12, marginBottom: 8 },
  notConnSub:   { color: Colors.textSecondary, fontSize: 14, textAlign: 'center', lineHeight: 22 },
});
