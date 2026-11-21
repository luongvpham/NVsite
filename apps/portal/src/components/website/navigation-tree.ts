import { arrayMove } from '@dnd-kit/sortable';
import type { NavigationChildItemDto, NavigationItemDto, PageDto } from '@vsite/api-sdk';
import { isSafeLinkUrl } from '@vsite/builder-components';

/**
 * Thao tác thuần trên cây menu của màn Menu (WEBSITE-007 F4). Không React, không DOM — để test riêng và để
 * `navigation-editor.tsx` chỉ còn phần giao diện + kéo-thả.
 *
 * Cây trong editor giữ thêm `uid` (id phía client, ổn định suốt phiên sửa) — dnd-kit cần id duy nhất mà `key`
 * thì không duy nhất (nhiều mục `custom`). `uid` không bao giờ gửi lên BE.
 */

/** Khoá mục menu (#19: chuỗi template, không enum). */
export type NavKey = `page:${string}` | 'custom';

export interface NavItem {
  uid: string;
  key: NavKey;
  label: string | null;
  url: string | null;
  hidden: boolean;
  children: NavItem[];
}

/** Giới hạn BE (brief F4) — chặn ở UI để không phải đợi 422. */
export const NAV_LIMITS = {
  /** Số cấp tối đa (cấp 1..3). Độ sâu trong code tính từ 0 nên sâu nhất là `levels - 1`. */
  levels: 3,
  topLevel: 12,
  perParent: 10,
  label: 60,
  url: 2048,
} as const;

export const MAX_DEPTH = NAV_LIMITS.levels - 1;

export const isPageKey = (key: string): key is `page:${string}` => key.startsWith('page:');

/** Id trang trong khoá `page:{id}`, chữ thường (BE luôn trả chữ thường — so với `pages[].id` cũng phải hạ chữ). */
export const pageIdOf = (key: `page:${string}`): string => key.slice('page:'.length).toLowerCase();

export const pageKeyOf = (pageId: string): `page:${string}` => `page:${pageId.toLowerCase()}`;

/** Trang không được đưa vào menu (BE: `NAV_PAGE_NOT_ALLOWED`). */
export const isPageAllowedInMenu = (page: PageDto): boolean => page.systemType !== 'NotFound';

let uidCounter = 0;
export const newUid = (): string => {
  uidCounter += 1;
  return `nav-${String(uidCounter)}`;
};

type AnyItemDto = {
  key: string;
  label: string | null;
  url: string | null;
  hidden: boolean;
  children?: AnyItemDto[];
};

function toNavKey(key: string): NavKey {
  // BE chỉ trả `page:` hoặc `custom` (nguồn khác để Bước 9/10c). Gặp khoá lạ thì giữ dạng `page:` để không mất mục —
  // dòng sẽ hiện "Trang không còn tồn tại" và người dùng xoá được.
  if (key === 'custom') return 'custom';
  return isPageKey(key) ? pageKeyOf(key.slice('page:'.length)) : `page:${key.toLowerCase()}`;
}

export function fromDto(items: readonly AnyItemDto[]): NavItem[] {
  return items.map((dto) => ({
    uid: newUid(),
    key: toNavKey(dto.key),
    label: dto.label,
    url: dto.key === 'custom' ? dto.url : null,
    hidden: dto.hidden,
    children: fromDto(dto.children ?? []),
  }));
}

/**
 * Body PUT: đủ `key`, `label`, `url`, `hidden`, `children` ở mọi mục; cấp 3 **không** có `children`
 * (BE trả 400 nếu có); mục `page:` gửi `url: null`; nhãn trống ⇒ `null` (dùng tên trang).
 */
export function toRequestItems(items: readonly NavItem[]): NavigationItemDto[] {
  const base = (item: NavItem) => ({
    key: item.key,
    label: item.label === null || item.label.trim() === '' ? null : item.label,
    url: item.key === 'custom' ? item.url : null,
    hidden: item.hidden,
  });
  return items.map((top): NavigationItemDto => ({
    ...base(top),
    children: top.children.map((child): NavigationChildItemDto => ({
      ...base(child),
      children: child.children.map((grand) => base(grand)),
    })),
  }));
}

// ── Duyệt cây ─────────────────────────────────────────────────────────────────────────────────────────────

export interface FlatNavItem {
  uid: string;
  parentUid: string | null;
  depth: number;
  item: NavItem;
}

export function flatten(
  items: readonly NavItem[],
  parentUid: string | null = null,
  depth = 0,
): FlatNavItem[] {
  return items.flatMap((item) => [
    { uid: item.uid, parentUid, depth, item },
    ...flatten(item.children, item.uid, depth + 1),
  ]);
}

/** Bỏ mọi hậu duệ của `uid` khỏi danh sách phẳng (lúc kéo, cả nhóm con đi theo mục cha). */
export function removeDescendants(flat: readonly FlatNavItem[], uid: string): FlatNavItem[] {
  const excluded = new Set<string>([uid]);
  return flat.filter((entry) => {
    if (entry.parentUid !== null && excluded.has(entry.parentUid)) {
      excluded.add(entry.uid);
      return false;
    }
    return true;
  });
}

