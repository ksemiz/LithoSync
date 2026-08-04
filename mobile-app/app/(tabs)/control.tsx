// =============================================================================
//  control.tsx  —  LED Kontrol Ekranı
//  • Renk seçici (SVG HSV wheel)
//  • Parlaklık slider
//  • Bireysel LED seçimi
//  • Canlı önizleme
// =============================================================================

import React, { useState, useCallback } from 'react';
import {
  View, Text, StyleSheet, ScrollView, TouchableOpacity,
  Platform, Switch,
} from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import Animated, { FadeIn, FadeInDown } from 'react-native-reanimated';
import * as Haptics from 'expo-haptics';
import { Colors, Gradients } from '@/constants/Colors';
import { ColorPicker } from '@/components/ColorPicker';
import { LedPreview } from '@/components/LedPreview';
import { hsvToRgb, RgbColor } from '@/constants/api';
import { useEsp32Context } from './index';

export default function ControlScreen() {
  const esp32 = useEsp32Context();

  // Renk seçici durumu
  const [hue,        setHue]        = useState(0);
  const [saturation, setSaturation] = useState(1);
  const [value,      setValue]      = useState(1);

  // Uygulama modu: 'global' (tüm LED) veya 'individual' (tekli)
  const [mode,        setMode]        = useState<'global' | 'individual'>('global');
  const [selectedLed, setSelectedLed] = useState(0);

  // Parlaklık (0-255)
  const [brightness, setBrightnessLocal] = useState(
    esp32.status?.brightness ?? 80
  );

  // Renk picker değişiminde
  const handleColorChange = useCallback((h: number, s: number, v: number) => {
    setHue(h); setSaturation(s); setValue(v);
  }, []);

  // Parmak bırakıldığında ESP32'ye gönder
  const handleColorRelease = useCallback(async (color: RgbColor) => {
    if (!esp32.isConnected) return;
    Haptics.selectionAsync();
    if (mode === 'global') {
      await esp32.setGlobalColor(color);
    } else {
      await esp32.setLedColor(selectedLed, color);
    }
  }, [esp32, mode, selectedLed]);

  // Parlaklık uygula
  const applyBrightness = useCallback(async () => {
    if (!esp32.isConnected) return;
    Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
    await esp32.setBrightness(brightness);
  }, [esp32, brightness]);

  // Hızlı renk presetleri
  const PRESETS: RgbColor[] = [
    { r: 255, g: 0,   b: 0   },  // Kırmızı
    { r: 255, g: 128, b: 0   },  // Turuncu
    { r: 255, g: 255, b: 0   },  // Sarı
    { r: 0,   g: 255, b: 0   },  // Yeşil
    { r: 0,   g: 128, b: 255 },  // Mavi
    { r: 128, g: 0,   b: 255 },  // Mor
    { r: 255, g: 255, b: 255 },  // Beyaz
    { r: 0,   g: 0,   b: 0   },  // Siyah (Kapat)
  ];

  const applyPreset = useCallback(async (color: RgbColor) => {
    Haptics.selectionAsync();
    if (mode === 'global') await esp32.setGlobalColor(color);
    else await esp32.setLedColor(selectedLed, color);
  }, [esp32, mode, selectedLed]);

  if (!esp32.isConnected) {
    return <NotConnected />;
  }

  return (
    <LinearGradient colors={['#0D0E1A', '#13141F']} style={styles.root}>
      <ScrollView
        contentContainerStyle={styles.scroll}
        showsVerticalScrollIndicator={false}
      >
        {/* ── Başlık ────────────────────────────────────────────────── */}
        <Animated.View entering={FadeIn} style={styles.header}>
          <Text style={styles.title}>LED Kontrol</Text>
          <Text style={styles.subtitle}>{esp32.status?.ip}</Text>
        </Animated.View>

        {/* ── LED Önizleme ──────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(50)} style={styles.card}>
          <LedPreview
            leds={esp32.status?.leds ?? Array(6).fill({ r: 0, g: 0, b: 0 })}
            selectedIndex={mode === 'individual' ? selectedLed : undefined}
            size="lg"
          />
        </Animated.View>

        {/* ── Global / Bireysel Mod Toggle ──────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(100)} style={styles.card}>
          <View style={styles.toggleRow}>
            <Text style={styles.toggleLabel}>Bireysel LED Modu</Text>
            <Switch
              value={mode === 'individual'}
              onValueChange={v => setMode(v ? 'individual' : 'global')}
              trackColor={{ false: Colors.border, true: Colors.accentDim }}
              thumbColor={mode === 'individual' ? Colors.accent : Colors.textMuted}
            />
          </View>

          {mode === 'individual' && (
            <View style={styles.ledSelector}>
              {Array.from({ length: 6 }, (_, i) => (
                <TouchableOpacity
                  key={i}
                  style={[styles.ledBtn, selectedLed === i && styles.ledBtnActive]}
                  onPress={() => { setSelectedLed(i); Haptics.selectionAsync(); }}
                >
                  <View
                    style={[
                      styles.ledBtnDot,
                      {
                        backgroundColor: esp32.status?.leds[i]
                          ? `rgb(${esp32.status.leds[i].r},${esp32.status.leds[i].g},${esp32.status.leds[i].b})`
                          : Colors.bgDeep,
                      },
                    ]}
                  />
                  <Text style={[styles.ledBtnLabel, selectedLed === i && styles.ledBtnLabelActive]}>
                    LED {i + 1}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
          )}
        </Animated.View>

        {/* ── Renk Seçici ───────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(150)} style={[styles.card, styles.pickerCard]}>
          <Text style={styles.sectionTitle}>Renk Seç</Text>
          <ColorPicker
            hue={hue}
            saturation={saturation}
            value={value}
            onChange={handleColorChange}
            onRelease={handleColorRelease}
          />
        </Animated.View>

        {/* ── Hızlı Renkler ─────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(200)} style={styles.card}>
          <Text style={styles.sectionTitle}>Hızlı Renkler</Text>
          <View style={styles.presetGrid}>
            {PRESETS.map((color, i) => (
              <TouchableOpacity
                key={i}
                style={[
                  styles.presetDot,
                  {
                    backgroundColor: `rgb(${color.r},${color.g},${color.b})`,
                    borderColor: color.r === 0 && color.g === 0 && color.b === 0
                      ? Colors.border : 'transparent',
                    borderWidth: 1,
                  },
                ]}
                onPress={() => applyPreset(color)}
              />
            ))}
          </View>
        </Animated.View>

        {/* ── Parlaklık ─────────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(250)} style={styles.card}>
          <View style={styles.brightnessHeader}>
            <Text style={styles.sectionTitle}>Parlaklık</Text>
            <Text style={styles.brightnessValue}>{brightness}</Text>
          </View>

          {/* Gradient slider track */}
          <View style={styles.sliderContainer}>
            <LinearGradient
              colors={['#000000', '#FFFFFF']}
              start={{ x: 0, y: 0 }}
              end={{ x: 1, y: 0 }}
              style={styles.sliderTrack}
            />
            {/* Native slider overlay — şeffaf arka plan */}
            <View style={styles.sliderOverlay}>
              {/* Basit dokunma alanı */}
              <TouchableOpacity
                style={StyleSheet.absoluteFill}
                onPress={(e) => {
                  const ratio = e.nativeEvent.locationX / 280;
                  setBrightnessLocal(Math.round(Math.max(0, Math.min(1, ratio)) * 255));
                }}
              />
              {/* Thumb */}
              <View
                style={[
                  styles.sliderThumb,
                  { left: `${(brightness / 255) * 100}%` },
                ]}
              />
            </View>
          </View>

          <View style={styles.brightnessButtons}>
            <TouchableOpacity
              style={styles.brightnessBtn}
              onPress={() => { setBrightnessLocal(v => Math.max(0, v - 25)); }}
            >
              <Text style={styles.brightnessBtnText}>−</Text>
            </TouchableOpacity>
            <TouchableOpacity style={styles.applyBtn} onPress={applyBrightness}>
              <LinearGradient colors={Gradients.accent} style={styles.applyBtnGrad}>
                <Text style={styles.applyBtnText}>Uygula</Text>
              </LinearGradient>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.brightnessBtn}
              onPress={() => { setBrightnessLocal(v => Math.min(255, v + 25)); }}
            >
              <Text style={styles.brightnessBtnText}>+</Text>
            </TouchableOpacity>
          </View>
        </Animated.View>

        {/* ── Tümünü Kapat ──────────────────────────────────────────── */}
        <Animated.View entering={FadeInDown.delay(300)}>
          <TouchableOpacity
            style={styles.offBtn}
            onPress={() => esp32.setGlobalColor({ r: 0, g: 0, b: 0 })}
          >
            <Text style={styles.offBtnText}>🌑  Tüm LED'leri Kapat</Text>
          </TouchableOpacity>
        </Animated.View>

      </ScrollView>
    </LinearGradient>
  );
}

