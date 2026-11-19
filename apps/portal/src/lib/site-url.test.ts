import { afterEach, describe, expect, it, vi } from 'vitest';
import { shopSiteUrl } from './site-url';

afterEach(() => {
  vi.unstubAllEnvs();
});

describe('shopSiteUrl', () => {
  it('thay {slug} trong template', () => {
    vi.stubEnv('VITE_SHOP_SITE_ORIGIN_TEMPLATE', 'http://{slug}.vsite.local:3000');
    expect(shopSiteUrl('spa-abc')).toBe('http://spa-abc.vsite.local:3000');
  });

  it('thay đúng mọi chỗ có {slug}', () => {
    vi.stubEnv('VITE_SHOP_SITE_ORIGIN_TEMPLATE', 'https://x.vn/{slug}/s/{slug}');
    expect(shopSiteUrl('a')).toBe('https://x.vn/a/s/a');
  });

  it('thiếu env ⇒ null', () => {
    vi.stubEnv('VITE_SHOP_SITE_ORIGIN_TEMPLATE', undefined);
    expect(shopSiteUrl('spa-abc')).toBeNull();
  });

  it('env rỗng/khoảng trắng ⇒ null', () => {
    vi.stubEnv('VITE_SHOP_SITE_ORIGIN_TEMPLATE', '  ');
    expect(shopSiteUrl('spa-abc')).toBeNull();
  });

  it('slug rỗng ⇒ null', () => {
    vi.stubEnv('VITE_SHOP_SITE_ORIGIN_TEMPLATE', 'http://{slug}.vsite.local:3000');
    expect(shopSiteUrl('')).toBeNull();
  });
});
