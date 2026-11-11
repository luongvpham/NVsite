import { describe, expect, it } from 'vitest';
import { clampCenter, frameSize, presetRatio } from './crop-frame';

describe('presetRatio', () => {
  it('cover → rộng/cao của preset', () => {
    expect(presetRatio('1600x900,cover')).toBeCloseTo(16 / 9);
    expect(presetRatio('800x800,cover')).toBe(1);
    expect(presetRatio('800x600,cover')).toBeCloseTo(4 / 3);
  });
  it('inside / lạ / rỗng → null (không khung)', () => {
    expect(presetRatio('1200x1200,inside')).toBeNull();
    expect(presetRatio('320x96,inside')).toBeNull();
    expect(presetRatio('')).toBeNull();
    expect(presetRatio('abc,cover')).toBeNull();
    expect(presetRatio('0x0,cover')).toBeNull();
  });
});

describe('frameSize', () => {
  it('ảnh vuông + preset 16:9 → kín ngang, thấp hơn', () => {
    const f = frameSize(1, 16 / 9);
    expect(f.w).toBe(1);
    expect(f.h).toBeCloseTo(9 / 16);
  });
  it('ảnh 21:9 + preset vuông → kín dọc, hẹp hơn', () => {
    const f = frameSize(21 / 9, 1);
    expect(f.h).toBe(1);
    expect(f.w).toBeCloseTo(9 / 21);
  });
  it('cùng tỉ lệ → kín cả ảnh; khung luôn đúng tỉ lệ preset', () => {
    expect(frameSize(4 / 3, 4 / 3)).toEqual({ w: 1, h: 1 });
    const f = frameSize(3 / 2, 16 / 9);
    expect((f.w * (3 / 2)) / f.h).toBeCloseTo(16 / 9);
  });
});

describe('clampCenter', () => {
  it('giữ khung trong ảnh', () => {
    const frame = { w: 0.5, h: 1 };
    expect(clampCenter(0, 0.2, frame)).toEqual({ x: 0.25, y: 0.5 });
    expect(clampCenter(1, 0.9, frame)).toEqual({ x: 0.75, y: 0.5 });
    expect(clampCenter(0.4, 0.5, frame)).toEqual({ x: 0.4, y: 0.5 });
  });
});
