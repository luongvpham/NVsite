#!/usr/bin/env node
/**
 * Pre-push hook — chặn push thẳng lên nhánh được bảo vệ (master/main).
 *
 * Mọi thay đổi vào master phải đi qua Pull Request. Hook này là lớp phòng thủ local;
 * lớp thật sự là branch protection / ruleset trên GitHub (hook local bỏ qua được bằng `--no-verify`).
 *
 * Git gửi vào stdin mỗi dòng: `<local ref> <local sha> <remote ref> <remote sha>`.
 * Test: node --test tools/pre-push-block-protected.test.mjs
 */

export const PROTECTED_BRANCHES = ['master', 'main'];

/** Trả về danh sách ref đích bị bảo vệ trong input stdin của pre-push. */
export function findProtectedTargets(stdin) {
  const hits = new Set();
  for (const line of String(stdin).split(/\r?\n/)) {
    const parts = line.trim().split(/\s+/);
    if (parts.length < 4) continue;
    const remoteRef = parts[2];
    const m = /^refs\/heads\/(.+)$/.exec(remoteRef);
    if (m && PROTECTED_BRANCHES.includes(m[1])) hits.add(m[1]);
  }
  return [...hits];
}

function readStdin() {
  return new Promise((resolve) => {
    let data = '';
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (chunk) => (data += chunk));
    process.stdin.on('end', () => resolve(data));
  });
}

const IS_MAIN =
  !!process.argv[1] && process.argv[1].replace(/\\/g, '/').endsWith('pre-push-block-protected.mjs');
if (IS_MAIN) {
  const targets = findProtectedTargets(await readStdin());
  if (targets.length > 0) {
    process.stderr.write(
      `BLOCKED: không push thẳng lên nhánh được bảo vệ: ${targets.join(', ')}.\n` +
        `Tạo nhánh feature/{ID}, push nhánh đó rồi mở Pull Request.\n` +
        `  git switch -c feature/<ID>\n  git push -u origin feature/<ID>\n`,
    );
    process.exit(1);
  }
  process.exit(0);
}
