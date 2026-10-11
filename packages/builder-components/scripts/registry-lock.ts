#!/usr/bin/env -S tsx
/**
 * `pnpm registry:lock` — cập nhật `registry.lock.json` cho khớp registry hiện tại (TOOLING-001).
 *
 * Lệnh TƯỜNG MINH, không chạy ngầm trong gen-registry: lock đổi phải hiện ra trong PR để người duyệt
 * thấy. Chỉ ghi khi KHÔNG có vi phạm additive-only — lock không bao giờ bị "cập nhật để hợp thức
 * hoá" một thay đổi phá vỡ (xoá type/variant/prop, thu hẹp ràng buộc). Cảnh báo (đổi preset…) thì
 * vẫn ghi, vì đó là thay đổi người viết manifest đã chủ động chấp nhận.
 */
import { writeFileSync } from 'node:fs';
import { runCheckAdditive } from './check-additive';
import { loadManifests } from './lib/load-manifests';
import { buildLockSnapshot } from './lib/lock-snapshot';
import { LOCK_FILE, PKG_ROOT, REGISTRY_DIR } from './lib/paths';

async function main() {
  const manifests = await loadManifests(PKG_ROOT, REGISTRY_DIR);
  const { ok, violations, warnings, unlocked } = await runCheckAdditive(manifests);

  for (const warning of warnings) console.warn(`[registry-lock] CẢNH BÁO: ${warning}`);
  if (!ok) {
    console.error('[registry-lock] TỪ CHỐI cập nhật lock — đang vi phạm additive-only (#43, #62):');
    for (const v of violations) console.error(`  - ${v}`);
    console.error('\nSửa manifest (tạo type/variant MỚI thay vì sửa cái cũ, §5), không sửa lock để lách.');
    process.exit(1);
  }

  writeFileSync(LOCK_FILE, JSON.stringify(buildLockSnapshot(manifests), null, 2) + '\n');
  console.log(
    unlocked.length > 0
      ? `[registry-lock] Đã cập nhật registry.lock.json — thêm bảo vệ cho ${unlocked.length} mục:\n${unlocked.map((u) => `  + ${u}`).join('\n')}`
      : '[registry-lock] registry.lock.json đã khớp, ghi lại (không đổi nội dung).',
  );
}

main().catch((error: unknown) => {
  console.error(error);
  process.exit(1);
});
