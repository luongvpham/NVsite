// Đọc/parse banner STATUS ở đầu mỗi file DesignIdeal/*.md, và kiểm tra đường dẫn
// PHÂN BIỆT HOA THƯỜNG (Windows/NTFS không phân biệt — fs.existsSync('docs/x') trả true
// cho 'Docs/x', nên CI Linux sẽ vỡ ở chỗ máy dev thấy xanh).

import fs from 'node:fs';
import path from 'node:path';

export const REPO_ROOT = path.resolve(import.meta.dirname, '../../..');
export const DESIGN_DIR = path.join(REPO_ROOT, 'DesignIdeal');

/** File trong DesignIdeal/ KHÔNG cần banner — chúng là chỉ mục, không phải tài liệu thiết kế. */
export const INDEX_FILES = new Set(['00-INDEX.md', 'DECISIONS.md']);

/** Tập đóng. Thêm giá trị mới = sửa cả bảng này và §2 của 00-INDEX sẽ tự đổi theo. */
export const STATUS_LABELS = {
  STABLE: '✅ Ổn định',
  SPEC: '📐 Thiết kế, chưa code',
  IMPLEMENTED: '⚠️ Đã code, có lệch có chủ đích',
  INCOMPLETE_DOC: '⚠️ Tài liệu thiếu phần',
  EXTERNAL_REF: '⛔ Tham chiếu ngoại lai',
  PROCESS: '✅ Quy trình đang hiệu lực',
};

const BANNER_RE =
  /^> \*\*STATUS:\*\* `([^`]+)` · \*\*Tasks:\*\* `([^`]+)` · \*\*Changelog:\*\* `([^`]+)` · \*\*Stale:\*\* `([^`]+)`/m;

const DASH = '—';

/**
 * Tồn tại VÀ đúng chữ hoa/thường từng segment.
 * Đi bộ từng segment và so khớp chính xác với readdirSync của thư mục cha.
 */
export function existsExact(relPath) {
  const segments = relPath.split('/').filter((s) => s && s !== '.');
  let current = REPO_ROOT;
  for (const segment of segments) {
    let entries;
    try {
      entries = fs.readdirSync(current);
    } catch {
      return false;
    }
    if (!entries.includes(segment)) return false;
    current = path.join(current, segment);
  }
  return true;
}

/** Danh sách file DesignIdeal cần có banner, theo thứ tự tên file. */
export function designDocs() {
  return fs
    .readdirSync(DESIGN_DIR)
    .filter((f) => f.endsWith('.md') && !INDEX_FILES.has(f))
    .sort();
}

/**
 * @returns {{file: string, status: string, tasks: string[], changelog: string|null,
 *            stale: string|null, error: string|null}}
 */
export function parseBanner(file) {
  const raw = fs.readFileSync(path.join(DESIGN_DIR, file), 'utf8');
  // Chỉ quét đầu file — banner phải nằm ngay sau tiêu đề, không lẫn với nội dung.
  const head = raw.split('\n').slice(0, 20).join('\n');
  const m = head.match(BANNER_RE);

  if (!m) {
    return { file, status: null, tasks: [], changelog: null, stale: null, error: 'thiếu banner STATUS hợp lệ trong 20 dòng đầu' };
  }

  const [, status, tasksRaw, changelogRaw, staleRaw] = m;

  if (!(status in STATUS_LABELS)) {
    return { file, status, tasks: [], changelog: null, stale: null, error: `STATUS '${status}' không thuộc tập đóng: ${Object.keys(STATUS_LABELS).join(', ')}` };
  }

  return {
    file,
    status,
    tasks: tasksRaw === DASH ? [] : tasksRaw.split(',').map((s) => s.trim()).filter(Boolean),
    changelog: changelogRaw === DASH ? null : changelogRaw.trim(),
    stale: staleRaw === DASH ? null : staleRaw.trim(),
    error: null,
  };
}

export function allBanners() {
  return designDocs().map(parseBanner);
}
