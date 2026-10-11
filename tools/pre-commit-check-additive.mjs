#!/usr/bin/env node
/**
 * Pre-commit hook (07 §5, §11) — chỉ chạy check-additive khi staged changes có `*.manifest.ts`
 * (thêm/sửa/XOÁ) hoặc chính `registry.lock.json`.
 *
 * TOOLING-001: trước đây `--diff-filter=ACM` bỏ sót XOÁ manifest (xoá cả type — vi phạm nặng nhất), và
 * không bắt việc sửa tay lock để lách luật. Đây là lớp phòng thủ thứ hai bên cạnh CI: agent/dev có thể quên chạy
 * `pnpm gen:registry` trước khi commit, hook này chặn ngay tại local trước khi push lên.
 */
import { execFileSync, execSync } from 'node:child_process';

const stagedFiles = execFileSync('git', ['diff', '--cached', '--name-only', '--diff-filter=ACMDR'], {
  encoding: 'utf8',
})
  .split('\n')
  .filter(Boolean);

const manifestChanged = stagedFiles.some(
  (f) =>
    /packages\/builder-components\/registry\/.+\.manifest\.ts$/.test(f) ||
    f === 'packages/builder-components/registry.lock.json' ||
    // Luật/snapshot của check-additive đổi cũng phải chạy lại (review TOOLING-001).
    f.startsWith('packages/builder-components/meta/') ||
    f === 'packages/builder-components/scripts/lib/lock-snapshot.ts',
);

if (!manifestChanged) {
  process.exit(0);
}

console.log('[pre-commit] Phát hiện thay đổi manifest/registry.lock.json — chạy check-additive...');

try {
  execSync('pnpm --filter @vsite/builder-components run check-additive', { stdio: 'inherit' });
} catch {
  console.error('[pre-commit] check-additive FAIL — commit bị chặn. Xem lỗi ở trên.');
  process.exit(1);
}
