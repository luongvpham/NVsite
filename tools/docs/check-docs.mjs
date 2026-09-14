#!/usr/bin/env node
// Năm khẳng định về tài liệu. Loại lỗi này không test nào bắt được, và nó là loại
// làm agent tra sai rồi tự tin đi tiếp — đắt hơn nhiều so với một test đỏ.
//
//   node tools/docs/check-docs.mjs

import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT, existsExact, allBanners } from './lib/doc-status.mjs';

const SCAN_ROOTS = ['DesignIdeal', 'Docs', 'backend/docs', 'CLAUDE.md', 'backend/CLAUDE.md',
  'apps/web/CLAUDE.md', 'apps/portal/CLAUDE.md',
  'packages/builder-components/CLAUDE.md', 'packages/builder-renderer/CLAUDE.md'];

const DECISIONS = 'DesignIdeal/DECISIONS.md';
const TASKS_DIR = path.join(REPO_ROOT, 'Docs/tasks');

const failures = [];
const warnings = [];
const fail = (check, msg) => failures.push(`[${check}] ${msg}`);
const warn = (check, msg) => warnings.push(`[${check}] ${msg}`);

function walkMarkdown(rel) {
  const abs = path.join(REPO_ROOT, rel);
  if (!fs.existsSync(abs)) return [];
  if (fs.statSync(abs).isFile()) return rel.endsWith('.md') ? [rel] : [];
  return fs.readdirSync(abs).flatMap((entry) =>
    entry === 'node_modules' ? [] : walkMarkdown(`${rel}/${entry}`),
  );
}

const mdFiles = SCAN_ROOTS.flatMap(walkMarkdown);
const readRel = (rel) => fs.readFileSync(path.join(REPO_ROOT, rel), 'utf8');

// ─── [1] Banner hợp lệ + [2] changelog trỏ tới file có thật ────────────────────
const banners = allBanners();
for (const b of banners) {
  if (b.error) {
    fail('banner', `DesignIdeal/${b.file} — ${b.error}`);
    continue;
  }
  if (b.changelog && !existsExact(b.changelog)) {
    fail('banner', `DesignIdeal/${b.file} — Changelog trỏ '${b.changelog}' nhưng file đó không tồn tại (hoặc sai chữ hoa/thường)`);
  }
  if (b.status === 'IMPLEMENTED' && !b.changelog) {
    fail('banner', `DesignIdeal/${b.file} — STATUS=IMPLEMENTED mà không khai Changelog (Definition of Done mục 9)`);
  }
}