/** Chiều cao cây con dưới `item` (0 = không có con). */
export function subtreeHeight(item: NavItem): number {
  return item.children.reduce((max, child) => Math.max(max, 1 + subtreeHeight(child)), 0);
}

export function findItem(items: readonly NavItem[], uid: string): NavItem | undefined {
  for (const item of items) {
    if (item.uid === uid) return item;
    const found = findItem(item.children, uid);
    if (found) return found;
  }
  return undefined;
}

export function updateItem(
  items: readonly NavItem[],
  uid: string,
  patch: Partial<Omit<NavItem, 'uid' | 'children'>>,
): NavItem[] {
  return items.map((item) =>
    item.uid === uid
      ? { ...item, ...patch }
      : { ...item, children: updateItem(item.children, uid, patch) },
  );
}

export function removeItem(items: readonly NavItem[], uid: string): NavItem[] {
  return items
    .filter((item) => item.uid !== uid)
    .map((item) => ({ ...item, children: removeItem(item.children, uid) }));
}

export function countItems(items: readonly NavItem[]): number {
  return items.reduce((sum, item) => sum + 1 + countItems(item.children), 0);
}

/** Id (chữ thường) các trang đã có trong menu — một trang chỉ xuất hiện một lần. */
export function pageIdsInMenu(items: readonly NavItem[]): Set<string> {
  return new Set(
    flatten(items)
      .map((entry) => entry.item.key)
      .filter(isPageKey)
      .map(pageIdOf),
  );
}

// ── Kéo-thả: dự đoán cấp khi thả (theo mẫu SortableTree của dnd-kit) ───────────────────────────────────────

export interface Projection {
  depth: number;
  minDepth: number;
  maxDepth: number;
  parentUid: string | null;
  /** `false` khi không có cấp nào hợp lệ ở vị trí này (mục có con sẽ bị đẩy quá 3 cấp). */
  valid: boolean;
}

/**
 * Cấp mới của mục đang kéo nếu thả trên `overUid` với độ lệch ngang `offsetX`. `flat` là danh sách phẳng **đã bỏ
 * hậu duệ của mục đang kéo**.
 *
 * - `maxDepth`: tối đa sâu hơn mục ngay trên 1 cấp, và không để cây con của mục kéo vượt cấp 3 (chặn cấp 4).
 * - `minDepth`: không nông hơn mục ngay dưới (nếu không, mục dưới sẽ "đổi cha" ngoài ý muốn).
 */
export function getProjection(
  flat: readonly FlatNavItem[],
  activeUid: string,
  overUid: string,
  offsetX: number,
  indentWidth: number,
): Projection {
  const overIndex = flat.findIndex((entry) => entry.uid === overUid);
  const activeIndex = flat.findIndex((entry) => entry.uid === activeUid);
  const active = flat[activeIndex];
  if (!active || overIndex < 0)
    return { depth: 0, minDepth: 0, maxDepth: 0, parentUid: null, valid: false };

  const moved = arrayMove([...flat], activeIndex, overIndex);
  const previous = moved[overIndex - 1];
  const next = moved[overIndex + 1];

  const dragDepth = Math.round(offsetX / indentWidth);
  const projected = active.depth + dragDepth;
  const structuralMax = previous ? previous.depth + 1 : 0;
  const maxDepth = Math.min(structuralMax, MAX_DEPTH - subtreeHeight(active.item));
  const minDepth = next ? next.depth : 0;
  const valid = maxDepth >= minDepth;

  let depth = projected;
  if (depth > maxDepth) depth = maxDepth;
  if (depth < minDepth) depth = minDepth;

  return { depth, minDepth, maxDepth, parentUid: parentFor(depth), valid };

  function parentFor(d: number): string | null {
    if (d === 0 || !previous) return null;
    if (d === previous.depth) return previous.parentUid;
    if (d > previous.depth) return previous.uid;
    return (
      moved
        .slice(0, overIndex)
        .reverse()
        .find((entry) => entry.depth === d)?.parentUid ?? null
    );
  }
}

export type MoveResult = { ok: true; items: NavItem[] } | { ok: false; message: string };

