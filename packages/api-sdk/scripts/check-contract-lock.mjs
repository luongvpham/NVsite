#!/usr/bin/env node
/**
 * Kiểm sha256 của contracts/openapi/*.json khớp contracts/contract.lock TRƯỚC khi chạy Orval.
 * Lệch → dừng (workflow §11). Không tự generate với contract chưa qua Gate 1.
 */
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const OPENAPI_DIR = path.join(REPO_ROOT, 'contracts', 'openapi');
const LOCK_FILE = path.join(REPO_ROOT, 'contracts', 'contract.lock');

if (!existsSync(LOCK_FILE)) {
  console.error(`[check-contract-lock] Không tìm thấy ${LOCK_FILE}. Chưa có contract nào được duyệt (Gate 1).`);
  process.exit(1);
}

const lock = JSON.parse(readFileSync(LOCK_FILE, 'utf8'));
const committedFiles = readdirSync(OPENAPI_DIR).filter((f) => f.endsWith('.json'));

let mismatch = false;

for (const file of committedFiles) {
  const entry = lock[file];
  if (!entry) {
    console.error(`[check-contract-lock] ${file} chưa có trong contract.lock — chưa qua Gate 1.`);
    mismatch = true;
    continue;
  }

  const actualSha256 = createHash('sha256').update(readFileSync(path.join(OPENAPI_DIR, file))).digest('hex');
  if (actualSha256 !== entry.sha256) {
    console.error(`[check-contract-lock] ${file} sha256 LỆCH với contract.lock.`);
    console.error(`  expected: ${entry.sha256}`);
    console.error(`  actual:   ${actualSha256}`);
    mismatch = true;
  }
}

if (mismatch) {
  console.error('[check-contract-lock] DỪNG — không chạy Orval với contract chưa khớp lock.');
  process.exit(1);
}

console.log('[check-contract-lock] OK — mọi contract khớp contract.lock.');
