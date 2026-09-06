#!/usr/bin/env node
// PreToolUse hook — matcher: Bash
// Blocks shell redirection/copy/move that would bypass guard-write.mjs
// (e.g. `echo x > contracts/contract.lock`, `cp foo packages/api-sdk/bar`).

const PROTECTED_PATTERNS = [
  /contracts\/openapi\/(?!\.staging\/)[^\s"']*\.json/,
  /contracts\/contract\.lock/,
  /config\/reserved-routes\.json/,
  /packages\/api-sdk\/src\/generated\//,
  /packages\/builder-components\/registry\/generated\//,
];

const WRITE_OPS = [
  />>?/, // > and >>
  /\btee\b/,
  /\bcp\b/,
  /\bmv\b/,
];

function readStdin() {
  return new Promise((resolve) => {
    let data = '';
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (chunk) => (data += chunk));
    process.stdin.on('end', () => resolve(data));
  });
}

const raw = await readStdin();
let input;
try {
  input = JSON.parse(raw || '{}');
} catch {
  process.exit(0);
}

const command = input?.tool_input?.command;
if (!command || typeof command !== 'string') process.exit(0);

const hasWriteOp = WRITE_OPS.some((re) => re.test(command));
if (!hasWriteOp) process.exit(0);

const hitsProtected = PROTECTED_PATTERNS.some((re) => re.test(command));
if (hitsProtected) {
  process.stderr.write(
    `BLOCKED: lệnh Bash có vẻ ghi vào đường dẫn được bảo vệ (contract, reserved-routes, hoặc generated code).\ncommand: ${command}\nDùng lệnh promote/gen chính thức thay vì redirect/cp/mv trực tiếp.\n`,
  );
  process.exit(2);
}

process.exit(0);
