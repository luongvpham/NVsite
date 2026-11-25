// node --test packages/api-sdk/scripts/lib/barrel.test.mjs
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { afterEach, beforeEach, test } from 'node:test';
import { buildBarrels } from './barrel.mjs';

let root;
const put = (rel, content = 'export {};\n') => {
  const file = path.join(root, rel);
  mkdirSync(path.dirname(file), { recursive: true });
  writeFileSync(file, content);
};

beforeEach(() => {
  root = mkdtempSync(path.join(tmpdir(), 'barrel-'));
});
afterEach(() => rmSync(root, { recursive: true, force: true }));

test('client, model, zod vào index; .msw vào mocks; bỏ model/index.ts của Orval', () => {
  put('shop/shop/shop.ts');
  put('shop/shop/shop.msw.ts');
  put('shop/model/index.ts');
  put('shop/model/shopDto.ts');
  put('shop/shop.zod.ts');

  const { index, mocks } = buildBarrels(root);

  assert.match(index, /export \* from '\.\/shop\/shop\/shop';/);
  assert.match(index, /export \* from '\.\/shop\/model\/shopDto';/);
  assert.match(index, /export \* from '\.\/shop\/shop\.zod';/);
  assert.doesNotMatch(index, /model\/index|\.msw/);
  assert.match(mocks, /export \* from '\.\/shop\/shop\/shop\.msw';/);
  assert.doesNotMatch(mocks, /shopDto|zod/);
});

test('zod rỗng (module chỉ có endpoint không body, vd. platform) không vào index — TS2306 "not a module"', () => {
  put('platform/platform/platform.ts');
  put('platform/platform.zod.ts', '/**\n * GENERATED — DO NOT EDIT (pnpm gen:api)\n */\n\n');

  const { index } = buildBarrels(root);

  assert.match(index, /export \* from '\.\/platform\/platform\/platform';/);
  assert.doesNotMatch(index, /platform\.zod/);
});

test('module/tag mới tự có mặt — không ai phải sửa barrel tay', () => {
  put('website/website/website.ts');
  put('website/site/site.ts');
  put('website/site/site.msw.ts');

  const { index, mocks } = buildBarrels(root);

  assert.match(index, /'\.\/website\/site\/site'/);
  assert.match(index, /'\.\/website\/website\/website'/);
  assert.match(mocks, /'\.\/website\/site\/site\.msw'/);
});

test('model trùng tên, giống hệt nhau → chỉ export bản đầu (tránh TS2308)', () => {
  const same = 'export interface ProblemDetails { title?: string | null }\n';
  put('identity/model/problemDetails.ts', same);
  put('shop/model/problemDetails.ts', same);

  const { index } = buildBarrels(root);

  assert.match(index, /'\.\/identity\/model\/problemDetails'/);
  assert.doesNotMatch(index, /'\.\/shop\/model\/problemDetails'/);
});

test('model trùng tên mà khác nội dung → throw, không chọn hộ', () => {
  put('identity/model/problemDetails.ts', 'export interface ProblemDetails { a: string }\n');
  put('shop/model/problemDetails.ts', 'export interface ProblemDetails { b: number }\n');

  assert.throws(() => buildBarrels(root), /problemDetails\.ts.*'identity'.*'shop'.*khác nhau/);
});

test('output ổn định (sort), không phụ thuộc thứ tự readdir', () => {
  put('b/b/b.ts');
  put('a/a/a.ts');
  const { index } = buildBarrels(root);
  assert.ok(index.indexOf("'./a/a/a'") < index.indexOf("'./b/b/b'"));
});

test('tag OpenAPI tên "model" trùng thư mục type → throw (review S1)', () => {
  put('shop/model/model.ts');
  put('shop/model/model.msw.ts');
  assert.throws(() => buildBarrels(root), /tag OpenAPI tên "model"/);
});
