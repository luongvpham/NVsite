export type PathSegment = string | number;

export function getAtPath(obj: unknown, path: PathSegment[]): unknown {
  let current = obj;
  for (const segment of path) {
    if (current === null || current === undefined) return undefined;
    current = (current as Record<PathSegment, unknown>)[segment];
  }
  return current;
}

/** Cập nhật immutable — không mutate `obj` gốc (props ở Bước 5 sẽ đi qua Operations Engine). */
export function setAtPath(obj: Record<string, unknown>, path: PathSegment[], value: unknown): Record<string, unknown> {
  if (path.length === 0) return obj;

  const [head, ...rest] = path;
  if (head === undefined) return obj;

  if (rest.length === 0) {
    if (typeof head === 'number') {
      const arr = Array.isArray(obj) ? [...obj] : [];
      arr[head] = value;
      return arr as unknown as Record<string, unknown>;
    }
    return { ...obj, [head]: value };
  }

  const childValue = (obj as Record<PathSegment, unknown>)[head];
  const updatedChild = setAtPath(
    (childValue as Record<string, unknown> | undefined) ?? (typeof rest[0] === 'number' ? ([] as unknown as Record<string, unknown>) : {}),
    rest,
    value,
  );

  if (typeof head === 'number') {
    const arr = Array.isArray(obj) ? [...obj] : [];
    arr[head] = updatedChild;
    return arr as unknown as Record<string, unknown>;
  }
  return { ...obj, [head]: updatedChild };
}
