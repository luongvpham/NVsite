// Sinh src/generated bằng Orval, XOÁ file mồ côi (REFACTOR-API-001), rồi sinh barrel (TOOLING-004).
//
// Orval không tự xoá file cũ: đổi tên operation/schema (vd. thêm operationId) để lại file model mồ
// côi — và thư mục này được commit, nên file mồ côi sẽ nằm lại trong repo.
//
// Dọn SAU khi Orval chạy xong, chỉ xoá file mà lần sinh này KHÔNG ghi lại (mtime cũ hơn lúc bắt đầu).
// KHÔNG xoá trước rồi mới sinh: turbo có thể chạy task khác của package (vd. test) song song với
// gen:api — xoá trước tạo ra khoảng thư mục rỗng làm task song song đó fail. Orval lỗi → không xoá gì.
import { spawnSync } from 'node:child_process';
import { readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { BARREL_FILES, buildBarrels } from './lib/barrel.mjs';

const root = fileURLToPath(new URL('..', import.meta.url));
const generated = path.join(root, 'src', 'generated');
// Trừ 2s: mtime trên một số filesystem làm tròn tới giây.
const startedAt = Date.now() - 2000;

const orval = spawnSync('orval --config orval.config.ts', { cwd: root, stdio: 'inherit', shell: true });
if (orval.status !== 0) {
  console.error('[gen] Orval lỗi — không dọn file cũ.');
  process.exit(orval.status ?? 1);
}

const walk = (dir) =>
  readdirSync(dir, { withFileTypes: true }).flatMap((e) =>
    e.isDirectory() ? walk(path.join(dir, e.name)) : [path.join(dir, e.name)],
  );

// Barrel do script này ghi (bên dưới), Orval không ghi — đừng coi là mồ côi.
const barrels = new Set(BARREL_FILES.map((f) => path.join(generated, f)));
const stale = walk(generated).filter((file) => !barrels.has(file) && statSync(file).mtimeMs < startedAt);
for (const file of stale) {
  rmSync(file);
  console.log('[gen] xoá file mồ côi', path.relative(root, file));
}

// Thư mục rỗng sau khi dọn.
for (const dir of readdirSync(generated, { withFileTypes: true, recursive: true })
  .filter((e) => e.isDirectory())
  .map((e) => path.join(e.parentPath ?? e.path, e.name))
  .sort((a, b) => b.length - a.length)) {
  if (readdirSync(dir).length === 0) rmSync(dir, { recursive: true });
}

// Barrel suy từ output SAU khi dọn (N5, TOOLING-004) — src/index.ts và src/mocks.ts chỉ re-export.
const { index, mocks } = buildBarrels(generated);
writeFileSync(path.join(generated, 'index.ts'), index);
writeFileSync(path.join(generated, 'mocks.ts'), mocks);
console.log('[gen] barrel src/generated/index.ts + mocks.ts');
