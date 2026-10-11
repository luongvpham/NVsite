/**
 * Diff hai OpenAPI document đã normalize, phân loại theo từng operation
 * (method + path) — NEW_ENDPOINT / ADDITIVE / BREAKING / REMOVED / UNCHANGED.
 *
 * TOOLING-001 — trước đây chỉ so `$ref` gắn TRỰC TIẾP ở request/response, nên mù với:
 *   - response dạng mảng (`{ type: array, items: { $ref } }`, vd. GET /shops) và mọi schema lồng;
 *   - component trỏ tiếp sang component khác (`ShopDto.logo → MediaAssetDto`);
 *   - `parameters` (query/path), `operationId` (= tên hàm/hook FE, #91).
 * Giờ: đi ĐỆ QUY mọi `$ref` mà operation chạm tới (bao đóng qua components), so thêm parameters,
 * operationId, schema inline. Và có LƯỚI AN TOÀN: JSON của operation (hoặc bao đóng component của nó)
 * khác nhau mà không có thay đổi nào được gọi tên → vẫn ghi một thay đổi BREAKING "khác ngoài phạm vi
 * phân loại" — không bao giờ ra UNCHANGED khi thực tế có lệch.
 */

const stable = (value) => JSON.stringify(value);

function flattenOperations(doc) {
  const ops = new Map();
  if (!doc || !doc.paths) return ops;

  for (const [path, methods] of Object.entries(doc.paths)) {
    for (const [method, operation] of Object.entries(methods)) {
      ops.set(`${method.toUpperCase()} ${path}`, operation);
    }
  }
  return ops;
}

function resolveRef(doc, ref) {
  if (!ref || !ref.startsWith('#/')) return undefined;
  const parts = ref.slice(2).split('/');
  let node = doc;
  for (const part of parts) {
    node = node?.[part];
  }
  return node;
}

/** Mọi `$ref` xuất hiện ở bất kỳ độ sâu nào trong `node` (items, allOf, oneOf, properties, …). */
function collectRefs(node, out = new Set()) {
  if (Array.isArray(node)) {
    for (const item of node) collectRefs(item, out);
  } else if (node !== null && typeof node === 'object') {
    for (const [key, value] of Object.entries(node)) {
      if (key === '$ref' && typeof value === 'string') out.add(value);
      else collectRefs(value, out);
    }
  }
  return out;
}

/**
 * Bao đóng `$ref` của một operation trong MỘT document: ref trực tiếp + ref của component được trỏ
 * tới, đệ quy. Không hardcode content-type: response 4xx/5xx dùng 'application/problem+json'
 * (ProblemDetails, nơi error_code sống) cũng được đi qua.
 */
function refClosure(doc, operation) {
  const seen = new Set();
  const queue = [...collectRefs(operation)];
  while (queue.length > 0) {
    const ref = queue.pop();
    if (seen.has(ref)) continue;
    seen.add(ref);
    for (const next of collectRefs(resolveRef(doc, ref))) {
      if (!seen.has(next)) queue.push(next);
    }
  }
  return seen;
}

function diffSchema(ref, oldSchema, newSchema) {
  const changes = [];
  const name = ref.split('/').pop();

  if (!oldSchema && newSchema) return [{ kind: 'ADDITIVE', detail: `+ schema '${name}'` }];
  if (oldSchema && !newSchema) return [{ kind: 'BREAKING', detail: `- schema '${name}' bị xoá` }];
  if (!oldSchema || !newSchema) return changes;

  const oldProps = oldSchema.properties ?? {};
  const newProps = newSchema.properties ?? {};
  const oldRequired = new Set(oldSchema.required ?? []);
  const newRequired = new Set(newSchema.required ?? []);

  for (const key of Object.keys(newProps)) {
    if (!(key in oldProps)) {
      changes.push({
        kind: newRequired.has(key) ? 'BREAKING' : 'ADDITIVE',
        detail: `+ ${name}.${key}${newRequired.has(key) ? ' (required)' : ' (optional)'}`,
      });
      continue;
    }

    if (stable(oldProps[key]) !== stable(newProps[key])) {
      changes.push({ kind: 'BREAKING', detail: `~ ${name}.${key} đổi shape` });
    }

    if (oldRequired.has(key) !== newRequired.has(key)) {
      changes.push({
        kind: newRequired.has(key) ? 'BREAKING' : 'ADDITIVE',
        detail: `~ ${name}.${key} đổi nullable/required`,
      });
    }
  }

  for (const key of Object.keys(oldProps)) {
    if (!(key in newProps)) {
      changes.push({ kind: 'BREAKING', detail: `- ${name}.${key} bị xoá` });
    }
  }

  // Phần ngoài `properties`/`required` (enum, type, items, allOf, nullable, additionalProperties…) —
  // đổi là BREAKING. So LUÔN, không chỉ khi properties không đổi (thêm field optional + siết
  // additionalProperties cùng lúc từng bị gắn ADDITIVE).
  const rest = ({ properties: _p, required: _r, description: _d, ...others }) => others;
  if (stable(rest(oldSchema)) !== stable(rest(newSchema))) {
    changes.push({ kind: 'BREAKING', detail: `~ schema '${name}' đổi (enum/type/items/allOf…)` });
  }

  return changes;
}

