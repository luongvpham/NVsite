import { describe, expect, it } from 'vitest';
import { ACCEPTED_MIME_TYPES, fitOfPreset, MAX_FILE_SIZE_BYTES, slotUploadClientSchema } from './media-validation';

function makeFile(name: string, sizeBytes: number, type: string): File {
  const file = new File([new Uint8Array(1)], name, { type });
  Object.defineProperty(file, 'size', { value: sizeBytes });
  return file;
}

describe('fitOfPreset', () => {
  it('suy fit từ tên preset theo quy ước {w}x{h},{fit}', () => {
    expect(fitOfPreset('1600x900,cover')).toBe('cover');
    expect(fitOfPreset('1200x1200,inside')).toBe('inside');
    expect(fitOfPreset('320x96,inside')).toBe('inside');
  });

  it('trả null khi preset không theo quy ước', () => {
    expect(fitOfPreset('unknown-preset')).toBeNull();
  });
});

describe('slotUploadClientSchema', () => {
  const baseValues = { preset: '800x800,cover', saveToLibrary: false };

  it('chấp nhận file hợp lệ (≤10MB, mime cho phép)', () => {
    const file = makeFile('a.jpg', 1024, 'image/jpeg');
    const result = slotUploadClientSchema.safeParse({ ...baseValues, file });
    expect(result.success).toBe(true);
  });

  it('từ chối file vượt quá 10MB', () => {
    const file = makeFile('a.jpg', MAX_FILE_SIZE_BYTES + 1, 'image/jpeg');
    const result = slotUploadClientSchema.safeParse({ ...baseValues, file });
    expect(result.success).toBe(false);
  });

  it('từ chối mime type không nằm trong danh sách cho phép', () => {
    const file = makeFile('a.heic', 1024, 'image/heic');
    const result = slotUploadClientSchema.safeParse({ ...baseValues, file });
    expect(result.success).toBe(false);
  });

  it('ACCEPTED_MIME_TYPES đúng đặc tả brief (jpeg, png, webp)', () => {
    expect(ACCEPTED_MIME_TYPES).toEqual(['image/jpeg', 'image/png', 'image/webp']);
  });
});