// ─── Bağlı Değil Ekranı ───────────────────────────────────────────────────────
function NotConnected() {
  return (
    <LinearGradient colors={['#0D0E1A', '#13141F']} style={styles.notConn}>
      <Text style={styles.notConnIcon}>🔌</Text>
      <Text style={styles.notConnTitle}>Bağlı Değil</Text>
      <Text style={styles.notConnSub}>LED kontrol için önce Bağlantı sekmesinden ESP32'ye bağlanın.</Text>
    </LinearGradient>
  );
}

// ─── Stiller ──────────────────────────────────────────────────────────────────
const styles = StyleSheet.create({
  root:   { flex: 1 },
  scroll: { padding: 20, paddingTop: 60, paddingBottom: 120 },

  header:   { marginBottom: 20 },
  title:    { fontSize: 24, fontWeight: '800', color: Colors.textPrimary },
  subtitle: { color: Colors.textSecondary, fontSize: 13, marginTop: 2 },

  card: {
    backgroundColor: Colors.bgCard,
    borderRadius: 16,
    padding: 18,
    marginBottom: 14,
    borderWidth: 1,
    borderColor: Colors.border,
  },
  pickerCard: { alignItems: 'center' },

  sectionTitle: { fontSize: 14, fontWeight: '700', color: Colors.textPrimary, marginBottom: 14 },

  toggleRow:  { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  toggleLabel: { color: Colors.textPrimary, fontSize: 14, fontWeight: '600' },

  ledSelector: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginTop: 14 },
  ledBtn:      { flex: 1, minWidth: '28%', alignItems: 'center', gap: 6, padding: 10, borderRadius: 10, backgroundColor: Colors.bgDeep, borderWidth: 1, borderColor: Colors.border },
  ledBtnActive: { borderColor: Colors.accent, backgroundColor: Colors.accentSoft },
  ledBtnDot:   { width: 20, height: 20, borderRadius: 10, borderWidth: 1, borderColor: Colors.border },
  ledBtnLabel: { color: Colors.textSecondary, fontSize: 10, fontWeight: '600' },
  ledBtnLabelActive: { color: Colors.accent },

  presetGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  presetDot:  { width: 40, height: 40, borderRadius: 20 },

  brightnessHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 },
  brightnessValue:  { color: Colors.accent, fontWeight: '700', fontSize: 18 },
  sliderContainer:  { height: 24, borderRadius: 12, overflow: 'hidden', marginBottom: 16, position: 'relative' },
  sliderTrack:      { ...StyleSheet.absoluteFillObject, borderRadius: 12 },
  sliderOverlay:    { ...StyleSheet.absoluteFillObject, justifyContent: 'center' },
  sliderThumb:      { position: 'absolute', width: 24, height: 24, borderRadius: 12, backgroundColor: '#FFF', marginLeft: -12, shadowColor: '#000', shadowOffset: { width: 0, height: 2 }, shadowOpacity: 0.4, shadowRadius: 4, elevation: 4, borderWidth: 2, borderColor: Colors.accent },

  brightnessButtons: { flexDirection: 'row', gap: 10, alignItems: 'center' },
  brightnessBtn:     { width: 44, height: 44, borderRadius: 22, backgroundColor: Colors.bgDeep, borderWidth: 1, borderColor: Colors.border, justifyContent: 'center', alignItems: 'center' },
  brightnessBtnText: { color: Colors.textPrimary, fontSize: 22, fontWeight: '300' },
  applyBtn:          { flex: 1, borderRadius: 10, overflow: 'hidden' },
  applyBtnGrad:      { padding: 12, alignItems: 'center' },
  applyBtnText:      { color: '#FFF', fontWeight: '700', fontSize: 14 },

  offBtn:     { backgroundColor: Colors.bgCard, borderRadius: 14, padding: 16, alignItems: 'center', borderWidth: 1, borderColor: Colors.border },
  offBtnText: { color: Colors.textSecondary, fontSize: 14, fontWeight: '600' },

  notConn:      { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 40 },
  notConnIcon:  { fontSize: 64, marginBottom: 16 },
  notConnTitle: { fontSize: 22, fontWeight: '700', color: Colors.textPrimary, marginBottom: 8 },
  notConnSub:   { color: Colors.textSecondary, fontSize: 14, textAlign: 'center', lineHeight: 22 },
});
