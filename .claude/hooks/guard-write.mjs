#!/usr/bin/env node
// PreToolUse hook — matcher: Write|Edit
// Blocks direct writes to protected contract/generated files.
// See CLAUDE.md "Contract — ba loại, đều phải người duyệt" and
// DesignIdeal/ai-agent-development-workflow.md §16.

import path from 'node:path';

const PROTECTED = [
  // Committed OpenAPI contracts — only the promote script may write these.
  { re: /(^|\/)contracts\/openapi\/(?!\.staging\/).+\.json$/, reason: 'contracts/openapi/*.json (ngoài .staging/) chỉ được ghi bởi lệnh promote, sau khi Gate 1 duyệt.' },
  { re: /(^|\/)contracts\/contract\.lock$/, reason: 'contracts/contract.lock chỉ được ghi bởi lệnh promote, cùng lúc với contract đã duyệt.' },
  { re: /(^|\/)config\/reserved-routes\.json$/, reason: 'config/reserved-routes.json là nguồn sự thật duy nhất (Quyết định #24) — sửa cần người duyệt, không sửa trực tiếp trong task thường.' },
  { re: /(^|\/)packages\/api-sdk\/src\/generated\/.+/, reason: 'packages/api-sdk/src/generated/** là output của Orval — không sửa tay, chạy pnpm gen:api thay vào đó. (package.json/orval.config.ts/scripts của api-sdk KHÔNG bị chặn — đó là setup viết tay một lần.)' },
  { re: /(^|\/)packages\/builder-components\/registry\/generated\/.+/, reason: 'registry/generated/** là generated (pnpm gen:registry) — không sửa tay.' },
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

const filePath = input?.tool_input?.file_path;
if (!filePath) process.exit(0);

const normalized = String(filePath).replace(/\\/g, '/');

for (const rule of PROTECTED) {
  if (rule.re.test(normalized)) {
    process.stderr.write(
      `BLOCKED: ${normalized}\n${rule.reason}\n`,
    );
    process.exit(2);
  }
}

process.exit(0);