// ─── [3] Mọi quyết định #N được tham chiếu đều có dòng trong DECISIONS.md ──────
const decisionsRaw = readRel(DECISIONS);
const declared = new Set(
  [...decisionsRaw.matchAll(/^\|\s*\*\*#(\d{1,2}(?:\.\d)?)\*\*/gm)].map((m) => m[1]),
);

// `§25 #2` nghĩa là "mục 2 của §25", KHÔNG phải Quyết định #2 — bỏ qua dạng đó.
const SUBITEM_PREFIX = /§\s*\d+(?:\.\d+)?\s*$/;

for (const file of mdFiles) {
  if (file === DECISIONS) continue;
  const text = readRel(file);
  for (const m of text.matchAll(/#(\d{1,2}(?:\.\d)?)\b/g)) {
    const num = m[1];
    const base = Number.parseInt(num, 10);
    // KHÔNG chặn trần ở số quyết định lớn nhất hiện có — chặn thế thì #69 mới cấp mà quên
    // ghi dòng sẽ lọt đúng lúc cần bắt nhất. Chỉ loại 0 và số quá lớn để không nhận nhầm.
    if (base < 1 || base > 99) continue;
    if (SUBITEM_PREFIX.test(text.slice(Math.max(0, m.index - 12), m.index))) continue;
    if (!declared.has(num)) {
      fail('decision-ref', `${file} tham chiếu #${num} nhưng ${DECISIONS} không có dòng nào cho nó`);
    }
  }
}

// ─── [4] Task đã qua Gate 1 phải có changelog.md ───────────────────────────────
// Bằng chứng "task này thật sự chạm code": có brief.md hoặc contract-diff.md.
// Miễn trừ: đặt file `.no-changelog` trong thư mục task, dòng đầu ghi lý do.
if (fs.existsSync(TASKS_DIR)) {
  for (const id of fs.readdirSync(TASKS_DIR)) {
    const dir = path.join(TASKS_DIR, id);
    if (!fs.statSync(dir).isDirectory()) continue;
    const files = fs.readdirSync(dir);
    const wentThroughGate1 = files.includes('brief.md') || files.includes('contract-diff.md');
    if (!wentThroughGate1 || files.includes('changelog.md')) continue;

    if (files.includes('.no-changelog')) {
      const reason = fs.readFileSync(path.join(dir, '.no-changelog'), 'utf8').trim().split('\n')[0];
      if (!reason) fail('task-changelog', `Docs/tasks/${id}/.no-changelog rỗng — phải ghi lý do ở dòng đầu`);
      continue;
    }
    fail(
      'task-changelog',
      `Docs/tasks/${id}/ đã qua Gate 1 (có ${files.filter((f) => f === 'brief.md' || f === 'contract-diff.md').join(', ')}) nhưng THIẾU changelog.md — Definition of Done mục 9`,
    );
  }
}

// ─── [5] Đường dẫn trong .md ───────────────────────────────────────────────────
// Hai mức, cố ý khác nhau:
//   - Tồn tại nhưng SAI CHỮ HOA  → FAIL. Chắc chắn là bug; Windows tha, CI Linux vỡ.
//   - Không tồn tại chút nào     → WARN. Có thể là forward reference trong plan.md,
//                                  hoặc tên file nhắc trong câu văn — không đủ chắc để chặn build.
//
// Đường dẫn trong backtick chỉ được kiểm khi nó bắt đầu bằng một thư mục top-level đã biết.
// Phần lớn backtick trong repo này viết tương đối theo phạm vi của chính tài liệu
// (`meta/prop-kinds.ts` trong builder-components, `Api/Program.cs` trong backend) — không có
// cách nào biết base, nên không đoán.
const TOP_LEVEL = /^(DesignIdeal|Docs|docs|config|contracts|packages|apps|backend|tools)\//;
const SKIP = /[{}<>*…]|^https?:|^mailto:|^#/;
const FILE_LIKE = /^[\w./@-]+\.(md|json|ts|tsx|cs|mjs|js|yml|yaml|csproj|sln)$/;

/** Tồn tại nếu bỏ qua chữ hoa/thường → dùng để phân biệt "sai case" với "không có". */
function resolveIgnoringCase(relPath) {
  const segments = relPath.split('/').filter((s) => s && s !== '.');
  let current = REPO_ROOT;
  const actual = [];
  for (const segment of segments) {
    let entries;
    try {
      entries = fs.readdirSync(current);
    } catch {
      return null;
    }
    const hit = entries.find((e) => e.toLowerCase() === segment.toLowerCase());
    if (!hit) return null;
    actual.push(hit);
    current = path.join(current, hit);
  }
  return actual.join('/');
}

for (const file of mdFiles) {
  const dir = path.posix.dirname(file);
  const text = readRel(file);
  const seen = new Set();

  const candidates = [
    // Link markdown LUÔN tương đối với chính tài liệu — đó là chuẩn markdown.
    ...[...text.matchAll(/\]\(([^)\s]+)\)/g)].map((m) => ({ raw: m[1], relative: true })),
    // Backtick: chỉ nhận dạng repo-root-relative.
    ...[...text.matchAll(/`([^`\n]+)`/g)]
      .map((m) => m[1])
      .filter((s) => FILE_LIKE.test(s) && TOP_LEVEL.test(s))
      .map((raw) => ({ raw, relative: false })),
  ];

  for (const { raw, relative } of candidates) {
    const target = raw.split('#')[0];
    if (!target || SKIP.test(target) || seen.has(target)) continue;
    seen.add(target);

    const rel = path.posix.normalize(relative ? path.posix.join(dir, target) : target);
    if (rel.startsWith('..') || existsExact(rel)) continue;

    const actual = resolveIgnoringCase(rel);
    if (actual) {
      fail('path-case', `${file} → '${target}' SAI CHỮ HOA. Đúng phải là: '${actual}'`);
    } else {
      warn('path', `${file} → '${target}' không tồn tại`);
    }
  }
}

// ─── Báo cáo ───────────────────────────────────────────────────────────────────
for (const w of warnings) console.warn(`⚠ ${w}`);
if (warnings.length) console.warn('');

if (failures.length) {
  for (const f of failures) console.error(`✗ ${f}`);
  console.error(`\n${failures.length} lỗi tài liệu.`);
  process.exit(1);
}

console.log(`✓ Tài liệu OK — ${mdFiles.length} file .md, ${banners.length} banner, ${declared.size} quyết định.`);
if (warnings.length) console.log(`  (${warnings.length} cảnh báo ở trên, không chặn build)`);