function paramKey(p) {
  return `${p.in}:${p.name}`;
}

function diffParameters(oldOp, newOp) {
  const changes = [];
  const oldParams = new Map((oldOp.parameters ?? []).map((p) => [paramKey(p), p]));
  const newParams = new Map((newOp.parameters ?? []).map((p) => [paramKey(p), p]));

  for (const [key, p] of newParams) {
    if (!oldParams.has(key)) {
      changes.push({ kind: p.required ? 'BREAKING' : 'ADDITIVE', detail: `+ param ${key}${p.required ? ' (required)' : ''}` });
    } else if (stable(oldParams.get(key)) !== stable(p)) {
      changes.push({ kind: 'BREAKING', detail: `~ param ${key} đổi` });
    }
  }
  for (const key of oldParams.keys()) {
    if (!newParams.has(key)) changes.push({ kind: 'BREAKING', detail: `- param ${key} bị xoá` });
  }
  return changes;
}

/** Schema inline của request/response (bỏ qua nội dung component — đã so ở diffSchema). */
function inlineSchemas(op) {
  const out = {};
  for (const [type, content] of Object.entries(op.requestBody?.content ?? {})) {
    out[`request ${type}`] = content?.schema;
  }
  for (const [code, response] of Object.entries(op.responses ?? {})) {
    for (const [type, content] of Object.entries(response?.content ?? {})) {
      out[`response ${code} ${type}`] = content?.schema;
    }
  }
  return out;
}

function diffOperation(oldDoc, newDoc, oldOp, newOp) {
  const changes = [];

  if ((oldOp.operationId ?? null) !== (newOp.operationId ?? null)) {
    // operationId = tên hàm/hook Orval sinh ra (#91): đổi là FE phải đổi tên.
    // Kể cả ∅ → X: Orval đổi từ tên tự sinh theo path sang X — FE phải đổi tên → BREAKING.
    changes.push({ kind: 'BREAKING', detail: `~ operationId ${oldOp.operationId ?? '∅'} → ${newOp.operationId ?? '∅'}` });
  }

  changes.push(...diffParameters(oldOp, newOp));

  const oldInline = inlineSchemas(oldOp);
  const newInline = inlineSchemas(newOp);
  for (const key of new Set([...Object.keys(oldInline), ...Object.keys(newInline)])) {
    if (!(key in newInline)) {
      changes.push({ kind: 'BREAKING', detail: `- ${key} bị bỏ` });
    } else if (!(key in oldInline)) {
      changes.push({ kind: 'ADDITIVE', detail: `+ ${key}` });
    } else if (stable(oldInline[key]) !== stable(newInline[key])) {
      changes.push({ kind: 'BREAKING', detail: `~ ${key} đổi schema` });
    }
  }

  if (!oldOp.requestBody?.required && newOp.requestBody?.required) {
    changes.push({ kind: 'BREAKING', detail: '~ requestBody thành bắt buộc' });
  }

  const refs = new Set([...refClosure(oldDoc, oldOp), ...refClosure(newDoc, newOp)]);
  for (const ref of [...refs].sort()) {
    changes.push(...diffSchema(ref, resolveRef(oldDoc, ref), resolveRef(newDoc, ref)));
  }

  const oldCodes = new Set(Object.keys(oldOp.responses ?? {}));
  const newCodes = new Set(Object.keys(newOp.responses ?? {}));
  for (const code of newCodes) {
    if (!oldCodes.has(code)) changes.push({ kind: 'ADDITIVE', detail: `+ response ${code}` });
  }
  for (const code of oldCodes) {
    if (!newCodes.has(code)) changes.push({ kind: 'BREAKING', detail: `- response ${code} bị xoá` });
  }

  // Lưới an toàn — chạy khi CHƯA có BREAKING (không chỉ khi chưa có thay đổi nào): một thay đổi
  // additive được gọi tên không được che một thay đổi khác mà classifier không gọi tên được
  // (security, response mất content, deprecated…). Nhãn ADDITIVE dẫn tới tự promote (#89) nên phải đúng.
  if (!changes.some((c) => c.kind === 'BREAKING')) {
    const unclassified = (op) => {
      const { parameters: _pa, requestBody, responses, operationId: _o, summary: _s, description: _d, tags: _t, ...rest } = op;
      const { content: _c, required: _r, ...bodyRest } = requestBody ?? {};
      const responsesRest = Object.fromEntries(
        Object.entries(responses ?? {}).map(([code, { content: _rc, description: _rd, ...r }]) => [code, r]),
      );
      return stable({ rest, bodyRest, responsesRest });
    };
    const sharedCodes = [...Object.keys(oldOp.responses ?? {})].filter((c) => c in (newOp.responses ?? {}));
    const lostContent = sharedCodes.some(
      (c) => Object.keys(oldOp.responses[c]?.content ?? {}).some((t) => !(t in (newOp.responses[c]?.content ?? {}))),
    );
    if (unclassified(oldOp) !== unclassified(newOp) || lostContent) {
      changes.push({ kind: 'BREAKING', detail: '~ operation khác ở chỗ ngoài phạm vi phân loại — đọc diff JSON' });
    }
  }

  return changes;
}

