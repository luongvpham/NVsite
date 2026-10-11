#!/usr/bin/env -S tsx
/**
 * So manifest hiện tại với `registry.lock.json` (committed) — luật FAIL ở §5 (#43, #62).
 * Chạy độc lập (CI, pre-commit) hoặc được gọi lại từ gen-registry.ts / registry-lock.ts.
 *
 * TOOLING-001 — chạy độc lập thì FAIL thêm hai trường hợp trước đây lọt:
 *   - không có `registry.lock.json` (xoá lock = xoá mọi bảo vệ, trước đây chỉ là cảnh báo);
 *   - lock chưa phủ hết registry hiện tại (type/variant/prop mới, field snapshot mới) — trước đây lock
 *     chỉ ghi lúc chưa tồn tại nên mọi thứ thêm sau không bao giờ được bảo vệ.
 * Cả hai sửa bằng `pnpm registry:lock`.
 */
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import type { ComponentManifest } from '../meta/manifest-schema';
import { loadManifests } from './lib/load-manifests';
import { buildLockSnapshot, checkAdditive, findUnlocked, type LockSnapshot } from './lib/lock-snapshot';
import { LOCK_FILE, PKG_ROOT, REGISTRY_DIR } from './lib/paths';

export interface RunCheckAdditiveResult {
  ok: boolean;
  lockExists: boolean;
  violations: string[];
  warnings: string[];
  /** Phần của registry hiện tại chưa được lock bảo vệ — khác rỗng thì cần `pnpm registry:lock`. */
  unlocked: string[];
}

export async function runCheckAdditive(manifests?: ComponentManifest[]): Promise<RunCheckAdditiveResult> {
  const current = buildLockSnapshot(manifests ?? (await loadManifests(PKG_ROOT, REGISTRY_DIR)));

  if (!existsSync(LOCK_FILE)) {
    return { ok: true, lockExists: false, violations: [], warnings: [], unlocked: Object.keys(current).map((t) => `${t} (type)`) };
  }

  const previous = JSON.parse(readFileSync(LOCK_FILE, 'utf8')) as LockSnapshot;
  const { violations, warnings } = checkAdditive(previous, current);
  return { ok: violations.length === 0, lockExists: true, violations, warnings, unlocked: findUnlocked(previous, current) };
}

async function main() {
  const { ok, lockExists, violations, warnings, unlocked } = await runCheckAdditive();

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

  if (!lockExists) {
    console.error('[check-additive] FAIL — không có registry.lock.json. Chạy `pnpm registry:lock` để tạo, rồi commit file đó.');
    process.exit(1);
  }

  if (unlocked.length > 0) {
    console.error('[check-additive] FAIL — registry.lock.json chưa phủ registry hiện tại (phần thêm sau sẽ không được bảo vệ):');
    for (const item of unlocked) console.error(`  - ${item}`);
    console.error('\nChạy `pnpm registry:lock` rồi commit registry.lock.json cùng manifest.');
    process.exit(1);
  }

  console.log('[check-additive] OK — không vi phạm additive-only, lock phủ đủ registry.');
}

// Chỉ tự chạy khi được gọi trực tiếp (không phải khi file khác import runCheckAdditive).
if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url))) {
  main().catch((error: unknown) => {
    console.error(error);
    process.exit(1);
  });
}
