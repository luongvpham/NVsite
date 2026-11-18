// @vitest-environment node
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { parseLoadSiteInput } from './load-site-input';

/** R13: server fn `loadSite` là endpoint RPC gọi được từ ngoài — input CHỈ có `path`. */
describe('parseLoadSiteInput (R13)', () => {
  it('bỏ mọi khoá ngoài `path` (host, shopId, …)', () => {
    const parsed = parseLoadSiteInput({ path: '/lien-he', host: 'evil.vsite.vn', shopId: 'x', slug: 'spa-xyz' });
    expect(parsed).toEqual({ path: '/lien-he' });
    expect(Object.keys(parsed)).toEqual(['path']);
  });

  it.each([undefined, null, 'x', 42, {}, { path: 1 }, { path: 'lien-he' }, { path: '' }])(
    'từ chối input sai %j',
    (input) => {
      expect(() => parseLoadSiteInput(input)).toThrow(TypeError);
    },
  );
});

describe('vỏ load-site.ts (R1, R13)', () => {
  const src = readFileSync(new URL('./load-site.ts', import.meta.url), 'utf8');

  it('host chỉ đọc bằng getRequestHost({ xForwardedHost: false })', () => {
    expect(src).toMatch(/getRequestHost\(\{\s*xForwardedHost:\s*false\s*\}\)/);
    expect(src).not.toMatch(/xForwardedHost:\s*true/);
    expect(src.toLowerCase()).not.toContain('x-forwarded');
    expect(src).not.toMatch(/\bgetRequestHeaders?\b|\bgetHeader\b/);
  });

  it('validator là parseLoadSiteInput và lõi chỉ nhận data.path', () => {
    expect(src).toMatch(/\.validator\(parseLoadSiteInput\)/);
    expect(src).toMatch(/loadSiteForHost\(\s*host,\s*data\.path,/);
    expect(src).not.toMatch(/data\.(host|shopId|slug)/);
  });
});
