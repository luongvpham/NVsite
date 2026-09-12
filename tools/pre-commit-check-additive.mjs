#!/usr/bin/env node
/**
 * Pre-commit hook (07 §5, §11) — chỉ chạy check-additive khi có file *.manifest.ts nằm trong
 * staged changes. Đây là lớp phòng thủ thứ hai bên cạnh CI: agent/dev có thể quên chạy
 * `pnpm gen:registry` trước khi commit, hook này chặn ngay tại local trước khi push lên.
 */
import { execFileSync, execSync } from 'node:child_process';

const stagedFiles = execFileSync('git', ['diff', '--cached', '--name-only', '--diff-filter=ACM'], {
  encoding: 'utf8',
})
  .split('\n')
  .filter(Boolean);

const manifestChanged = stagedFiles.some((f) => /packages\/builder-components\/registry\/.+\.manifest\.ts$/.test(f));

if (!manifestChanged) {
  process.exit(0);
}

console.log('[pre-commit] Phát hiện thay đổi *.manifest.ts — chạy check-additive...');

try {
  execSync('pnpm --filter @vsite/builder-components run check-additive', { stdio: 'inherit' });
} catch {
  console.error('[pre-commit] check-additive FAIL — commit bị chặn. Xem lỗi ở trên.');
  process.exit(1);
}
