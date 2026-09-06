#!/usr/bin/env node
/**
 * Promote contract sau khi Gate 1 duyệt (workflow §9). Đây là HÀNH ĐỘNG RIÊNG,
 * tách khỏi export/diff — chỉ chạy sau khi người đã đọc contract-diff.md và đồng ý.
 *
 * Copy contracts/openapi/.staging/{module}.v{n}.json → contracts/openapi/{module}.v{n}.json,
 * cập nhật contracts/contract.lock (sha256 của file vừa promote + approvedAt/approvedBy/taskId).
 *
 * Usage: node tools/contract-sync/promote.mjs <taskId> <approvedBy> <moduleFile> [moduleFile ...]
 *   vd:  node tools/contract-sync/promote.mjs SAMPLE-001 human sample.v1.json
 */
import { createHash } from 'node:crypto';
import { existsSync, copyFileSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const STAGING_DIR = path.join(REPO_ROOT, 'contracts', 'openapi', '.staging');
const COMMITTED_DIR = path.join(REPO_ROOT, 'contracts', 'openapi');
const LOCK_FILE = path.join(REPO_ROOT, 'contracts', 'contract.lock');

const [taskId, approvedBy, ...moduleFiles] = process.argv.slice(2);

if (!taskId || !approvedBy || moduleFiles.length === 0) {
  console.error('Usage: node tools/contract-sync/promote.mjs <taskId> <approvedBy> <moduleFile> [moduleFile ...]');
  process.exit(1);
}

const lock = existsSync(LOCK_FILE) ? JSON.parse(readFileSync(LOCK_FILE, 'utf8')) : {};

for (const moduleFile of moduleFiles) {
  const stagingPath = path.join(STAGING_DIR, moduleFile);
  const committedPath = path.join(COMMITTED_DIR, moduleFile);

  if (!existsSync(stagingPath)) {
    throw new Error(`Không tìm thấy ${stagingPath}. Chạy export.mjs trước.`);
  }

  copyFileSync(stagingPath, committedPath);

  const sha256 = createHash('sha256').update(readFileSync(committedPath)).digest('hex');
  lock[moduleFile] = {
    sha256,
    approvedAt: new Date().toISOString(),
    approvedBy,
    taskId,
  };

  console.log(`[promote] ${moduleFile} -> contracts/openapi/${moduleFile} (sha256: ${sha256})`);
}

writeFileSync(LOCK_FILE, JSON.stringify(lock, null, 2) + '\n', 'utf8');
console.log(`[promote] -> contracts/contract.lock cập nhật`);
