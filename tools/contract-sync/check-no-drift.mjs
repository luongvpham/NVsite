#!/usr/bin/env node
/**
 * CI check (Gate 2 checklist: "contract-sync sau cùng: không còn diff ngoài contract đã duyệt").
 * So contracts/openapi/.staging/{module}.v{n}.json (runtime, đã export) với
 * contracts/openapi/{module}.v{n}.json (committed). Có bất kỳ diff nào ngoài UNCHANGED → fail.
 *
 * Giả định export.mjs đã chạy trước bước này (xem .github/workflows/ci.yml).
 */
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { normalizeDocument } from './lib/normalize.mjs';
import { classifyDiff, documentsEqual, firstDifference } from './lib/diff.mjs';

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const STAGING_DIR = path.join(REPO_ROOT, 'contracts', 'openapi', '.staging');
const COMMITTED_DIR = path.join(REPO_ROOT, 'contracts', 'openapi');

const committedFiles = readdirSync(COMMITTED_DIR).filter((f) => f.endsWith('.json'));

let hasDrift = false;

for (const file of committedFiles) {
  const stagingPath = path.join(STAGING_DIR, file);
  if (!existsSync(stagingPath)) {
    console.error(`[check-no-drift] ${file} có trong committed nhưng không thấy trong .staging — export.mjs đã chạy chưa?`);
    hasDrift = true;
    continue;
  }

  const committedDoc = normalizeDocument(JSON.parse(readFileSync(path.join(COMMITTED_DIR, file), 'utf8')));
  const stagingDoc = normalizeDocument(JSON.parse(readFileSync(stagingPath, 'utf8')));

  const result = classifyDiff(committedDoc, stagingDoc);
  const drift = [...result.newEndpoint, ...result.additive, ...result.breaking, ...result.removed];

  if (drift.length > 0) {
    console.error(`[check-no-drift] ${file} lệch với contract đã duyệt:`);
    for (const d of drift) {
      console.error(`  - ${d.operation}`);
      for (const c of d.changes ?? []) console.error(`      ${c.kind}: ${c.detail}`);
    }
    hasDrift = true;
  } else if (!documentsEqual(committedDoc, stagingDoc)) {
    // Cổng chặn cuối (TOOLING-001): classifier là để BÁO CÁO cho người đọc, không phải để quyết định
    // pass/fail. Document khác nhau ở chỗ không thuộc operation nào (component không ai dùng,
    // securitySchemes, tags…) vẫn là lệch so với bản đã duyệt.
    console.error(`[check-no-drift] ${file} khác contract đã duyệt ở chỗ ngoài các operation — lệch đầu tiên tại ${firstDifference(committedDoc, stagingDoc)}.`);
    hasDrift = true;
  }
}

if (hasDrift) {
  console.error('[check-no-drift] FAIL — runtime lệch với contract đã duyệt. Cần chạy lại bước sync + Gate 1.');
  process.exit(1);
}

console.log('[check-no-drift] OK — không còn diff ngoài contract đã duyệt.');
