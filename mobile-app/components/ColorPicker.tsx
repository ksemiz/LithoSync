// =============================================================================
//  ColorPicker.tsx  —  HSV Renk Seçici (Hue çubuğu + SV kare)
// =============================================================================

import React, { useCallback } from 'react';
import { View, StyleSheet, Text } from 'react-native';
import { Gesture, GestureDetector } from 'react-native-gesture-handler';
import Animated, {
  useSharedValue,
  useAnimatedStyle,
  runOnJS,
} from 'react-native-reanimated';
import { LinearGradient } from 'expo-linear-gradient';
import Svg, { Defs, Rect, LinearGradient as SvgGradient, Stop } from 'react-native-svg';
import { Colors } from '@/constants/Colors';
import { hsvToRgb, RgbColor } from '@/constants/api';

interface ColorPickerProps {
  hue: number;          // 0-360
  saturation: number;   // 0-1
  value: number;        // 0-1
  onChange: (h: number, s: number, v: number) => void;
  onRelease?: (color: RgbColor) => void;
}

const SV_SIZE = 240;
const HUE_HEIGHT = 20;
const HUE_WIDTH = SV_SIZE;

// Hue renk noktaları
const HUE_COLORS = [
  '#FF0000', '#FF8000', '#FFFF00', '#00FF00',
  '#00FFFF', '#0000FF', '#FF00FF', '#FF0000',
] as const;

export function ColorPicker({ hue, saturation, value, onChange, onRelease }: ColorPickerProps) {
  const svThumbX = useSharedValue(saturation * SV_SIZE);
  const svThumbY = useSharedValue((1 - value) * SV_SIZE);
  const hueThumbX = useSharedValue((hue / 360) * HUE_WIDTH);

  // ── SV Alan Gesture ─────────────────────────────────────────────────────────
  const svGesture = Gesture.Pan()
    .onUpdate((e) => {
      const x = Math.max(0, Math.min(SV_SIZE, e.x));
      const y = Math.max(0, Math.min(SV_SIZE, e.y));
      svThumbX.value = x;
      svThumbY.value = y;
      const s = x / SV_SIZE;
      const v = 1 - y / SV_SIZE;
      runOnJS(onChange)(hue, s, v);
    })
    .onEnd((e) => {
      const x = Math.max(0, Math.min(SV_SIZE, e.x));
      const y = Math.max(0, Math.min(SV_SIZE, e.y));
      const s = x / SV_SIZE;
      const v = 1 - y / SV_SIZE;
      if (onRelease) runOnJS(onRelease)(hsvToRgb(hue, s, v));
    });

  // ── Hue Çubuğu Gesture ──────────────────────────────────────────────────────
  const hueGesture = Gesture.Pan()
    .onUpdate((e) => {
      const x = Math.max(0, Math.min(HUE_WIDTH, e.x));
      hueThumbX.value = x;
      const h = (x / HUE_WIDTH) * 360;
      runOnJS(onChange)(h, saturation, value);
    })
    .onEnd((e) => {
      const x = Math.max(0, Math.min(HUE_WIDTH, e.x));
      const h = (x / HUE_WIDTH) * 360;
      if (onRelease) runOnJS(onRelease)(hsvToRgb(h, saturation, value));
    });

  // ── Animasyonlu thumb stilleri ───────────────────────────────────────────────
  const svThumbStyle = useAnimatedStyle(() => ({
    transform: [
      { translateX: svThumbX.value - 12 },
      { translateY: svThumbY.value - 12 },
    ],
  }));

  const hueThumbStyle = useAnimatedStyle(() => ({
    transform: [{ translateX: hueThumbX.value - 10 }],
  }));

  // Seçilen hue'nun hex rengi (SV alanı arka planı için)
  const hueColor = `hsl(${hue}, 100%, 50%)`;
  const currentRgb = hsvToRgb(hue, saturation, value);
  const currentHex = `rgb(${currentRgb.r}, ${currentRgb.g}, ${currentRgb.b})`;

  return (
    <View style={styles.container}>

      {/* ── SV Alan (Saturation × Value 2D pad) ──────────────────────── */}
      <GestureDetector gesture={svGesture}>
        <View style={styles.svContainer}>
          <Svg width={SV_SIZE} height={SV_SIZE} style={StyleSheet.absoluteFill}>
            <Defs>
              {/* Yatay: Beyazdan seçili hue'ya */}
              <SvgGradient id="satGrad" x1="0" y1="0" x2="1" y2="0">
                <Stop offset="0" stopColor="#FFFFFF" stopOpacity="1"/>
                <Stop offset="1" stopColor={hueColor} stopOpacity="1"/>
              </SvgGradient>
              {/* Dikey: Şeffaftan siyaha */}
              <SvgGradient id="valGrad" x1="0" y1="0" x2="0" y2="1">
                <Stop offset="0" stopColor="#000000" stopOpacity="0"/>
                <Stop offset="1" stopColor="#000000" stopOpacity="1"/>
              </SvgGradient>
            </Defs>
            <Rect width={SV_SIZE} height={SV_SIZE} fill="url(#satGrad)"/>
            <Rect width={SV_SIZE} height={SV_SIZE} fill="url(#valGrad)"/>
          </Svg>

          {/* Thumb */}
          <Animated.View style={[styles.svThumb, svThumbStyle, { borderColor: currentHex }]} />
        </View>
      </GestureDetector>

      {/* ── Hue Çubuğu ───────────────────────────────────────────────────── */}
      <GestureDetector gesture={hueGesture}>
        <View style={styles.hueWrapper}>
          <LinearGradient
            colors={HUE_COLORS as unknown as string[]}
            start={{ x: 0, y: 0 }}
            end={{ x: 1, y: 0 }}
            style={styles.hueBar}
          />
          <Animated.View style={[styles.hueThumb, hueThumbStyle]} />
        </View>
      </GestureDetector>

      {/* ── Renk Önizleme ─────────────────────────────────────────────────── */}
      <View style={styles.preview}>
        <View style={[styles.previewSwatch, { backgroundColor: currentHex }]} />
        <Text style={styles.previewText}>
          {`RGB(${currentRgb.r}, ${currentRgb.g}, ${currentRgb.b})`}
        </Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    gap: 16,
  },
  svContainer: {
    width: SV_SIZE,
    height: SV_SIZE,
    borderRadius: 12,
    overflow: 'hidden',
    borderWidth: 1,
    borderColor: Colors.border,
  },
  svThumb: {
    position: 'absolute',
    width: 24,
    height: 24,
    borderRadius: 12,
    borderWidth: 3,
    backgroundColor: 'transparent',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.5,
    shadowRadius: 4,
    elevation: 5,
  },
  hueWrapper: {
    width: HUE_WIDTH,
    height: HUE_HEIGHT + 20,
    justifyContent: 'center',
  },
  hueBar: {
    width: HUE_WIDTH,
    height: HUE_HEIGHT,
    borderRadius: HUE_HEIGHT / 2,
    borderWidth: 1,
    borderColor: Colors.border,
  },
  hueThumb: {
    position: 'absolute',
    width: 20,
    height: 28,
    borderRadius: 4,
    backgroundColor: '#FFF',
    top: -4,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.4,
    shadowRadius: 4,
    elevation: 4,
  },
  preview: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    backgroundColor: Colors.bgCard,
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: Colors.border,
  },
  previewSwatch: {
    width: 32,
    height: 32,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: Colors.border,
  },
  previewText: {
    color: Colors.textSecondary,
    fontSize: 13,
    fontFamily: 'monospace',
  },
});
