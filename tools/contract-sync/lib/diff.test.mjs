// Chạy: node --test tools/contract-sync/lib/diff.test.mjs  (CI: job "Contract drift check")
// TOOLING-001 — mỗi test là một điểm mù cũ của classifier: trước đây tất cả đều ra UNCHANGED.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { classifyDiff, documentsEqual, firstDifference } from './diff.mjs';
import { normalizeDocument } from './normalize.mjs';

const base = () => ({
  openapi: '3.0.1',
  paths: {
    '/api/shops': {
      get: {
        operationId: 'listShops',
        parameters: [{ in: 'query', name: 'page', required: false, schema: { type: 'integer' } }],
        responses: {
          200: { content: { 'application/json': { schema: { type: 'array', items: { $ref: '#/components/schemas/ShopSummaryDto' } } } } },
          403: { content: { 'application/problem+json': { schema: { $ref: '#/components/schemas/ProblemDetails' } } } },
        },
      },
    },
  },
  components: {
    schemas: {
      ShopSummaryDto: { type: 'object', properties: { id: { type: 'string' }, logo: { $ref: '#/components/schemas/LogoDto' } } },
      LogoDto: { type: 'object', properties: { url: { type: 'string' } } },
      ProblemDetails: { type: 'object', properties: { title: { type: 'string' } } },
      Unused: { type: 'object', properties: { x: { type: 'string' } } },
    },
  },
});

const classify = (mutate) => {
  const oldDoc = normalizeDocument(base());
  const next = base();
  mutate(next);
  return classifyDiff(oldDoc, normalizeDocument(next));
};

test('không đổi gì → UNCHANGED', () => {
  const r = classify(() => {});
  assert.equal(r.unchanged.length, 1);
  assert.equal(r.breaking.length + r.additive.length, 0);
});

test('response dạng MẢNG: thêm field optional vào schema của items → ADDITIVE', () => {
  const r = classify((d) => { d.components.schemas.ShopSummaryDto.properties.name = { type: 'string' }; });
  assert.equal(r.additive.length, 1);
  assert.match(r.additive[0].changes[0].detail, /ShopSummaryDto\.name/);
});

test('component lồng (ShopSummaryDto → LogoDto): xoá field ở LogoDto → BREAKING', () => {
  const r = classify((d) => { delete d.components.schemas.LogoDto.properties.url; });
  assert.equal(r.breaking.length, 1);
  assert.match(r.breaking[0].changes.map((c) => c.detail).join('|'), /LogoDto\.url bị xoá/);
});

test('parameters: thêm query param required → BREAKING', () => {
  const r = classify((d) => {
    d.paths['/api/shops'].get.parameters.push({ in: 'query', name: 'kind', required: true, schema: { type: 'string' } });
  });
  assert.equal(r.breaking.length, 1);
  assert.match(r.breaking[0].changes[0].detail, /param query:kind/);
});

test('operationId đổi (= đổi tên hook FE, #91) → BREAKING', () => {
  const r = classify((d) => { d.paths['/api/shops'].get.operationId = 'getShops'; });
  assert.equal(r.breaking.length, 1);
  assert.match(r.breaking[0].changes[0].detail, /operationId listShops → getShops/);
});

test('enum của schema (ngoài properties) đổi → BREAKING', () => {
  const r = classify((d) => { d.components.schemas.LogoDto.enum = ['a']; });
  assert.equal(r.breaking.length, 1);
});

test('lưới an toàn: khác ở chỗ classifier không gọi tên (security) → vẫn không UNCHANGED', () => {
  const r = classify((d) => { d.paths['/api/shops'].get.security = [{ Bearer: [] }]; });
  assert.equal(r.unchanged.length, 0);
  assert.match(r.breaking[0].changes[0].detail, /ngoài phạm vi phân loại/);
});

test('component không operation nào dùng đổi → classifier UNCHANGED nhưng documentsEqual = false (drift check vẫn fail)', () => {
  const oldDoc = normalizeDocument(base());
  const next = base();
  next.components.schemas.Unused.properties.y = { type: 'string' };
  const newDoc = normalizeDocument(next);
  assert.equal(classifyDiff(oldDoc, newDoc).unchanged.length, 1);
  assert.equal(documentsEqual(oldDoc, newDoc), false);
});

// ---- Sau review: thay đổi additive được gọi tên KHÔNG được che thay đổi phá vỡ (nhãn ADDITIVE → tự promote, #89) ----

const label = (r) => (r.breaking.length ? 'BREAKING' : r.additive.length ? 'ADDITIVE' : 'UNCHANGED');
const addOptionalParam = (d) => d.paths['/api/shops'].get.parameters.push({ in: 'query', name: 'q', required: false, schema: { type: 'string' } });

test('thêm param optional + thêm security → BREAKING (lưới an toàn vẫn chạy)', () => {
  assert.equal(label(classify((d) => { addOptionalParam(d); d.paths['/api/shops'].get.security = [{ Bearer: [] }]; })), 'BREAKING');
});

test('thêm response 201 + response 200 mất content → BREAKING', () => {
  assert.equal(label(classify((d) => {
    const r = d.paths['/api/shops'].get.responses;
    r[201] = { description: 'x' };
    delete r[200].content;
  })), 'BREAKING');
});

test('thêm field optional + additionalProperties:false → BREAKING', () => {
  assert.equal(label(classify((d) => {
    const s = d.components.schemas.ShopSummaryDto;
    s.properties.name = { type: 'string' };
    s.additionalProperties = false;
  })), 'BREAKING');
});

test('operationId ∅ → X (đổi tên hook từ tên tự sinh) → BREAKING', () => {
  assert.equal(label(classify((d) => { delete d.paths['/api/shops'].get.operationId; })), 'BREAKING');
});

test('chỉ thêm param optional → ADDITIVE (không báo động giả)', () => {
  assert.equal(label(classify(addOptionalParam)), 'ADDITIVE');
});

test('documentsEqual không phụ thuộc thứ tự `required`; firstDifference chỉ ra chỗ lệch', () => {
  const a = normalizeDocument(base());
  const bDoc = base();
  bDoc.components.schemas.ShopSummaryDto.required = ['id', 'logo'];
  const aDoc = base();
  aDoc.components.schemas.ShopSummaryDto.required = ['logo', 'id'];
  assert.equal(documentsEqual(normalizeDocument(aDoc), normalizeDocument(bDoc)), true);
  bDoc.components.schemas.Unused.properties.z = { type: 'string' };
  assert.equal(firstDifference(a, normalizeDocument(bDoc)).startsWith('$.components.schemas'), true);
});