/** Áp nước thả: đưa `activeUid` (kèm cả cây con) tới vị trí của `overUid` ở cấp `projection`. */
export function applyMove(
  items: readonly NavItem[],
  activeUid: string,
  overUid: string,
  projection: Projection,
): MoveResult {
  if (!projection.valid) {
    return {
      ok: false,
      message: `Không thả được ở đây: menu tối đa ${String(NAV_LIMITS.levels)} cấp.`,
    };
  }
  const flat = removeDescendants(flatten(items), activeUid);
  const activeIndex = flat.findIndex((entry) => entry.uid === activeUid);
  const overIndex = flat.findIndex((entry) => entry.uid === overUid);
  const active = flat[activeIndex];
  if (!active || overIndex < 0) return { ok: true, items: [...items] };

  const updated = [...flat];
  updated[activeIndex] = { ...active, depth: projection.depth, parentUid: projection.parentUid };
  const sorted = arrayMove(updated, activeIndex, overIndex);
  const next = buildTree(sorted, activeUid);

  const siblings =
    projection.parentUid === null ? next : (findItem(next, projection.parentUid)?.children ?? []);
  const limit = projection.parentUid === null ? NAV_LIMITS.topLevel : NAV_LIMITS.perParent;
  if (siblings.length > limit) {
    return {
      ok: false,
      message:
        projection.parentUid === null
          ? `Không thả được: tối đa ${String(NAV_LIMITS.topLevel)} mục cấp 1.`
          : `Không thả được: mỗi nhóm con tối đa ${String(NAV_LIMITS.perParent)} mục.`,
    };
  }
  return { ok: true, items: next };
}

/** Dựng lại cây từ danh sách phẳng theo `parentUid`. Mục `keepChildrenOf` giữ nguyên cây con (đã bị bỏ khỏi `flat`). */
function buildTree(flat: readonly FlatNavItem[], keepChildrenOf: string): NavItem[] {
  const nodes = new Map<string, NavItem>();
  const roots: NavItem[] = [];
  for (const entry of flat) {
    const node: NavItem = {
      ...entry.item,
      children: entry.uid === keepChildrenOf ? entry.item.children : [],
    };
    nodes.set(entry.uid, node);
    const parent = entry.parentUid === null ? undefined : nodes.get(entry.parentUid);
    if (parent) parent.children.push(node);
    else roots.push(node);
  }
  return roots;
}

// ── Kiểm tra trước khi lưu + đọc lỗi 422 ───────────────────────────────────────────────────────────────────

/** Lỗi theo dòng (`uid`) — giá trị là danh sách `code` (map ra câu chữ ở giao diện). */
export type RowErrors = Map<string, string[]>;

function addError(errors: RowErrors, uid: string, message: string) {
  const list = errors.get(uid);
  if (list) {
    if (!list.includes(message)) list.push(message);
  } else errors.set(uid, [message]);
}

/** Kiểm như BE (phần làm được ở client). Trả mã lỗi `NAV_*` theo dòng; rỗng = gửi được. */
export function validateMenu(
  items: readonly NavItem[],
  pagesById: ReadonlyMap<string, PageDto>,
): RowErrors {
  const errors: RowErrors = new Map();
  const seenPages = new Set<string>();

  const walk = (list: readonly NavItem[], depth: number) => {
    for (const item of list) {
      const label = item.label?.trim() ?? '';
      if (label.length > NAV_LIMITS.label) addError(errors, item.uid, 'NAV_LABEL_TOO_LONG');
      if (item.key === 'custom') {
        if (label === '') addError(errors, item.uid, 'NAV_LABEL_REQUIRED');
        const url = item.url?.trim() ?? '';
        if (url.length > NAV_LIMITS.url || !isSafeLinkUrl(url))
          addError(errors, item.uid, 'NAV_URL_UNSAFE');
      } else {
        const pageId = pageIdOf(item.key);
        const page = pagesById.get(pageId);
        if (!page) addError(errors, item.uid, 'NAV_PAGE_NOT_FOUND');
        else if (!isPageAllowedInMenu(page)) addError(errors, item.uid, 'NAV_PAGE_NOT_ALLOWED');
        if (seenPages.has(pageId)) addError(errors, item.uid, 'NAV_PAGE_DUPLICATE');
        seenPages.add(pageId);
      }
      if (depth >= MAX_DEPTH && item.children.length > 0)
        addError(errors, item.uid, 'NAV_TOO_DEEP');
      if (item.children.length > NAV_LIMITS.perParent)
        addError(errors, item.uid, 'NAV_TOO_MANY_ITEMS');
      walk(item.children, depth + 1);
    }
  };
  walk(items, 0);
  return errors;
}

/**
 * Dòng mà JSON Pointer của 422 trỏ tới, tính trên đúng mảng `items` đã gửi. `/items/2/children/0/url` ⇒ mục
 * `items[2].children[0]`; `/items/2/children` (mảng bị tràn) ⇒ mục cha `items[2]`; `/items` hoặc không đọc
 * được ⇒ `null` (lỗi chung).
 */
export function uidAtPointer(items: readonly NavItem[], pointer: string): string | null {
  const segments = pointer.split('/').slice(1);
  if (segments[0] !== 'items') return null;
  let list: readonly NavItem[] = items;
  let current: NavItem | null = null;
  let i = 1;
  while (i < segments.length) {
    const index = Number(segments[i]);
    if (!Number.isInteger(index)) break;
    const next = list[index];
    if (!next) return current?.uid ?? null;
    current = next;
    if (segments[i + 1] !== 'children') break;
    list = next.children;
    i += 2;
  }
  return current?.uid ?? null;
}
