/**
 * Diff hai OpenAPI document đã normalize, phân loại theo từng operation
 * (method + path) — NEW_ENDPOINT / ADDITIVE / BREAKING / REMOVED / UNCHANGED.
 *
 * Giới hạn đã biết: so sánh schema chỉ resolve $ref một cấp (component trực
 * tiếp gắn ở request/response của operation). Không walk đệ quy toàn bộ cây
 * schema lồng nhau — đủ cho Bước 1, cần mở rộng khi module thật (nhiều field
 * lồng sâu hơn) bắt đầu sinh false negative.
 */

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

function extractSchemaRefs(operation) {
  const refs = [];

  // Không hardcode 'application/json' — lỗi thật đã gặp: response 4xx/5xx dùng
  // 'application/problem+json' (ProblemDetails), bỏ qua content-type đó làm diff
  // không bao giờ bắt được thay đổi ở error schema (chính là chỗ error_code sống).
  for (const content of Object.values(operation.requestBody?.content ?? {})) {
    if (content?.schema?.$ref) refs.push(content.schema.$ref);
  }

  for (const response of Object.values(operation.responses ?? {})) {
    for (const content of Object.values(response?.content ?? {})) {
      if (content?.schema?.$ref) refs.push(content.schema.$ref);
    }
  }

  return refs;
}

function diffSchema(oldSchema, newSchema) {
  const changes = [];
  if (!oldSchema || !newSchema) return changes;

  const oldProps = oldSchema.properties ?? {};
  const newProps = newSchema.properties ?? {};
  const oldRequired = new Set(oldSchema.required ?? []);
  const newRequired = new Set(newSchema.required ?? []);

  for (const key of Object.keys(newProps)) {
    if (!(key in oldProps)) {
      changes.push({
        kind: newRequired.has(key) ? 'BREAKING' : 'ADDITIVE',
        detail: `+ field '${key}'${newRequired.has(key) ? ' (required)' : ' (optional)'}`,
      });
      continue;
    }

    const oldProp = oldProps[key];
    const newProp = newProps[key];
    if (JSON.stringify(oldProp) !== JSON.stringify(newProp)) {
      changes.push({ kind: 'BREAKING', detail: `~ field '${key}' đổi shape` });
    }

    if (oldRequired.has(key) !== newRequired.has(key)) {
      changes.push({
        kind: newRequired.has(key) ? 'BREAKING' : 'ADDITIVE',
        detail: `~ field '${key}' đổi nullable/required`,
      });
    }
  }

  for (const key of Object.keys(oldProps)) {
    if (!(key in newProps)) {
      changes.push({ kind: 'BREAKING', detail: `- field '${key}' bị xoá` });
    }
  }

  return changes;
}

function diffOperation(oldDoc, newDoc, oldOp, newOp) {
  const changes = [];

  const oldRefs = extractSchemaRefs(oldOp);
  const newRefs = extractSchemaRefs(newOp);
  const allRefs = new Set([...oldRefs, ...newRefs]);

  for (const ref of allRefs) {
    const oldSchema = resolveRef(oldDoc, ref);
    const newSchema = resolveRef(newDoc, ref);
    changes.push(...diffSchema(oldSchema, newSchema));
  }

  const oldCodes = new Set(Object.keys(oldOp.responses ?? {}));
  const newCodes = new Set(Object.keys(newOp.responses ?? {}));
  for (const code of newCodes) {
    if (!oldCodes.has(code)) changes.push({ kind: 'ADDITIVE', detail: `+ response ${code}` });
  }
  for (const code of oldCodes) {
    if (!newCodes.has(code)) changes.push({ kind: 'BREAKING', detail: `- response ${code} bị xoá` });
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
