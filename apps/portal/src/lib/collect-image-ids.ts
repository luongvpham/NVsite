/**
 * Đệ quy tìm mọi `imageId` trong props của cây (top-level như Hero.image, hoặc lồng trong list như
 * Gallery.items[].image) — không cần biết trước shape theo manifest, chỉ cần nhận ra field
 * `{ imageId: string }` bất kỳ đâu. Dùng để dựng `mediaMap` qua `GET media/assets?ids=`.
 */
export interface NodeLike {
  props?: unknown;
  children?: readonly NodeLike[];
}

export function collectImageIds(root: NodeLike, acc: Set<string> = new Set()): Set<string> {
  walk(root.props, acc);
  for (const child of root.children ?? []) {
    collectImageIds(child, acc);
  }
  return acc;
}

function walk(value: unknown, acc: Set<string>): void {
  if (Array.isArray(value)) {
    for (const item of value) walk(item, acc);
    return;
  }
  if (value && typeof value === 'object') {
    const obj = value as Record<string, unknown>;
    if (typeof obj.imageId === 'string' && obj.imageId.length > 0) {
      acc.add(obj.imageId);
    }
    for (const v of Object.values(obj)) walk(v, acc);
  }
}
