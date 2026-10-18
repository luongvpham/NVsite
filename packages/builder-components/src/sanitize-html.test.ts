import { readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import { classifyDangerous } from '../tests/helpers/richtext-danger';
import { sanitizeRichText } from './sanitize-html';

describe('sanitizeRichText (§7.2, §9 test #4)', () => {
  it('strips <script>, <img onerror>, and javascript: URLs', () => {
    const dirty =
      '<p>Hello</p><script>alert(1)</script><img src=x onerror="alert(2)"><a href="javascript:alert(3)">click</a>';
    const clean = sanitizeRichText(dirty, 'basic');

    expect(clean).not.toContain('<script');
    expect(clean).not.toContain('onerror');
    expect(clean).not.toContain('<img');
    expect(clean).not.toContain('javascript:');
  });

  it('keeps whitelisted tags (basic profile)', () => {
    const clean = sanitizeRichText('<p>Text <strong>bold</strong> and <ul><li>item</li></ul></p>', 'basic');
    expect(clean).toContain('<strong>bold</strong>');
    expect(clean).toContain('<li>item</li>');
  });

  it('strips tags not in the "inline" profile even if valid HTML (ul/li only in "basic")', () => {
    const clean = sanitizeRichText('<ul><li>item</li></ul><strong>bold</strong>', 'inline');
    expect(clean).not.toContain('<ul>');
    expect(clean).not.toContain('<li>');
    expect(clean).toContain('<strong>bold</strong>');
  });

  it('forces rel="noopener noreferrer" on target="_blank" links', () => {
    const clean = sanitizeRichText('<a href="https://x.com" target="_blank">link</a>', 'inline');
    expect(clean).toContain('rel="noopener noreferrer"');
  });

  it('strips style/class/id/data-* attributes', () => {
    const clean = sanitizeRichText('<p style="color:red" class="x" id="y" data-foo="bar">text</p>', 'basic');
    expect(clean).not.toContain('style=');
    expect(clean).not.toContain('class=');
    expect(clean).not.toContain('id=');
    expect(clean).not.toContain('data-foo');
  });

  it('"dangerous" là khoá danh sách nguy hiểm, KHÔNG phải profile — không dùng để sanitize (WEBSITE-001)', () => {
    expect(() => sanitizeRichText('<p>x</p>', 'dangerous')).toThrow();
  });

  it('throws on an unknown profile name', () => {
    expect(() => sanitizeRichText('<p>x</p>', 'unknown-profile')).toThrow();
  });
});

/**
 * Fixture dùng chung FE + BE (WEBSITE-001 S0-T4, R4/R5): tests/fixtures/tree/xss-richtext.json.
 * Đóng nợ BOOTSTRAP-002 "thiếu test data: URL". BE chạy CÙNG file này (RichTextSanitizerTests).
 */
interface XssVector {
  id: string;
  input: string;
  expect: 'reject' | 'clean' | 'pass';
  clean?: string;
  mustNotContain?: string[];
  parserDependent?: boolean;
}
const xssFixture = JSON.parse(
  readFileSync(path.resolve(import.meta.dirname, '..', 'tests', 'fixtures', 'tree', 'xss-richtext.json'), 'utf8'),
) as { profile: string; vectors: XssVector[] };

describe('xss-richtext.json — fixture dùng chung FE+BE (R4, R5)', () => {
  it('có đủ ba loại expect và vector data:/javascript:/vbscript:/on*/entity-encoded', () => {
    const expects = new Set(xssFixture.vectors.map((v) => v.expect));
    expect([...expects].sort()).toEqual(['clean', 'pass', 'reject']);
    const ids = xssFixture.vectors.map((v) => v.id);
    expect(new Set(ids).size).toBe(ids.length);
    const all = xssFixture.vectors.map((v) => v.input).join(' ');
    for (const needle of ['data:', 'javascript:', 'vbscript:', 'onerror', '&lt;script&gt;']) expect(all).toContain(needle);
  });

  it.each(xssFixture.vectors.map((v) => [v.id, v] as const))('%s', (_id, vector) => {
    const out = sanitizeRichText(vector.input, xssFixture.profile);

    // Idempotent (R5): sanitize(sanitize(x)) == sanitize(x)
    expect(sanitizeRichText(out, xssFixture.profile)).toBe(out);
    // Bản sạch không bao giờ còn thứ nguy hiểm — kể cả vector BE phải từ chối.
    expect(classifyDangerous(out)).toBe('safe');
    for (const bad of vector.mustNotContain ?? []) expect(out.toLowerCase()).not.toContain(bad.toLowerCase());

    if (vector.expect === 'pass') expect(out).toBe(vector.input);
    if (vector.expect === 'clean') {
      if (vector.clean !== undefined) expect(out).toBe(vector.clean);
      expect(out).not.toBe(vector.input);
    }
    if (!vector.parserDependent) {
      // Nhãn expect phải khớp danh sách "dangerous" của config (nhãn sai = fixture sai, BE sẽ test theo nhãn).
      expect(classifyDangerous(vector.input) === 'unsafe', 'phân loại theo config/sanitize-profiles.json#dangerous').toBe(vector.expect === 'reject');
    }
  });
});
