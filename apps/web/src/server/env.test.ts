// @vitest-environment node
import { afterEach, describe, expect, it } from 'vitest';
import { readApiOrigin, readRootDomain, readServerEnv } from './env';

const saved = { a: process.env.API_ORIGIN, r: process.env.ROOT_DOMAIN };
afterEach(() => {
  if (saved.a === undefined) delete process.env.API_ORIGIN; else process.env.API_ORIGIN = saved.a;
  if (saved.r === undefined) delete process.env.ROOT_DOMAIN; else process.env.ROOT_DOMAIN = saved.r;
});

describe('readServerEnv', () => {
  it('reads both variables lazily', () => {
    process.env.API_ORIGIN = 'http://localhost:5270';
    process.env.ROOT_DOMAIN = 'vsite.local';
    expect(readServerEnv()).toEqual({ apiOrigin: 'http://localhost:5270', rootDomain: 'vsite.local' });
  });
  it('throws a clear error naming the missing variable', () => {
    process.env.API_ORIGIN = 'http://x';
    delete process.env.ROOT_DOMAIN;
    expect(() => readServerEnv()).toThrow(/ROOT_DOMAIN/);
    delete process.env.API_ORIGIN;
    process.env.ROOT_DOMAIN = 'vsite.local';
    expect(() => readServerEnv()).toThrow(/API_ORIGIN/);
  });
  it('treats blank as missing', () => {
    process.env.API_ORIGIN = '  ';
    process.env.ROOT_DOMAIN = 'vsite.local';
    expect(() => readServerEnv()).toThrow(/API_ORIGIN/);
  });
});

describe('readRootDomain / readApiOrigin (độc lập nhau)', () => {
  it('readRootDomain không cần API_ORIGIN', () => {
    delete process.env.API_ORIGIN;
    process.env.ROOT_DOMAIN = 'vsite.local';
    expect(readRootDomain()).toBe('vsite.local');
  });
  it('readApiOrigin không cần ROOT_DOMAIN; thiếu thì báo rõ', () => {
    delete process.env.ROOT_DOMAIN;
    process.env.API_ORIGIN = 'http://x';
    expect(readApiOrigin()).toBe('http://x');
    delete process.env.API_ORIGIN;
    expect(() => readApiOrigin()).toThrow(/API_ORIGIN/);
    expect(() => readRootDomain()).toThrow(/ROOT_DOMAIN/);
  });
});
