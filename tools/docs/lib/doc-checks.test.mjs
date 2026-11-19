// node --test tools/docs/lib/doc-checks.test.mjs
import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  bannersMissingTask,
  declaredDecisions,
  decisionRefs,
  parentTaskId,
  strayBacktickInBanner,
} from './doc-checks.mjs';

// ─── (c) #100 trở lên ──────────────────────────────────────────────────────────

test('declaredDecisions nhận cả số ba chữ số và #N.M', () => {
  const raw = ['| **#7** | a |', '| **#21.4** | b |', '| **#110** | c |', 'text **#999** ngoài bảng'].join('\n');
  assert.deepEqual([...declaredDecisions(raw)].sort(), ['110', '21.4', '7']);
});

test('decisionRefs bắt #100+ — trước TOOLING-004 #110 lọt kiểm', () => {
  assert.deepEqual(decisionRefs('theo #110 và #21.4, xem #5'), ['110', '21.4', '5']);
});

test('decisionRefs bỏ §25 #2 (mục con), #0, số quá lớn và màu hex', () => {
  assert.deepEqual(decisionRefs('§25 #2 · § 6.1 #3 · #0 · #1000 · #0a0 · #ffffff · #123abc · color: #333'), []);
});

test('decisionRefs bỏ số PR/issue, entity HTML, anchor link (review W1)', () => {
  assert.deepEqual(decisionRefs('PR #130 · PR #25 và #26 · issue #140 · &#123; · [x](a.md#105-foo)'), []);
});

test('decisionRefs vẫn bắt dạng thường gặp: (#21), "#21:", đầu dòng', () => {
  assert.deepEqual(decisionRefs('(#21) · #89: gọn · \n#110 ở đầu dòng · **#102**'), ['21', '89', '110', '102']);
});

// ─── (b) backtick lẻ trong banner ───────────────────────────────────────────────

const banner = (stale) =>
  `# T\n\n> **STATUS:** \`IMPLEMENTED\` · **Tasks:** \`A-1\` · **Changelog:** \`x.md\` · **Stale:** \`${stale}\`\n`;

test('banner chẵn backtick → ổn', () => {
  assert.equal(strayBacktickInBanner(banner('—')), null);
});

test('banner có backtick lẻ → báo', () => {
  assert.match(strayBacktickInBanner(banner('đọc `05 §6 trước')), /9 backtick \(lẻ\)/);
});

test('không có banner → không báo ở đây (kiểm [1] lo)', () => {
  assert.equal(strayBacktickInBanner('# T\n\nkhông banner'), null);
});

// ─── (a) changelog trỏ DesignIdeal/0X → banner có mã task ──────────────────────

const banners = [
  { file: '05-website-builder-and-product-design.md', tasks: ['WEBSITE-005', 'MEDIA-001'] },
  { file: '08-media-asset-design.md', tasks: ['MEDIA-001'] },
  { file: 'ai-agent-development-workflow.md', tasks: [] },
];

test('changelog trỏ tới 0X mà banner thiếu mã task → báo', () => {
  const changelogText = 'Sửa [`08`](../../../DesignIdeal/08-media-asset-design.md) §3';
  assert.deepEqual(bannersMissingTask({ taskId: 'WEBSITE-006', changelogText, banners }), [
    { file: '08-media-asset-design.md', taskId: 'WEBSITE-006' },
  ]);
});

test('banner đã có mã task → không báo', () => {
  const changelogText = '`DesignIdeal/05-website-builder-and-product-design.md`';
  assert.deepEqual(bannersMissingTask({ taskId: 'WEBSITE-005', changelogText, banners }), []);
});

test('vòng D{n} tính theo task cha (workflow §6.1)', () => {
  assert.equal(parentTaskId('MEDIA-001-D3'), 'MEDIA-001');
  assert.equal(parentTaskId('MEDIA-001-ACCEPT'), 'MEDIA-001-ACCEPT');
  const changelogText = 'DesignIdeal/08-media-asset-design.md';
  assert.deepEqual(bannersMissingTask({ taskId: 'MEDIA-001-D3', changelogText, banners }), []);
});

test('tài liệu quy trình (không đánh số 0X) không đòi Tasks', () => {
  const changelogText = 'DesignIdeal/ai-agent-development-workflow.md §9';
  assert.deepEqual(bannersMissingTask({ taskId: 'TOOLING-004', changelogText, banners }), []);
});

test('duplicateDecisions bắt số cấp trùng (review S3)', async () => {
  const { duplicateDecisions } = await import('./doc-checks.mjs');
  assert.deepEqual(duplicateDecisions('| **#113** | a |\n| **#7** | b |\n| **#113** | c |'), ['113']);
  assert.deepEqual(duplicateDecisions('| **#1** | a |\n| **#2** | b |'), []);
});
