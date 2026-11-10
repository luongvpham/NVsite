import { readFileSync } from 'node:fs';
import path from 'node:path';
import { $generateNodesFromDOM } from '@lexical/html';
import { $createParagraphNode, $getRoot, $insertNodes, createEditor, type LexicalEditor } from 'lexical';
import { describe, expect, it } from 'vitest';
import { classifyDangerous } from '../../tests/helpers/richtext-danger';
import { sanitizeRichText } from '../sanitize-html';
import { $importHtml, exportHtml, nodesForProfile } from './lexical-html';

function makeEditor(profile: string): LexicalEditor {
  return createEditor({
    namespace: 'test',
    nodes: nodesForProfile(profile),
    onError: (e) => {
      throw e;
    },
  });
}

/** Nạp như lúc mở khối (HTML đã lưu → sanitize → editor) rồi xuất. */
function roundTrip(html: string, profile: string): string {
  const editor = makeEditor(profile);
  editor.update(() => { $importHtml(editor, html, profile); }, { discrete: true });
  return exportHtml(editor, profile);
}

/**
 * Mô phỏng DÁN HTML thô: bỏ qua `$importHtml` (không sanitize trước), đưa node thẳng vào editor như trình duyệt làm với clipboard.
 * Lớp bảo vệ còn lại là bước xuất.
 */
function pasteRaw(html: string, profile: string): string {
  const editor = makeEditor(profile);
  editor.update(
    () => {
      const dom = new DOMParser().parseFromString(html, 'text/html');
      const nodes = $generateNodesFromDOM(editor, dom);
      const root = $getRoot();
      root.clear();
      const p = $createParagraphNode();
      root.append(p);
      p.select();
      $insertNodes(nodes);
    },
    { discrete: true },
  );
  return exportHtml(editor, profile);
}

interface XssVector {
  id: string;
  input: string;
  expect: 'reject' | 'clean' | 'pass';
  clean?: string;
  mustNotContain?: string[];
}
const fixture = JSON.parse(
  readFileSync(path.resolve(import.meta.dirname, '..', '..', 'tests', 'fixtures', 'tree', 'xss-richtext.json'), 'utf8'),
) as { vectors: XssVector[] };

/**
 * jsdom không gán `.style` cho phần tử MathML nên `$generateNodesFromDOM` của Lexical ném TypeError ở đúng vector này khi DÁN THÔ
 * (trình duyệt thật có `style`). Chỉ loại đường "dán thô" của vector đó; đường "nạp" (sanitize trước khi vào Lexical) vẫn chạy.
 */
const JSDOM_NO_MATHML_STYLE = new Set(['mxss-math-mglyph-style']);

describe('xss-richtext.json qua Lexical (cả hai profile, cả hai đường vào)', () => {
  const cases = fixture.vectors.flatMap((v) =>
    (['basic', 'inline'] as const).flatMap((profile) => [
      [`${v.id} · ${profile} · nạp`, v, profile, roundTrip] as const,
      ...(JSDOM_NO_MATHML_STYLE.has(v.id) ? [] : [[`${v.id} · ${profile} · dán thô`, v, profile, pasteRaw] as const]),
    ]),
  );

  it.each(cases)('%s → HTML ra sạch', (_name, vector, profile, run) => {
    const out = run(vector.input, profile);
    expect(classifyDangerous(out)).toBe('safe');
    expect(out).toBe(sanitizeRichText(out, profile)); // đã sạch: sanitize thêm lần nữa không đổi gì
    for (const bad of vector.mustNotContain ?? []) expect(out.toLowerCase()).not.toContain(bad.toLowerCase());
    expect(out).not.toMatch(/<(script|iframe|object|embed|style|img|svg)\b/i);
    expect(out).not.toMatch(/\son\w+\s*=/i);
    expect(out).not.toMatch(/(javascript|vbscript|data):/i);
  });
});

describe('round-trip HTML hợp lệ (không mất định dạng cho phép)', () => {
  const valid = [
    '<p>Xin chào <strong>bạn</strong></p>',
    '<p><em>nghiêng</em> <u>gạch chân</u> <s>gạch ngang</s></p>',
    '<p>Xem <a href="https://example.com/a">trang này</a>.</p>',
    '<p><a href="mailto:a@b.vn">mail</a> <a href="tel:0901234567">gọi</a></p>',
    '<h2>Tiêu đề</h2><p>nội dung</p>',
    '<h3>Ba</h3><h4>Bốn</h4>',
    '<ul><li>một</li><li>hai</li></ul>',
    '<ol><li>một</li><li>hai</li></ol>',
    '<blockquote>trích dẫn</blockquote>',
    '<p>a &amp; b &lt; c</p>',
    '<p>dòng 1<br />dòng 2</p>',
  ];

  it.each(valid)('basic: %s', (html) => {
    expect(roundTrip(html, 'basic')).toBe(html);
  });

  it('inline: định dạng chữ, link và xuống dòng còn nguyên', () => {
    const html = 'Xin <strong>chào</strong> <em>bạn</em><br />xem <a href="https://x.vn">đây</a>';
    expect(roundTrip(html, 'inline')).toBe(html);
  });

  it('nạp lần hai từ chính kết quả → không đổi (idempotent)', () => {
    const once = roundTrip('<h2>T</h2><ul><li>a</li></ul><p><strong>b</strong></p>', 'basic');
    expect(roundTrip(once, 'basic')).toBe(once);
  });

  it('dữ liệu cũ dạng "chữ + <p>" (bản sạch của fixture reformatted) vẫn nạp được, chữ không mất', () => {
    const out = roundTrip('Tiêu đề<p>Xin chào</p>', 'basic');
    expect(out).toContain('Tiêu đề');
    expect(out).toContain('Xin chào');
  });

  it('HTML thô của Lexical (class/style/dir/span, h1) → bản sạch giữ chữ', () => {
    const out = roundTrip(
      '<h1>Tiêu đề</h1><p class="PlaygroundEditorTheme__paragraph" dir="ltr"><span style="white-space: pre-wrap">Xin chào</span></p>',
      'basic',
    );
    expect(out).toBe('<p>Tiêu đề</p><p>Xin chào</p>');
  });
});

describe('profile inline: không có heading/list/đoạn', () => {
  it('HTML có h2/ul/blockquote/p nạp vào → chỉ còn chữ (+ định dạng chữ), không thẻ khối', () => {
    const out = roundTrip(
      '<h2>Tiêu đề</h2><ul><li>một</li><li>hai</li></ul><blockquote>q</blockquote><p>đoạn <strong>đậm</strong></p>',
      'inline',
    );
    expect(out).not.toMatch(/<(h[1-6]|ul|ol|li|blockquote|p)\b/i);
    for (const word of ['Tiêu đề', 'một', 'hai', 'q', 'đoạn', '<strong>đậm</strong>']) expect(out).toContain(word);
  });

  it('dán h2/ul thô vào editor inline cũng không ra thẻ khối', () => {
    const out = pasteRaw('<h2>Tiêu đề</h2><ul><li>một</li></ul>', 'inline');
    expect(out).not.toMatch(/<(h[1-6]|ul|ol|li|p)\b/i);
    expect(out).toContain('Tiêu đề');
  });

  it('chỉ đăng ký node mà profile cần', () => {
    const names = (p: string) => nodesForProfile(p).map((n) => n.getType());
    expect(names('inline')).toEqual(['link']);
    expect(names('basic')).toEqual(expect.arrayContaining(['link', 'heading', 'quote', 'list', 'listitem']));
  });
});
