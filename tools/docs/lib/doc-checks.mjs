// Hàm thuần cho check-docs.mjs — tách ra để test được (N2, TOOLING-004).
// Không đọc file ở đây: check-docs.mjs đọc rồi đưa text vào.

/** `#N`, `#N.M`. Ba chữ số — trước TOOLING-004 regex dừng ở hai chữ số nên mọi #100+ lọt kiểm. */
const DECISION_ROW = /^\|\s*\*\*#(\d{1,3}(?:\.\d)?)\*\*/gm;
// `#` dính sau chữ/số/`&`/`/`/`.` không phải quyết định: anchor `a.md#105-x`, entity `&#123;`.
const DECISION_REF = /(?<![\w&/.])#(\d{1,3}(?:\.\d)?)\b/g;
/** `§25 #2` nghĩa là "mục 2 của §25", KHÔNG phải Quyết định #2 — bỏ qua dạng đó. */
const SUBITEM_PREFIX = /§\s*\d+(?:\.\d+)?\s*$/;
/** Số PR/issue (sẽ vượt #112) và màu hex chỉ có chữ số (`color: #333`). */
const NOT_DECISION_PREFIX = /(?:\b(?:PR|pull|issue)s?\s*(?:#\d+\s*(?:,|và|and)\s*)*|(?:color|background|fill|stroke)[\w-]*:\s*)$/i;

/** Số quyết định có dòng trong bảng của DECISIONS.md. */
export function declaredDecisions(decisionsRaw) {
  return new Set([...decisionsRaw.matchAll(DECISION_ROW)].map((m) => m[1]));
}

/** Mọi `#N` trong text được hiểu là tham chiếu quyết định. */
export function decisionRefs(text) {
  const refs = [];
  for (const m of text.matchAll(DECISION_REF)) {
    const base = Number.parseInt(m[1], 10);
    // KHÔNG chặn trần ở số quyết định lớn nhất hiện có — chặn thế thì số mới cấp mà quên
    // ghi dòng sẽ lọt đúng lúc cần bắt nhất. Chỉ loại 0 và số quá lớn để không nhận nhầm.
    if (base < 1 || base > 999) continue;
    if (SUBITEM_PREFIX.test(text.slice(Math.max(0, m.index - 12), m.index))) continue;
    if (NOT_DECISION_PREFIX.test(text.slice(Math.max(0, m.index - 40), m.index))) continue;
    refs.push(m[1]);
  }
  return refs;
}

/**
 * Dòng `> **STATUS:**` có số backtick lẻ → một trường bị cắt sai chỗ (BANNER_RE dùng `[^`]+`),
 * phần còn lại của banner bị đọc lệch mà không báo gì.
 * @returns {string|null} thông báo lỗi, hoặc null nếu ổn
 */
export function strayBacktickInBanner(fileText) {
  const line = fileText.split('\n').slice(0, 20).find((l) => l.startsWith('> **STATUS:**'));
  if (!line) return null; // thiếu banner do kiểm [1] báo
  const count = (line.match(/`/g) ?? []).length;
  return count % 2 === 0 ? null : `dòng STATUS có ${count} backtick (lẻ) — có backtick thừa hoặc thiếu`;
}

const NUMBERED_DESIGN_DOC = /^\d{2}-/;

/** `MEDIA-001-D3` → `MEDIA-001`: vòng D{n} thuộc task cha (workflow §6.1). */
export function parentTaskId(taskId) {
  return taskId.replace(/-D\d+$/, '');
}

/**
 * Tài liệu DesignIdeal có banner mà changelog nhắc tới (link hoặc backtick, theo tên file).
 * @param {string} changelogText
 * @param {Iterable<string>} bannerFiles tên file như `05-website-builder-and-product-design.md`
 */
export function designDocsReferenced(changelogText, bannerFiles) {
  return [...bannerFiles].filter((file) => changelogText.includes(file));
}

/**
 * Changelog của task trỏ tới `DesignIdeal/0X` mà banner 0X không có mã task ở `Tasks:` →
 * người tra 00-INDEX §2 không biết tài liệu đã bị task đó chạm.
 * @param {{taskId: string, changelogText: string, banners: {file: string, tasks: string[]}[]}} input
 * @returns {{file: string, taskId: string}[]}
 */
export function bannersMissingTask({ taskId, changelogText, banners }) {
  const id = parentTaskId(taskId);
  // Chỉ tài liệu thiết kế đánh số `0X-…`. Tài liệu quy trình (PROCESS) không ghi Tasks.
  const byFile = new Map(banners.filter((b) => NUMBERED_DESIGN_DOC.test(b.file)).map((b) => [b.file, b]));
  return designDocsReferenced(changelogText, byFile.keys())
    .filter((file) => !byFile.get(file).tasks.map(parentTaskId).includes(id))
    .map((file) => ({ file, taskId: id }));
}

/**
 * Số quyết định có từ hai dòng trở lên. Nhánh song song cùng cấp "max + 1" thì trùng số;
 * merge chưa chắc conflict nếu hai dòng nằm khác chỗ (review S3, TOOLING-004).
 */
export function duplicateDecisions(decisionsRaw) {
  const seen = new Set();
  const dup = new Set();
  for (const m of decisionsRaw.matchAll(DECISION_ROW)) (seen.has(m[1]) ? dup : seen).add(m[1]);
  return [...dup];
}
