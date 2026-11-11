import { getProfileTags, sanitizeRichText } from '../sanitize-html';

export type BlockKind = 'paragraph' | 'h2' | 'h3' | 'h4' | 'quote' | 'ul' | 'ol';

export interface ToolbarCapabilities {
  marks: { bold: boolean; italic: boolean; underline: boolean; strikethrough: boolean };
  link: boolean;
  /** Kiểu khối chọn được (rỗng = profile không có khối, vd. `inline`). */
  blocks: BlockKind[];
  /** Profile không có `<p>`: Enter chèn xuống dòng (`<br>`) thay vì tách đoạn. */
  enterInsertsLineBreak: boolean;
}

/** Toolbar chỉ bật thẻ mà profile cho phép — nút không có trong profile thì không tồn tại (không phải bị tắt). */
export function toolbarCapabilities(profile: string): ToolbarCapabilities {
  const tags = new Set(getProfileTags(profile));
  const blocks: BlockKind[] = [];
  if (tags.has('p')) blocks.push('paragraph');
  if (tags.has('h2')) blocks.push('h2');
  if (tags.has('h3')) blocks.push('h3');
  if (tags.has('h4')) blocks.push('h4');
  if (tags.has('blockquote')) blocks.push('quote');
  if (tags.has('ul') && tags.has('li')) blocks.push('ul');
  if (tags.has('ol') && tags.has('li')) blocks.push('ol');
  return {
    marks: {
      bold: tags.has('strong'),
      italic: tags.has('em'),
      underline: tags.has('u'),
      strikethrough: tags.has('s'),
    },
    link: tags.has('a'),
    blocks,
    enterInsertsLineBreak: !tags.has('p'),
  };
}

const hasVisibleContent = (html: string): boolean => /\S/.test(html.replace(/<(?!img)[^>]*>/gi, ''));

/**
 * Lexical thêm một `<br>` giữ chỗ sau xuống dòng ở cuối khối (Enter ở cuối dòng → `a<br><br>`). `<br>` cuối cùng của nội dung
 * không có nghĩa gì khi hiển thị nên bỏ đi — nếu không, mỗi lần Enter ở cuối lại tạo ra một giá trị khác và một op `update`.
 * Chỉ động tới `<br>` nằm cuối chuỗi (inline) hoặc ngay trước `</p>` cuối cùng; `<p><br /></p>` giữa nội dung (dòng trống) giữ nguyên.
 */
function trimTrailingBreaks(html: string): string {
  return html.replace(/(^|[^>]|<\/\w+>)(?:<br>)+$/, '$1').replace(/([^>]|<\/\w+>)(?:<br>)+(?=<\/p>$)/, '$1');
}

/**
 * Viết HTML theo đúng dạng serializer của BE (Ganss HtmlSanitizer/AngleSharp): `<br>` thay `<br />`, U+00A0 thành `&nbsp;`.
 * BE so chuỗi sau sanitize với chuỗi gửi lên — khác cách viết là bật `sanitized: true` → editor nạp lại sau MỖI lần lưu.
 * Đã đối chiếu bằng chính `RichTextSanitizer` của BE (WEBSITE-002 review M1); `tests/fixtures/richtext/be-canonical.json` chốt các mẫu.
 */
const NBSP = new RegExp(String.fromCharCode(0xa0), 'g');

function toBackendSerialization(html: string): string {
  return html.replace(/<br\s*\/?>/gi, '<br>').replace(NBSP, '&nbsp;');
}

/**
 * HTML thô của Lexical → giá trị lưu vào prop. Luôn qua `sanitizeRichText` (UX + lớp bảo vệ; BE vẫn là nguồn sự thật, #67).
 * - profile không có `<p>` (inline): các đoạn nối bằng `<br>` thay vì bị dính liền.
 * - không còn chữ nào (chỉ khung đoạn/`<br>` rỗng) → chuỗi rỗng, để xoá hết nội dung thật sự là `''` chứ không phải `<p><br /></p>`.
 */
export function normalizeEditorHtml(html: string, profile: string): string {
  const caps = toolbarCapabilities(profile);
  const prepared = caps.enterInsertsLineBreak ? html.replace(/<\/p>\s*<p\b[^>]*>/gi, '<br>') : html;
  const clean = trimTrailingBreaks(toBackendSerialization(sanitizeRichText(prepared, profile).trim()));
  return hasVisibleContent(clean) ? clean : '';
}
