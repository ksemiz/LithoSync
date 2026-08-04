// =============================================================================
//  Colors.ts  —  LithoSync Dark Tema Renk Paleti
// =============================================================================

export const Colors = {
  // Arka planlar
  bgDeep:    '#0D0E1A',
  bgPanel:   '#13141F',
  bgCard:    '#1A1B2E',
  bgCardHov: '#1F2035',

  // Vurgu
  accent:     '#6C63FF',
  accentDim:  '#4A44B0',
  accentGlow: '#9D98FF',
  accentSoft: 'rgba(108, 99, 255, 0.15)',

  // Metin
  textPrimary:   '#E8E9F3',
  textSecondary: '#7B7D99',
  textMuted:     '#4A4B66',

  // Durum
  success: '#2ECC71',
  warning: '#F39C12',
  danger:  '#E74C3C',
  info:    '#3498DB',

  // Kenarlık
  border:      '#252640',
  borderLight: '#2E3055',

  // Tab bar
  tabActive:   '#6C63FF',
  tabInactive: '#4A4B66',
};

export const Gradients = {
  accent:  ['#6C63FF', '#4A44B0'] as const,
  success: ['#2ECC71', '#27AE60'] as const,
  danger:  ['#E74C3C', '#C0392B'] as const,
  card:    ['#1A1B2E', '#13141F'] as const,
  bg:      ['#0D0E1A', '#13141F'] as const,
};
