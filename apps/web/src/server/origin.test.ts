// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { buildOrigin } from './origin';

describe('buildOrigin (A3)', () => {
  it.each([
    ['https:', 'Spa-ABC.vsite.vn', 'https://spa-abc.vsite.vn'],
    ['http:', 'spa.vsite.local:3000', 'http://spa.vsite.local:3000'],
    ['https:', 'spa-abc.vsite.vn.', 'https://spa-abc.vsite.vn'],
    ['https:', 'spa-abc.vsite.vn.:8443', 'https://spa-abc.vsite.vn:8443'],
    ['ftp:', 'x.vsite.vn', 'http://x.vsite.vn'],
  ])('%s %s → %s', (proto, host, out) => {
    expect(buildOrigin(proto, host)).toBe(out);
  });
});
