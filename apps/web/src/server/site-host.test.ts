import { describe, expect, it } from 'vitest';
import { reservedRoutes } from '../lib/reserved-routes';
import { resolveSiteHost } from './site-host';

const PLATFORM = { kind: 'platform' } as const;

describe.each(['vsite.vn', 'vsite.local'])('resolveSiteHost (root %s)', (root) => {
  const shop = (label: string) => ({ kind: 'shop', host: `${label}.${root}`, slug: label });

  it('accepts a single valid label under root', () => {
    expect(resolveSiteHost(`spa-abc.${root}`, root)).toEqual(shop('spa-abc'));
    expect(resolveSiteHost(`a1.${root}`, root)).toEqual(shop('a1'));
  });

  it('normalizes case, port, trailing dot (A11)', () => {
    expect(resolveSiteHost(`Spa-ABC.${root.toUpperCase()}:3000`, root)).toEqual(shop('spa-abc'));
    expect(resolveSiteHost(`spa-abc.${root}.`, root)).toEqual(shop('spa-abc'));
    expect(resolveSiteHost(`spa-abc.${root}.:8080`, root)).toEqual(shop('spa-abc'));
  });

  it('strips exactly one trailing dot: ".." → platform', () => {
    expect(resolveSiteHost(`spa-abc.${root}..`, root)).toEqual(PLATFORM);
    expect(resolveSiteHost(`spa-abc.${root}..:8080`, root)).toEqual(PLATFORM);
  });

  it('normalizes rootDomain too', () => {
    expect(resolveSiteHost(`spa-abc.${root}`, `.${root.toUpperCase()}.`)).toEqual(shop('spa-abc'));
  });

  it.each([
    '',
    root,
    `${root}:3000`,
    'evil.com',
    `spa-abc.${root}.evil.com`,
    `x${root}`,
    `spa-abc.evil${root}`,
    `a.b.${root}`,
    `.${root}`,
    `-a.${root}`,
    `a-.${root}`,
    `a_b.${root}`,
    `a b.${root}`,
    `a@b.${root}`,
    `a..${root}`,
    `${'a'.repeat(64)}.${root}`,
    '127.0.0.1',
    '127.0.0.1:3000',
    '[::1]:3000',
    '[::1]',
    'localhost',
    `spa-abc.${root}:abc`,
  ])('platform for %j', (h) => {
    expect(resolveSiteHost(h, root)).toEqual(PLATFORM);
  });

  it('accepts a 63-char label', () => {
    const l = 'a'.repeat(63);
    expect(resolveSiteHost(`${l}.${root}`, root)).toEqual(shop(l));
  });

  it('rejects every reserved subdomain from config', () => {
    expect(reservedRoutes.reservedSubdomains.length).toBeGreaterThan(0);
    for (const r of reservedRoutes.reservedSubdomains) {
      expect(resolveSiteHost(`${r}.${root}`, root)).toEqual(PLATFORM);
      expect(resolveSiteHost(`${r.toUpperCase()}.${root}`, root)).toEqual(PLATFORM);
    }
  });

  it('platform when rootDomain is empty', () => {
    expect(resolveSiteHost('a.b', '')).toEqual(PLATFORM);
  });
});
