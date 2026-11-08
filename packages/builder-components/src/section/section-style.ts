import type { CSSProperties } from 'react';
import { colorVar, sectionGapVar } from '@vsite/theme-engine';

/**
 * Style dùng chung của Section01/02: nền theo KEY màu của theme (`background` là key như `primary`, không phải mã hex —
 * manifest `allowCustom: false`) và khoảng cách trên/dưới theo `--vs-section-gap`. Key lạ/thiếu (kể cả key cũ của theme
 * tĩnh như `muted`, `foreground`) → `background`. `sm` = 40% của `lg` (trước đây py-6 so với py-16).
 */
export function sectionStyle(background: string | undefined, paddingY: string | undefined): CSSProperties {
  const padding = paddingY === 'sm' ? `calc(${sectionGapVar} * 0.4)` : sectionGapVar;
  return { backgroundColor: colorVar(background), paddingTop: padding, paddingBottom: padding };
}