export function classifyDiff(oldDoc, newDoc) {
  const oldOps = flattenOperations(oldDoc);
  const newOps = flattenOperations(newDoc);

  const result = { newEndpoint: [], additive: [], breaking: [], removed: [], unchanged: [] };

  for (const [key, newOp] of newOps) {
    if (!oldOps.has(key)) {
      result.newEndpoint.push({ operation: key });
      continue;
    }

    const oldOp = oldOps.get(key);
    const changes = diffOperation(oldDoc, newDoc, oldOp, newOp);
    if (changes.length === 0) {
      result.unchanged.push({ operation: key });
    } else if (changes.some((c) => c.kind === 'BREAKING')) {
      result.breaking.push({ operation: key, changes });
    } else {
      result.additive.push({ operation: key, changes });
    }
  }

  for (const key of oldOps.keys()) {
    if (!newOps.has(key)) {
      result.removed.push({ operation: key });
    }
  }

  return result;
}

/** Mảng mà thứ tự KHÔNG mang nghĩa — so không phụ thuộc thứ tự (Microsoft.OpenApi có thể xuất khác thứ tự
 * giữa Windows và Linux). Không đổi luật normalize (SKILL contract-sync), chỉ áp trong phép so. */
const UNORDERED_KEYS = new Set(['required', 'tags']);

function canonical(value) {
  if (Array.isArray(value)) return value.map(canonical);
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(
      Object.keys(value)
        .sort()
        .map((k) => {
          const v = canonical(value[k]);
          return [k, UNORDERED_KEYS.has(k) && Array.isArray(v) && v.every((x) => typeof x === 'string') ? [...v].sort() : v];
        }),
    );
  }
  return value;
}

/** Hai document (đã normalize) có giống hệt nhau không — dùng làm cổng chặn cuối của drift check. */
export function documentsEqual(oldDoc, newDoc) {
  return stable(canonical(oldDoc)) === stable(canonical(newDoc));
}

/** JSON-path đầu tiên khác nhau — để thông báo của drift check chỉ ra được chỗ lệch. */
export function firstDifference(oldDoc, newDoc, path = '$') {
  const a = canonical(oldDoc);
  const b = canonical(newDoc);
  if (stable(a) === stable(b)) return null;
  if (a !== null && b !== null && typeof a === 'object' && typeof b === 'object' && Array.isArray(a) === Array.isArray(b)) {
    for (const key of new Set([...Object.keys(a), ...Object.keys(b)])) {
      const found = firstDifference(a[key], b[key], `${path}.${key}`);
      if (found) return found;
    }
  }
  return path;
}
