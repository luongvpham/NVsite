import { colorTokens } from '@vsite/theme-engine';

/** @type {import('tailwindcss').Config} */
export default {
  theme: {
    extend: {
      colors: {
        background: colorTokens.background,
        foreground: colorTokens.foreground,
        primary: {
          DEFAULT: colorTokens.primary,
          foreground: colorTokens.primaryForeground,
        },
        muted: {
          DEFAULT: colorTokens.muted,
          foreground: colorTokens.mutedForeground,
        },
        border: colorTokens.border,
        destructive: colorTokens.destructive,
      },
    },
  },
};
