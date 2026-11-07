import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import themeTokensJson from '../../../config/theme-tokens.json';
import {
  NODE_ID_PATTERN,
  isSafeLinkUrl,
  opRules,
  sanitizeRichText,
  themeTokensSchema,
  treeEnvelopeSchema,
} from '../src/index';

/**
 * Bề mặt công khai mà Operations Engine + editor (WEBSITE-002 F3→F4) dựa vào. Các export này trước đây chỉ
 * nằm ở `meta/`, `generated/`, `src/sanitize-html.ts` hoặc — với `isSafeLinkUrl` — trong test helper.
 */
const FIXTURES = path.join(import.meta.dirname, 'fixtures', 'tree');

function fixture(name: string): { tree: unknown; expect: string; errorCodes?: string[] } {
  return JSON.parse(readFileSync(path.join(FIXTURES, name), 'utf8')) as { tree: unknown; expect: string };
}

/** Mọi `url` nằm trong các object `target` của một cây fixture. */
function collectUrls(value: unknown, acc: unknown[] = []): unknown[] {
  if (Array.isArray(value)) {
    value.forEach((item) => collectUrls(item, acc));
  } else if (typeof value === 'object' && value !== null) {
    const record = value as Record<string, unknown>;
    if ('url' in record) acc.push(record.url);
    Object.values(record).forEach((item) => collectUrls(item, acc));
  }
  return acc;
}

describe('treeEnvelopeSchema', () => {
  it('nhận cây hợp lệ của fixture dùng chung', () => {
    const result = treeEnvelopeSchema.safeParse(fixture('valid-link-urls.json').tree);
    expect(result.success).toBe(true);
  });

  it('từ chối vỏ sai: thiếu schemaVersion, gốc không phải Page, khoá lạ ở gốc', () => {
    const base = { schemaVersion: 1, id: 'root', type: 'Page', children: [] };
    expect(treeEnvelopeSchema.safeParse(base).success).toBe(true);
    expect(treeEnvelopeSchema.safeParse({ ...base, schemaVersion: undefined }).success).toBe(false);
    expect(treeEnvelopeSchema.safeParse({ ...base, type: 'Section' }).success).toBe(false);
    expect(treeEnvelopeSchema.safeParse({ ...base, extra: 1 }).success).toBe(false);
  });
});

describe('themeTokensSchema', () => {
  it('nhận `defaults` của config/theme-tokens.json', () => {
    expect(themeTokensSchema.safeParse(themeTokensJson.defaults).success).toBe(true);
  });

  it('từ chối màu sai pattern và giá trị ngoài tập cho phép', () => {
    const defaults = themeTokensJson.defaults;
    expect(
      themeTokensSchema.safeParse({ ...defaults, colors: { ...defaults.colors, primary: 'tím' } }).success,
    ).toBe(false);
    expect(
      themeTokensSchema.safeParse({ ...defaults, shape: { ...defaults.shape, radius: 'huge' } }).success,
    ).toBe(false);
  });
});

describe('NODE_ID_PATTERN', () => {
  it('khớp id node hợp lệ, không khớp id ngắn hoặc sai tiền tố', () => {
    const pattern = new RegExp(NODE_ID_PATTERN);
    expect(pattern.test('c_hero1')).toBe(true);
    expect(pattern.test('c_ab-_9Z')).toBe(true);
    expect(pattern.test('c_ab')).toBe(false);
    expect(pattern.test('hero1')).toBe(false);
    expect(pattern.test('c_hero 1')).toBe(false);
  });
});

describe('opRules', () => {
  it('có luật cho 5 type của registry (acceptsChildren, maxPerPage, kind cho phép)', () => {
    expect(Object.keys(opRules).sort()).toEqual(['Gallery', 'Hero', 'RichText', 'Section', 'ServiceGrid']);
    expect(opRules.Hero.maxPerPage).toBe(1);
    expect(opRules.Section.acceptsChildren).toBe(true);
    expect(opRules.RichText.allowedInPageKinds).toContain('System');
    expect(opRules.Hero.allowedInPageKinds).not.toContain('System');
    expect(opRules.RichText.editableInSystemPageProps).toEqual(['content']);
  });
});

describe('sanitizeRichText (export công khai)', () => {
  it('làm sạch script/onerror theo profile basic', () => {
    const clean = sanitizeRichText('<p>a</p><script>alert(1)</script><img src=x onerror=alert(2)>', 'basic');
    expect(clean).toContain('<p>a</p>');
    expect(clean).not.toMatch(/script|onerror|<img/);
  });

  it('`dangerous` không phải profile → ném lỗi', () => {
    expect(() => sanitizeRichText('x', 'dangerous')).toThrow(/profile 'dangerous'/);
  });
});

describe('isSafeLinkUrl', () => {
  it.each([
    'https://vsite.vn/x?a=1&b=2',
    'http://vsite.vn',
    'mailto:a@b.vn',
    'tel:+84901234567',
    '/dich-vu',
    '/dich-vu?x=1#top',
    '#lien-he',
    'HTTPS://VSITE.VN',
  ])('an toàn: %s', (url) => {
    expect(isSafeLinkUrl(url)).toBe(true);
  });

  it.each([
    'javascript:alert(1)',
    'JaVa\tScript:alert(1)',
    'java&#115;cript&colon;alert(1)',
    'data:text/html;base64,PHNjcmlwdD4=',
    'vbscript:msgbox(1)',
    '//evil.example/x',
    '/\\evil.example',
    'evil.example/x',
    '',
    '   ',
  ])('không an toàn: %j', (url) => {
    expect(isSafeLinkUrl(url)).toBe(false);
  });

  it('không phải chuỗi → không an toàn', () => {
    for (const value of [undefined, null, 5, {}, []]) expect(isSafeLinkUrl(value)).toBe(false);
  });

  it('khớp bộ fixture dùng chung: mọi url ở valid-link-urls an toàn, mọi url ở invalid-link-url-* không', () => {
    const validUrls = collectUrls(fixture('valid-link-urls.json').tree);
    expect(validUrls.length).toBeGreaterThan(0);
    for (const url of validUrls) expect(isSafeLinkUrl(url), String(url)).toBe(true);

    const invalidFiles = readdirSync(FIXTURES).filter((name) => name.startsWith('invalid-link-url-'));
    expect(invalidFiles.length).toBeGreaterThanOrEqual(7);
    for (const name of invalidFiles) {
      const urls = collectUrls(fixture(name).tree);
      expect(urls.length, name).toBeGreaterThan(0);
      for (const url of urls) expect(isSafeLinkUrl(url), `${name}: ${String(url)}`).toBe(false);
    }
  });
});
