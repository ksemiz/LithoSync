// =============================================================================
//  LedPreview.tsx  —  6 LED'in canlı renk önizlemesi (glow efektli)
// =============================================================================

import React from 'react';
import { View, StyleSheet, Text } from 'react-native';
import { Colors } from '@/constants/Colors';
import { rgbToHex } from '@/constants/api';

interface LedPreviewProps {
  leds: Array<{ r: number; g: number; b: number }>;
  selectedIndex?: number;
  onPress?: (index: number) => void;
  showLabels?: boolean;
  size?: 'sm' | 'md' | 'lg';
}

const SIZES = { sm: 28, md: 40, lg: 56 };

export function LedPreview({
  leds,
  selectedIndex,
  onPress,
  showLabels = false,
  size = 'md',
}: LedPreviewProps) {
  const dotSize = SIZES[size];

  return (
    <View style={styles.container}>
      {leds.map((led, i) => {
        const hex = rgbToHex(led);
        const isSelected = selectedIndex === i;
        const brightness = (led.r + led.g + led.b) / 3;

        return (
          <View key={i} style={styles.ledWrapper}>
            {/* Glow efekti */}
            <View
              style={[
                styles.glow,
                {
                  width: dotSize + 20,
                  height: dotSize + 20,
                  borderRadius: (dotSize + 20) / 2,
                  backgroundColor: hex,
                  opacity: brightness > 10 ? 0.35 : 0,
                },
              ]}
            />
            {/* Ana LED */}
            <View
              style={[
                styles.led,
                {
                  width: dotSize,
                  height: dotSize,
                  borderRadius: dotSize / 2,
                  backgroundColor: hex,
                  borderWidth: isSelected ? 2 : 0,
                  borderColor: Colors.accentGlow,
                },
                brightness < 10 && styles.ledOff,
              ]}
            />
            {showLabels && (
              <Text style={styles.label}>{i + 1}</Text>
            )}
          </View>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    justifyContent: 'space-evenly',
    alignItems: 'center',
    paddingVertical: 8,
  },
  ledWrapper: {
    alignItems: 'center',
    justifyContent: 'center',
    position: 'relative',
  },
  glow: {
    position: 'absolute',
    // React Native shadow for glow effect
    shadowColor: '#FFFFFF',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 1,
    shadowRadius: 12,
    elevation: 8,
  },
  led: {
    shadowColor: '#FFFFFF',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.8,
    shadowRadius: 8,
    elevation: 5,
  },
  ledOff: {
    backgroundColor: '#1A1B2E',
    borderWidth: 1,
    borderColor: '#252640',
  },
  label: {
    color: Colors.textMuted,
    fontSize: 9,
    marginTop: 4,
    fontWeight: '600',
  },
});
