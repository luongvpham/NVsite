/**
 * F11 — khung crop theo preset. Contract upload/clone chỉ nhận focal point (không có tham số crop), nên "khung" chỉ là
 * cách chọn focal point trực quan: tâm khung = focal point. Preset đặt tên `{w}x{h},{fit}` (config/image-presets.json).
 */
export interface Frame {
  /** Kích thước khung theo tỉ lệ ảnh hiển thị (0..1). */
  w: number;
  h: number;
}

/** Tỉ lệ rộng/cao của preset `cover`; `inside`/lạ/không hợp lệ → null (không khung). */
export function presetRatio(preset: string): number | null {
  const match = /^(\d+)x(\d+),cover$/.exec(preset);
  if (!match) return null;
  const width = Number(match[1]);
  const height = Number(match[2]);
  return width > 0 && height > 0 ? width / height : null;
}

/** Khung lớn nhất đúng tỉ lệ preset nằm gọn trong ảnh (ảnh càng "hẹp" hơn preset thì khung càng kín chiều ngang). */
export function frameSize(imageAspect: number, ratio: number): Frame {
  if (!(imageAspect > 0) || !(ratio > 0)) return { w: 1, h: 1 };
  return ratio >= imageAspect ? { w: 1, h: imageAspect / ratio } : { w: ratio / imageAspect, h: 1 };
}

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/** Tâm khung (= focal point) sao cho khung không tràn ra ngoài ảnh. */
export function clampCenter(x: number, y: number, frame: Frame): { x: number; y: number } {
  return {
    x: clamp(x, frame.w / 2, 1 - frame.w / 2),
    y: clamp(y, frame.h / 2, 1 - frame.h / 2),
  };
}
