// node --test .claude/hooks/guard-bash.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { shouldBlock } from './guard-bash.mjs';

const BLOCK = [
  // Bash
  'echo x > contracts/contract.lock',
  'echo x >> contracts/openapi/listing.v1.json',
  'cp /tmp/a.json contracts/openapi/listing.v1.json',
  'mv tmp.ts packages/api-sdk/src/generated/model.ts',
  'cat a | tee config/reserved-routes.json',
  "sed -i 's/a/b/' packages/builder-components/generated/index.ts",
  // PowerShell
  'Set-Content contracts/contract.lock "x"',
  'Set-Content -Path contracts\\contract.lock -Value "x"',
  '"x" | Out-File contracts\\openapi\\listing.v1.json -Encoding utf8',
  'Add-Content config\\reserved-routes.json "x"',
  'Copy-Item C:\\tmp\\a.ts packages\\api-sdk\\src\\generated\\a.ts',
  'Move-Item a.ts packages/builder-components/generated/a.ts',
  '"x" > Contracts\\Contract.lock',
  "[IO.File]::WriteAllText('contracts/contract.lock', 'x')",
];

const ALLOW = [
  'grep -r "foo" packages/api-sdk/src/generated/ 2>/dev/null',
  'git diff contracts/openapi/listing.v1.json > /tmp/diff.txt',
  'Get-Content contracts\\contract.lock 2>$null',
  'git diff -- contracts/openapi/listing.v1.json 2>&1',
  'node tools/contract-sync/promote.mjs WEBSITE-003 luong website.v1.json',
  'pnpm gen:api',
  'echo x > contracts/openapi/.staging/listing.v1.json',
  'cp a.json contracts/openapi/.staging/listing.v1.json',
  'echo x > notes.txt',
  'Set-Content notes.txt "x"',
];

for (const c of BLOCK) test(`chặn: ${c}`, () => assert.equal(shouldBlock(c), true));
for (const c of ALLOW) test(`cho qua: ${c}`, () => assert.equal(shouldBlock(c), false));

test('chạy như hook: exit 2 khi chặn, 0 khi cho qua', () => {
  const hook = path.join(path.dirname(fileURLToPath(import.meta.url)), 'guard-bash.mjs');
  const run = (command) =>
    spawnSync(process.execPath, [hook], { input: JSON.stringify({ tool_input: { command } }), encoding: 'utf8' });
  const blocked = run('Set-Content contracts/contract.lock "x"');
  assert.equal(blocked.status, 2);
  assert.match(blocked.stderr, /BLOCKED/);
  assert.equal(run('pnpm gen:api').status, 0);
});
