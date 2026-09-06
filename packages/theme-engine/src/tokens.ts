/**
 * Token nền tối thiểu cho Bước 1 — chỉ đủ để apps/web và apps/portal dùng chung
 * một bảng màu/spacing cơ bản. Theme per-shop (Quyết định #12/#13) là Phase 2.
 */
export const colorTokens = {
  background: '#ffffff',
  foreground: '#0a0a0a',
  primary: '#2563eb',
  primaryForeground: '#ffffff',
  muted: '#f4f4f5',
  mutedForeground: '#71717a',
  border: '#e4e4e7',
  destructive: '#dc2626',
} as const;

export const spacingTokens = {
  xs: '0.25rem',
  sm: '0.5rem',
  md: '1rem',
  lg: '1.5rem',
  xl: '2rem',
} as const;
