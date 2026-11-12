#!/usr/bin/env node
// PreToolUse hook — matcher: Bash|PowerShell
// Blocks shell redirection/copy/move that would bypass guard-write.mjs
// (e.g. `echo x > contracts/contract.lock`, `cp foo packages/api-sdk/bar`,
// `Set-Content contracts/contract.lock ...`).
// Test: node --test .claude/hooks/guard-bash.test.mjs

const PROTECTED_PATTERNS = [
  /contracts\/openapi\/(?!\.staging\/)[^\s"']*\.json/i,
  /contracts\/contract\.lock/i,
  /config\/reserved-routes\.json/i,
  /packages\/api-sdk\/src\/generated\//i,
  /packages\/builder-components\/generated\//i,
];

// Lệnh ghi/chép/chuyển: có một trong các lệnh này + đường dẫn được bảo vệ ở bất kỳ đâu → chặn.
// (Thô có chủ đích: `cp <protected> /tmp/x` cũng bị chặn — dùng tool Read thay vào đó.)
const WRITE_COMMANDS = [
  // POSIX
  /\btee\b/,
  /\bcp\b/,
  /\bmv\b/,
  /\bsed\b[^|;&]*\s-[a-z]*i/,
  // PowerShell cmdlet + alias
  /\b(Set|Add|Clear)-Content\b/i,
  /\bOut-File\b/i,
  /\bTee-Object\b/i,
  /\b(Copy|Move|Rename|New)-Item\b/i,
  /\b(copy|move|cpi|mi|ni|ren)\b/i,
  /\[(System\.)?IO\.File\]::(Write|Append|Copy|Move|Create)/i,
];

// Redirect: chỉ chặn khi ĐÍCH của `>` / `>>` là đường dẫn được bảo vệ. `2>/dev/null`, `2>&1`,
// `> /tmp/x` trên lệnh chỉ đọc (grep, git diff…) không còn bị chặn nhầm.
const REDIRECT_TARGET = /\d?>>?\s*["']?([^\s"'|;&<>]+)/g;

const isProtected = (s) => PROTECTED_PATTERNS.some((re) => re.test(s));

export function shouldBlock(rawCommand) {
  const command = String(rawCommand).replace(/\\/g, '/');
  if (!isProtected(command)) return false;
  if (WRITE_COMMANDS.some((re) => re.test(command))) return true;
  for (const m of command.matchAll(REDIRECT_TARGET)) {
    if (isProtected(m[1])) return true;
  }
  return false;
}

function readStdin() {
  return new Promise((resolve) => {
    let data = '';
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (chunk) => (data += chunk));
    process.stdin.on('end', () => resolve(data));
  });
}

const IS_MAIN = !!process.argv[1] && process.argv[1].replace(/\\/g, '/').endsWith('guard-bash.mjs');
if (IS_MAIN) {
  const raw = await readStdin();
  let input;
  try {
    input = JSON.parse(raw || '{}');
  } catch {
    process.exit(0);
  }

  const command = input?.tool_input?.command;
  if (!command || typeof command !== 'string') process.exit(0);

  if (shouldBlock(command)) {
    process.stderr.write(
      `BLOCKED: lệnh shell có vẻ ghi vào đường dẫn được bảo vệ (contract, reserved-routes, hoặc generated code).\ncommand: ${command}\nDùng lệnh promote/gen chính thức thay vì redirect/cp/mv/Set-Content trực tiếp.\n`,
    );
    process.exit(2);
  }

  process.exit(0);
}
