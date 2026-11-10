import { describe, expect, it } from 'vitest';
import { normalizeEditorHtml, toolbarCapabilities } from './profile';

describe('toolbarCapabilities — suy ra từ config/sanitize-profiles.json', () => {
  it('inline: chỉ định dạng chữ + link, KHÔNG có khối; Enter = xuống dòng', () => {
    expect(toolbarCapabilities('inline')).toEqual({
      marks: { bold: true, italic: true, underline: true, strikethrough: true },
      link: true,
      blocks: [],
      enterInsertsLineBreak: true,
    });
  });

  it('basic: có đoạn/h2–h4/trích dẫn/danh sách; Enter tách đoạn', () => {
    const caps = toolbarCapabilities('basic');
    expect(caps.blocks).toEqual(['paragraph', 'h2', 'h3', 'h4', 'quote', 'ul', 'ol']);
    expect(caps.link).toBe(true);
    expect(caps.enterInsertsLineBreak).toBe(false);
  });

  it('không bao giờ có h1/h5/h6 (ngoài whitelist) và profile lạ / "dangerous" bị từ chối', () => {
    expect(toolbarCapabilities('basic').blocks).not.toContain('h1');
    expect(() => toolbarCapabilities('nope')).toThrow(/profile/);
    expect(() => toolbarCapabilities('dangerous')).toThrow(/profile/);
  });
});

describe('normalizeEditorHtml', () => {
  it('bỏ class/style/dir/span và b/i của Lexical, giữ strong/em và chữ', () => {
    const raw =
      '<p class="x" dir="ltr"><b><strong style="white-space: pre-wrap;">đậm</strong></b> <i><em>nghiêng</em></i> <span style="white-space: pre-wrap;">thường</span></p>';
    expect(normalizeEditorHtml(raw, 'basic')).toBe('<p><strong>đậm</strong> <em>nghiêng</em> thường</p>');
  });

  it('inline: nhiều đoạn nối bằng <br>, không dính liền, không còn <p>', () => {
    expect(normalizeEditorHtml('<p>dòng 1</p><p>dòng 2</p>', 'inline')).toBe('dòng 1<br />dòng 2');
  });

  it('không còn chữ nào → chuỗi rỗng', () => {
    expect(normalizeEditorHtml('<p><br></p>', 'basic')).toBe('');
    expect(normalizeEditorHtml('<p></p><p><br></p>', 'inline')).toBe('');
    expect(normalizeEditorHtml('', 'basic')).toBe('');
  });

  it('<br> giữ chỗ cuối nội dung bị bỏ; dòng trống ở giữa và xuống dòng giữa chữ giữ nguyên', () => {
    expect(normalizeEditorHtml('<p>a<br><br></p>', 'inline')).toBe('a');
    expect(normalizeEditorHtml('a<br>b<br><br>', 'inline')).toBe('a<br />b');
    expect(normalizeEditorHtml('<p>a<br>b<br></p>', 'basic')).toBe('<p>a<br />b</p>');
    expect(normalizeEditorHtml('<p>a</p><p><br></p><p>b</p>', 'basic')).toBe('<p>a</p><p><br /></p><p>b</p>');
  });

  it('chữ không bao giờ bị mất khi thẻ bị loại (h1 ngoài whitelist)', () => {
    expect(normalizeEditorHtml('<h1>Tiêu đề</h1>', 'basic')).toBe('Tiêu đề');
  });
});
