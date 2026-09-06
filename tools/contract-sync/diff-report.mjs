#!/usr/bin/env node
/**
 * So contracts/openapi/.staging/{module}.v{n}.json (runtime) với
 * contracts/openapi/{module}.v{n}.json (committed, nếu có), phân loại theo
 * §8 DesignIdeal/ai-agent-development-workflow.md, sinh docs/tasks/{ID}/contract-diff.md.
 *
 * Usage: node tools/contract-sync/diff-report.mjs <taskId> [moduleName ...]
 *        (không truyền module nào → dùng mọi file trong .staging/)
 */
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { normalizeDocument } from './lib/normalize.mjs';
import { classifyDiff } from './lib/diff.mjs';

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const STAGING_DIR = path.join(REPO_ROOT, 'contracts', 'openapi', '.staging');
const COMMITTED_DIR = path.join(REPO_ROOT, 'contracts', 'openapi');
const TASKS_DIR = path.join(REPO_ROOT, 'docs', 'tasks');

function loadJson(filePath) {
  return existsSync(filePath) ? JSON.parse(readFileSync(filePath, 'utf8')) : null;
}

function renderReport(taskId, perModule) {
  const lines = [`# ${taskId} — contract diff`, ''];

  const allBreaking = perModule.flatMap((m) => m.result.breaking.map((b) => ({ ...b, module: m.moduleFile })));
  const allRemoved = perModule.flatMap((m) => m.result.removed.map((r) => ({ ...r, module: m.moduleFile })));

  lines.push('## ⚠️ BREAKING / REMOVED');
  if (allBreaking.length === 0 && allRemoved.length === 0) {
    lines.push('(không có)');
  } else {
    for (const b of allBreaking) {
      lines.push(`- **BREAKING** \`${b.operation}\` (${b.module}) — nghi ngờ bug implementation, mặc định KHÔNG phải lý do tạo v2:`);
      for (const c of b.changes.filter((c) => c.kind === 'BREAKING')) {
        lines.push(`  - ${c.detail}`);
      }
    }
    for (const r of allRemoved) {
      lines.push(`- **REMOVED** \`${r.operation}\` (${r.module}) — endpoint có trong contract nhưng runtime không còn`);
    }
  }
  lines.push('');

  lines.push('## NEW_ENDPOINT');
  const allNew = perModule.flatMap((m) => m.result.newEndpoint.map((n) => n.operation));
  lines.push(...(allNew.length ? allNew.map((op) => `- ${op}`) : ['(không có)']));
  lines.push('');

  lines.push('## ADDITIVE');
  const allAdditive = perModule.flatMap((m) =>
    m.result.additive.map((a) => `- \`${a.operation}\`: ` + a.changes.map((c) => c.detail).join('; ')),
  );
  lines.push(...(allAdditive.length ? allAdditive : ['(không có)']));
  lines.push('');

  lines.push('## UNCHANGED');
  const unchangedCount = perModule.reduce((sum, m) => sum + m.result.unchanged.length, 0);
  lines.push(`${unchangedCount} operation không đổi.`);
  lines.push('');

  lines.push('## Giả định tôi đã tự đặt (không hỏi)');
  lines.push('(điền tay trước khi trình Gate 1 — script không tự suy luận được phần này)');
  lines.push('');

  lines.push('## Câu hỏi cần anh quyết');
  lines.push('(điền tay nếu có)');
  lines.push('');

  return lines.join('\n');
}

const [taskId, ...moduleFilter] = process.argv.slice(2);
if (!taskId) {
  console.error('Usage: node tools/contract-sync/diff-report.mjs <taskId> [moduleName ...]');
  process.exit(1);
}

if (!existsSync(STAGING_DIR)) {
  throw new Error(`Không tìm thấy ${STAGING_DIR}. Chạy export.mjs trước.`);
}

const stagingFiles = readdirSync(STAGING_DIR)
  .filter((f) => f.endsWith('.json'))
  .filter((f) => moduleFilter.length === 0 || moduleFilter.some((m) => f.startsWith(`${m}.`)));

if (stagingFiles.length === 0) {
  throw new Error(`Không có file .staging nào khớp filter: ${moduleFilter.join(',') || '(all)'}`);
}

const perModule = stagingFiles.map((moduleFile) => {
  const stagingDoc = loadJson(path.join(STAGING_DIR, moduleFile));
  const committedDoc = loadJson(path.join(COMMITTED_DIR, moduleFile));

  const normalizedNew = normalizeDocument(stagingDoc);
  const normalizedOld = committedDoc ? normalizeDocument(committedDoc) : null;

  return { moduleFile, result: classifyDiff(normalizedOld, normalizedNew) };
});

const report = renderReport(taskId, perModule);

const taskDir = path.join(TASKS_DIR, taskId);
mkdirSync(taskDir, { recursive: true });
const outputPath = path.join(taskDir, 'contract-diff.md');
writeFileSync(outputPath, report, 'utf8');

console.log(`[diff-report] -> ${path.relative(REPO_ROOT, outputPath)}`);
