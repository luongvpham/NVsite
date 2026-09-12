import { describe, expect, it } from 'vitest';
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

  it('throws on an unknown profile name', () => {
    expect(() => sanitizeRichText('<p>x</p>', 'unknown-profile')).toThrow();
  });
});
