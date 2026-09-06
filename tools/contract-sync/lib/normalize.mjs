/**
 * Normalize OpenAPI document trước khi diff (backend/CLAUDE.md — "Quy tắc normalize khi diff contract").
 * - sort key đệ quy
 * - bỏ servers và info.version
 * - chuẩn hoá whitespace trong description
 */

function normalizeWhitespace(value) {
  return value.replace(/\s+/g, ' ').trim();
}

function sortAndClean(value) {
  if (Array.isArray(value)) {
    return value.map(sortAndClean);
  }

  if (value !== null && typeof value === 'object') {
    const sortedKeys = Object.keys(value).sort();
    const result = {};
    for (const key of sortedKeys) {
      if (key === 'description' && typeof value[key] === 'string') {
        result[key] = normalizeWhitespace(value[key]);
      } else {
        result[key] = sortAndClean(value[key]);
      }
    }
    return result;
  }

  return value;
}

export function normalizeDocument(doc) {
  const clone = JSON.parse(JSON.stringify(doc));
  delete clone.servers;
  if (clone.info && typeof clone.info === 'object') {
    delete clone.info.version;
  }
  return sortAndClean(clone);
}
