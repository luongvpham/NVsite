import { describe, expect, it } from 'vitest';
import {
  getThemeValue,
  isValidThemeColor,
  mapThemeErrors,
  pointerToFieldKey,
  setThemeValue,
  themeEquals,
} from './theme-edit';

const tokens = {
  schemaVersion: 1,
  colors: { primary: '#7c3aed', text: '#111827' },
  shape: { radius: 'md', shadow: 'soft' },
};

describe('isValidThemeColor', () => {
  it.each(['#7c3aed', '#FFFFFF', '#000000'])('%s hợp lệ', (c) => {
    expect(isValidThemeColor(c)).toBe(true);
  });
  it.each(['7c3aed', '#fff', '#7c3aedff', 'red', '#ggg000', '', 'url(x)'])('%j bị chặn', (c) => {
    expect(isValidThemeColor(c)).toBe(false);
  });
});

describe('get/setThemeValue', () => {
  it('đọc theo khoá a.b; thiếu → undefined', () => {
    expect(getThemeValue(tokens, 'colors.primary')).toBe('#7c3aed');
    expect(getThemeValue(tokens, 'colors.nope')).toBeUndefined();
    expect(getThemeValue(tokens, 'x.y')).toBeUndefined();
    expect(getThemeValue(null, 'colors.primary')).toBeUndefined();
  });

  it('set trả bản sao, không đụng bản gốc, giữ phần còn lại', () => {
    const next = setThemeValue(tokens, 'colors.primary', '#000000');
    expect(next).toEqual({ ...tokens, colors: { primary: '#000000', text: '#111827' } });
    expect(tokens.colors.primary).toBe('#7c3aed');
    expect(next.shape).toBe(tokens.shape);
  });
});

describe('themeEquals', () => {
  it('bằng nhau không phụ thuộc thứ tự khoá', () => {
    expect(themeEquals({ a: 1, b: { c: 2, d: 3 } }, { b: { d: 3, c: 2 }, a: 1 })).toBe(true);
  });
  it('khác giá trị / thiếu khoá → false', () => {
    expect(themeEquals({ a: 1 }, { a: 2 })).toBe(false);
    expect(themeEquals({ a: 1 }, { a: 1, b: 1 })).toBe(false);
  });
});

describe('pointerToFieldKey', () => {
  it('đổi JSON Pointer sang khoá ô; gốc → undefined', () => {
    expect(pointerToFieldKey('/colors/primary')).toBe('colors.primary');
    expect(pointerToFieldKey('')).toBeUndefined();
    expect(pointerToFieldKey('/')).toBeUndefined();
  });
});

describe('mapThemeErrors', () => {
  const known = new Set(['colors.primary', 'shape.radius']);
  it('gắn lỗi vào ô; path lạ/rỗng → banner; ô trùng lấy lỗi đầu', () => {
    const mapped = mapThemeErrors(
      [
        { path: '/colors/primary', code: 'THEME_TOKEN_INVALID', message: 'màu sai' },
        { path: '/colors/primary', code: 'THEME_TOKEN_INVALID', message: 'lỗi thứ hai' },
        { path: '/colors/ghost', code: 'THEME_TOKEN_INVALID', message: 'key lạ' },
        { path: '', code: 'THEME_INVALID', message: 'thiếu tokens' },
      ],
      known,
    );
    expect(mapped.byField.get('colors.primary')).toBe('màu sai');
    expect(mapped.byField.size).toBe(1);
    expect(mapped.general).toEqual(['key lạ', 'thiếu tokens']);
  });
});
