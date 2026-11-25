import { test } from 'node:test';
import assert from 'node:assert/strict';
import { findProtectedTargets } from './pre-push-block-protected.mjs';

const Z = '0000000000000000000000000000000000000000';
const A = 'a'.repeat(40);

test('chặn push lên master', () => {
  assert.deepEqual(findProtectedTargets(`refs/heads/master ${A} refs/heads/master ${Z}\n`), ['master']);
});

test('chặn push nhánh local khác vào master (HEAD:master)', () => {
  assert.deepEqual(findProtectedTargets(`refs/heads/feature/X ${A} refs/heads/master ${A}\n`), ['master']);
});

test('chặn xoá master trên remote', () => {
  assert.deepEqual(findProtectedTargets(`(delete) ${Z} refs/heads/master ${A}\n`), ['master']);
});

test('chặn main, kể cả CRLF', () => {
  assert.deepEqual(findProtectedTargets(`refs/heads/main ${A} refs/heads/main ${Z}\r\n`), ['main']);
});

test('cho phép nhánh feature và tag', () => {
  const input = [
    `refs/heads/feature/JOBS-001 ${A} refs/heads/feature/JOBS-001 ${Z}`,
    `refs/heads/master-notes ${A} refs/heads/master-notes ${Z}`,
    `refs/tags/master ${A} refs/tags/master ${Z}`,
  ].join('\n');
  assert.deepEqual(findProtectedTargets(input), []);
});

test('stdin rỗng → không chặn', () => {
  assert.deepEqual(findProtectedTargets(''), []);
});
