#!/usr/bin/env node
// Sinh lại §2 "Trạng thái từng tài liệu" của DesignIdeal/00-INDEX.md từ banner STATUS
// của từng file DesignIdeal/*.md.
//
// Banner là NGUỒN DUY NHẤT. Bảng trong 00-INDEX là output — đừng sửa tay, sửa banner rồi chạy lại.
// Chỉ §2 được sinh; §1 (bảng tra), §3 (tiến độ step), §4 (việc đang mở), §5 (quy ước) viết tay,
// vì chúng không suy ra được từ filesystem — sinh bừa những thứ đó còn tệ hơn không sinh.
//
//   node tools/docs/gen-doc-index.mjs            → ghi lại 00-INDEX.md
//   node tools/docs/gen-doc-index.mjs --check    → không ghi, exit 1 nếu lệch

import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT, STATUS_LABELS, allBanners } from './lib/doc-status.mjs';

const INDEX_PATH = path.join(REPO_ROOT, 'DesignIdeal/00-INDEX.md');
const START = '<!-- GEN:START doc-status -->';
const END = '<!-- GEN:END doc-status -->';

const check = process.argv.includes('--check');

function cell(value) {
  return value ? value.replaceAll('|', '\\|') : '—';
}

function changelogCell(banner) {
  if (banner.changelog) {
    const rel = path.relative('DesignIdeal', banner.changelog).replaceAll('\\', '/');
    return `[\`${path.basename(path.dirname(banner.changelog))}/changelog.md\`](${rel})`;
  }
  // Đã code mà không có changelog = vi phạm Definition of Done mục 9.
  return banner.status === 'IMPLEMENTED' ? '❌ **THIẾU**' : '—';
}

function renderTable() {
  const banners = allBanners();
  const bad = banners.filter((b) => b.error);
  if (bad.length) {
    for (const b of bad) console.error(`  ✗ ${b.file}: ${b.error}`);
    console.error('\nSửa banner rồi chạy lại. Grammar:');
    console.error('> **STATUS:** `ENUM` · **Tasks:** `A,B` · **Changelog:** `path` · **Stale:** `text`');
    process.exit(1);
  }

  const rows = banners.map((b) => {
    const tasks = b.tasks.length ? b.tasks.join(', ') : '—';
    return `| \`${b.file}\` | ${STATUS_LABELS[b.status]} | ${tasks} | ${changelogCell(b)} | ${cell(b.stale)} |`;
  });

  return [
    '',
    '<!-- Bảng này SINH TỰ ĐỘNG từ banner STATUS của từng file. Đừng sửa tay —',
    '     sửa banner ở đầu file tương ứng rồi chạy `pnpm gen:doc-index`. -->',
    '',
    '| File | Trạng thái | Task đã chạm | Changelog | Đừng tin ở |',
    '|---|---|---|---|---|',
    ...rows,
    '',
  ].join('\n');
}

const current = fs.readFileSync(INDEX_PATH, 'utf8');
const startIdx = current.indexOf(START);
const endIdx = current.indexOf(END);

if (startIdx === -1 || endIdx === -1 || endIdx < startIdx) {
  console.error(`✗ Không tìm thấy cặp marker trong ${path.relative(REPO_ROOT, INDEX_PATH)}:`);
  console.error(`    ${START}`);
  console.error(`    ${END}`);
  process.exit(1);
}

const next =
  current.slice(0, startIdx + START.length) + renderTable() + current.slice(endIdx);

if (next === current) {
  console.log('✓ 00-INDEX.md §2 đã khớp banner.');
  process.exit(0);
}

if (check) {
  console.error('✗ 00-INDEX.md §2 KHÔNG khớp banner STATUS của các file DesignIdeal.');
  console.error('  Ai đó sửa banner mà quên sinh lại index, hoặc sửa tay bảng đã sinh.');
  console.error('  Chạy: pnpm gen:doc-index');
  process.exit(1);
}

fs.writeFileSync(INDEX_PATH, next);
console.log('✓ Đã sinh lại 00-INDEX.md §2.');
