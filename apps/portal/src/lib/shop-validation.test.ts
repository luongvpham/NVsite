import { describe, expect, it } from 'vitest';
import { createShopFormSchema, slugSchema } from './shop-validation';

describe('slugSchema', () => {
  it('chấp nhận slug hợp lệ', () => {
    expect(slugSchema.safeParse('demo-spa').success).toBe(true);
  });

  it('từ chối slug quá ngắn', () => {
    expect(slugSchema.safeParse('ab').success).toBe(false);
  });

  it('từ chối slug bắt đầu hoặc kết thúc bằng "-"', () => {
    expect(slugSchema.safeParse('-demo-spa').success).toBe(false);
    expect(slugSchema.safeParse('demo-spa-').success).toBe(false);
  });

  it('từ chối slug có ký tự hoa hoặc ký tự đặc biệt', () => {
    expect(slugSchema.safeParse('Demo-Spa').success).toBe(false);
    expect(slugSchema.safeParse('demo_spa').success).toBe(false);
  });

  it('từ chối slug nằm trong config/reserved-routes.json (vd. "admin", "shop")', () => {
    expect(slugSchema.safeParse('admin').success).toBe(false);
    expect(slugSchema.safeParse('shop').success).toBe(false);
  });
});

describe('createShopFormSchema — ràng buộc chéo ExternalOnly ⇒ externalUrl', () => {
  it('kind Hosted không cần externalUrl', () => {
    const result = createShopFormSchema.safeParse({
      name: 'Spa Demo',
      slug: 'demo-spa',
      kind: 'Hosted',
      externalUrl: null,
    });
    expect(result.success).toBe(true);
  });

  it('kind ExternalOnly thiếu externalUrl bị từ chối', () => {
    const result = createShopFormSchema.safeParse({
      name: 'Spa Demo',
      slug: 'demo-spa',
      kind: 'ExternalOnly',
      externalUrl: '',
    });
    expect(result.success).toBe(false);
  });

  it('kind ExternalOnly có externalUrl hợp lệ', () => {
    const result = createShopFormSchema.safeParse({
      name: 'Spa Demo',
      slug: 'demo-spa',
      kind: 'ExternalOnly',
      externalUrl: 'https://demo-spa.example.com',
    });
    expect(result.success).toBe(true);
  });
});
