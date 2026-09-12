#!/usr/bin/env -S tsx
/**
 * So manifest hiện tại với `registry.lock.json` (committed) — 7 luật FAIL ở §5.
 * Chạy độc lập (CI, pre-commit) hoặc được gọi lại từ gen-registry.ts.
 *
 * Không có registry.lock.json → không có gì để so, coi là lần đầu (không fail).
 */
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadManifests } from './lib/load-manifests';
import { buildLockSnapshot, checkAdditive, type LockSnapshot } from './lib/lock-snapshot';

const PKG_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const LOCK_FILE = path.join(PKG_ROOT, 'registry.lock.json');

export async function runCheckAdditive(): Promise<{ ok: boolean; violations: string[]; warnings: string[] }> {
  if (!existsSync(LOCK_FILE)) {
    return { ok: true, violations: [], warnings: ['registry.lock.json chưa tồn tại — lần đầu chạy, không có gì để so.'] };
  }

  const previous = JSON.parse(readFileSync(LOCK_FILE, 'utf8')) as LockSnapshot;
  const manifests = await loadManifests(PKG_ROOT);
  const current = buildLockSnapshot(manifests);

  const { violations, warnings } = checkAdditive(previous, current);
  return { ok: violations.length === 0, violations, warnings };
}

async function main() {
  const { ok, violations, warnings } = await runCheckAdditive();

  for (const warning of warnings) {
    console.warn(`[check-additive] CẢNH BÁO: ${warning}`);
  }

  if (!ok) {
    console.error('[check-additive] FAIL — vi phạm additive-only (#43, #62):');
    for (const violation of violations) {
      console.error(`  - ${violation}`);
    }
    console.error('\nCần đổi phá vỡ thật? Tạo type/variant MỚI, không sửa cái cũ (§5).');
    process.exit(1);
  }

  console.log('[check-additive] OK — không vi phạm additive-only.');
}

// Chỉ tự chạy khi được gọi trực tiếp (không phải khi gen-registry.ts import runCheckAdditive).
if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url))) {
  main().catch((error: unknown) => {
    console.error(error);
    process.exit(1);
  });
}
