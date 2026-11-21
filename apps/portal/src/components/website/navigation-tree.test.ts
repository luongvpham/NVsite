import { describe, expect, it } from 'vitest';
import {
  applyMove,
  flatten,
  fromDto,
  getProjection,
  removeDescendants,
  uidAtPointer,
  type NavItem,
} from './navigation-tree';

interface Dto {
  key: string;
  label: string | null;
  url: string | null;
  hidden: boolean;
  children: Dto[];
}

const dto = (key: string, children: Dto[] = []): Dto => ({
  key,
  label: null,
  url: key === 'custom' ? '/x' : null,
  hidden: false,
  children,
});

/** Lấy phần tử, ném lỗi nếu thiếu (tránh `!`). */
function at<T>(list: readonly T[], index: number): T {
  const value = list[index];
  if (value === undefined) throw new Error(`Thiếu phần tử ${String(index)}`);
  return value;
}

function tree(): NavItem[] {
  // a[ a1[ a1x ], a2 ], b
  return fromDto([
    dto('page:a', [dto('page:a1', [dto('page:a1x')]), dto('page:a2')]),
    dto('page:b'),
  ]);
}

describe('uidAtPointer', () => {
  const items = tree();
  const a = at(items, 0);
  const b = at(items, 1);
  const a1 = at(a.children, 0);

  it('trỏ tới mục theo /items/i/children/j/...', () => {
    expect(uidAtPointer(items, '/items/0/children/0/children/0/url')).toBe(at(a1.children, 0).uid);
    expect(uidAtPointer(items, '/items/1/label')).toBe(b.uid);
    expect(uidAtPointer(items, '/items/0')).toBe(a.uid);
  });

  it('mảng con bị tràn (`/items/0/children`) ⇒ mục cha; `/items` hoặc lạ ⇒ null', () => {
    expect(uidAtPointer(items, '/items/0/children')).toBe(a.uid);
    expect(uidAtPointer(items, '/items')).toBeNull();
    expect(uidAtPointer(items, '/version')).toBeNull();
    expect(uidAtPointer(items, '/items/9')).toBeNull();
  });
});

describe('fromDto', () => {
  it('hạ chữ thường id trong khoá page:, bỏ url của mục page', () => {
    const item = at(
      fromDto([{ key: 'page:AB-1', label: null, url: '/bo-di', hidden: false, children: [] }]),
      0,
    );
    expect(item.key).toBe('page:ab-1');
    expect(item.url).toBeNull();
  });
});

describe('getProjection / applyMove', () => {
  it('không cho mục có con xuống quá cấp 2 (cây con vượt cấp 3)', () => {
    const items = fromDto([
      dto('page:a', [dto('page:a1', [dto('page:a1x')])]),
      dto('custom', [dto('page:c1')]),
    ]);
    const custom = at(items, 1);
    const flat = removeDescendants(flatten(items), custom.uid);
    const projection = getProjection(flat, custom.uid, custom.uid, 24 * 5, 24);
    expect(projection.maxDepth).toBe(1);
    expect(projection.depth).toBe(1);
  });

  it('vị trí không có cấp hợp lệ ⇒ applyMove từ chối', () => {
    const items = tree();
    const b = at(items, 1);
    const projection = { depth: 0, minDepth: 2, maxDepth: 1, parentUid: null, valid: false };
    expect(applyMove(items, b.uid, b.uid, projection).ok).toBe(false);
  });

  it('chặn thả khi nhóm đích đã đủ 10 mục', () => {
    const items = fromDto([
      dto(
        'page:p',
        Array.from({ length: 10 }, (_, i) => dto(`page:c${String(i)}`)),
      ),
      dto('page:x'),
    ]);
    const x = at(items, 1);
    const flat = removeDescendants(flatten(items), x.uid);
    const projection = getProjection(flat, x.uid, x.uid, 24, 24);
    expect(projection.parentUid).toBe(at(items, 0).uid);
    expect(applyMove(items, x.uid, x.uid, projection)).toEqual({
      ok: false,
      message: 'Không thả được: mỗi nhóm con tối đa 10 mục.',
    });
  });

  it('kéo mục cha mang theo cả cây con', () => {
    const items = tree();
    const a = at(items, 0);
    const b = at(items, 1);
    const flat = removeDescendants(flatten(items), a.uid);
    const projection = getProjection(flat, a.uid, b.uid, 0, 24);
    const result = applyMove(items, a.uid, b.uid, projection);
    if (!result.ok) throw new Error(result.message);
    expect(result.items.map((i) => i.key)).toEqual(['page:b', 'page:a']);
    expect(at(result.items, 1).children.map((c) => c.key)).toEqual(['page:a1', 'page:a2']);
  });
});
