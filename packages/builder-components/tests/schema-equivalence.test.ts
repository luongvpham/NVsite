import Ajv2020 from 'ajv/dist/2020';
import { describe, expect, it } from 'vitest';
import propsSchemasJson from '../generated/props-schemas.json';
import {
  galleryPropsSchema,
  heroPropsSchema,
  richTextPropsSchema,
  sectionPropsSchema,
  serviceGridPropsSchema,
} from '../generated/props-schemas';

/**
 * §9 test #2 — bài test QUAN TRỌNG NHẤT của Bước 2 (#60): Zod (FE) và JSON Schema (BE) phải
 * cho CÙNG kết quả trên toàn bộ fixture. Nếu hai runtime lệch, BE và FE sẽ chấp nhận hai tập
 * dữ liệu khác nhau — đúng rủi ro mà #60 tồn tại để chặn.
 */
const ajv = new Ajv2020({ strict: false });

function assertEquivalent(zodSchema: { safeParse: (v: unknown) => { success: boolean } }, jsonSchema: object, cases: Array<{ name: string; value: unknown }>) {
  const validateJson = ajv.compile(jsonSchema);

  for (const { name, value } of cases) {
    const zodResult = zodSchema.safeParse(value).success;
    const jsonResult = validateJson(value);
    expect(jsonResult, `case '${name}': JSON Schema verdict phải khớp Zod (Zod=${zodResult})`).toBe(zodResult);
  }
}

describe('Zod ↔ JSON Schema equivalence (#60)', () => {
  it('Hero — valid, wrong type, over maxLength, bad enum, nested group/link', () => {
    assertEquivalent(heroPropsSchema, propsSchemasJson.Hero, [
      { name: 'empty (mọi prop optional)', value: {} },
      { name: 'valid full', value: { title: 'x', image: { imageId: 'a' }, align: 'center', overlayOpacity: 40 } },
      { name: 'overlayOpacity string thay vì number', value: { overlayOpacity: 'nhiều' } },
      { name: 'title vượt maxLength 120', value: { title: 'x'.repeat(121) } },
      { name: 'align không trong enum', value: { align: 'diagonal' } },
      { name: 'image thiếu imageId (required)', value: { image: { alt: 'x' } } },
      { name: 'cta.target hợp lệ (external)', value: { cta: { target: { kind: 'external', url: 'https://x.com' } } } },
      { name: 'cta.target sai kind', value: { cta: { target: { kind: 'unknown', url: 'x' } } } },
    ]);
  });

  it('Section — enum paddingY/maxWidth', () => {
    assertEquivalent(sectionPropsSchema, propsSchemasJson.Section, [
      { name: 'valid', value: { paddingY: 'sm', maxWidth: 'lg' } },
      { name: 'paddingY sai enum', value: { paddingY: 'huge' } },
    ]);
  });

  it('RichText — maxLength và enum align', () => {
    assertEquivalent(richTextPropsSchema, propsSchemasJson.RichText, [
      { name: 'valid', value: { content: '<p>ok</p>', align: 'left' } },
      { name: 'content vượt maxLength 5000', value: { content: 'x'.repeat(5001) } },
      { name: 'align sai enum', value: { align: 'center' } },
    ]);
  });

  it('Gallery — list minItems/maxItems, item required imageId', () => {
    assertEquivalent(galleryPropsSchema, propsSchemasJson.Gallery, [
      { name: 'valid 1 item', value: { items: [{ image: { imageId: 'a' } }] } },
      { name: 'items rỗng vi phạm minItems:1', value: { items: [] } },
      { name: 'item thiếu imageId', value: { items: [{ image: {} }] } },
      { name: 'columns ngoài min/max', value: { columns: 10 } },
    ]);
  });

  it('ServiceGrid — boolean showPrice, number columns', () => {
    assertEquivalent(serviceGridPropsSchema, propsSchemasJson.ServiceGrid, [
      { name: 'valid', value: { showPrice: true, columns: 3 } },
      { name: 'showPrice sai type', value: { showPrice: 'yes' } },
    ]);
  });
});
