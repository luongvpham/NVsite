import { ROOT_ID, type PropPathSegment, type TreeEnvelope, type TreeNode } from './types';

export interface FoundNode {
  node: TreeNode;
  /** `"root"` khi là con trực tiếp của gốc. */
  parentId: string;
  /** Vị trí trong danh sách con của cha. */
  index: number;
  /** Con trực tiếp của gốc = 1 (cùng quy ước `PageTreeLimits.MaxDepth` của BE). */
  depth: number;
}

/** Duyệt tiền thứ tự có stack tường minh — cây tới 2000 node/sâu 20 vẫn an toàn, không đệ quy. */
function* walk(tree: TreeEnvelope): Generator<FoundNode> {
  const stack: FoundNode[] = tree.children.map((node, index) => ({ node, parentId: ROOT_ID, index, depth: 1 })).reverse();
  while (stack.length > 0) {
    const current = stack.pop();
    if (!current) break;
    yield current;
    const children = current.node.children ?? [];
    for (let index = children.length - 1; index >= 0; index--) {
      const child = children[index];
      if (child) stack.push({ node: child, parentId: current.node.id, index, depth: current.depth + 1 });
    }
  }
}

/** Tìm node theo id. Gốc `"root"` không phải node → `undefined`. */
export function findNode(tree: TreeEnvelope, id: string): FoundNode | undefined {
  for (const found of walk(tree)) if (found.node.id === id) return found;
  return undefined;
}

export function collectIds(tree: TreeEnvelope): Set<string> {
  const ids = new Set<string>();
  for (const { node } of walk(tree)) ids.add(node.id);
  return ids;
}

/** Số node (không tính gốc). */
export function countNodes(tree: TreeEnvelope): number {
  return [...walk(tree)].length;
}

/** Độ sâu lớn nhất; gốc rỗng = 0. */
export function maxDepth(tree: TreeEnvelope): number {
  let depth = 0;
  for (const found of walk(tree)) depth = Math.max(depth, found.depth);
  return depth;
}

/** Chiều cao của một nhánh tính từ chính nó (node không con = 1) — dùng khi dời nhánh xuống chỗ sâu hơn. */
export function subtreeHeight(node: TreeNode): number {
  let height = 0;
  const stack: { node: TreeNode; depth: number }[] = [{ node, depth: 1 }];
  while (stack.length > 0) {
    const current = stack.pop();
    if (!current) break;
    height = Math.max(height, current.depth);
    for (const child of current.node.children ?? []) stack.push({ node: child, depth: current.depth + 1 });
  }
  return height;
}

/** `true` nếu `id` là chính `ancestorId` hoặc nằm trong nhánh của nó. */
export function isSelfOrDescendant(tree: TreeEnvelope, ancestorId: string, id: string): boolean {
  const ancestor = findNode(tree, ancestorId);
  if (!ancestor) return false;
  const stack: TreeNode[] = [ancestor.node];
  while (stack.length > 0) {
    const current = stack.pop();
    if (!current) break;
    if (current.id === id) return true;
    stack.push(...(current.children ?? []));
  }
  return false;
}

// ── JSON Pointer ↔ node/prop: 422 `TREE_INVALID` báo lỗi theo `path` (RFC 6901), Inspector tô đỏ theo node + prop ──

const escapeSegment = (segment: string | number): string => String(segment).replaceAll('~', '~0').replaceAll('/', '~1');
const unescapeSegment = (segment: string): string => segment.replaceAll('~1', '/').replaceAll('~0', '~');

/** Chuỗi vị trí `[i0, i1, …]` trong `children` từ gốc tới node; `undefined` nếu không có node. */
function indexChain(tree: TreeEnvelope, nodeId: string): number[] | undefined {
  const found = findNode(tree, nodeId);
  if (!found) return undefined;
  const chain = [found.index];
  let parentId = found.parentId;
  while (parentId !== ROOT_ID) {
    const parent = findNode(tree, parentId);
    if (!parent) return undefined;
    chain.unshift(parent.index);
    parentId = parent.parentId;
  }
  return chain;
}

/** Pointer tới node (hoặc tới một prop của nó): `/children/0/children/1/props/cta/label`. */
export function pointerOf(tree: TreeEnvelope, nodeId: string, propPath: PropPathSegment[] = []): string | undefined {
  const chain = indexChain(tree, nodeId);
  if (!chain) return undefined;
  const nodePart = chain.map((i) => `/children/${String(i)}`).join('');
  const propPart = propPath.length > 0 ? `/props/${propPath.map(escapeSegment).join('/')}` : '';
  return nodePart + propPart;
}

export interface ResolvedPointer {
  /** `null` = lỗi ở gốc/vỏ (banner chung), không gắn được vào node nào. */
  nodeId: string | null;
  /** Đường dẫn trong `props` của node; rỗng khi lỗi ở chính node (`id`/`type`/`variant`/`children`…). */
  propPath: PropPathSegment[];
}

/**
 * Ngược lại `pointerOf`. `null` khi pointer hỏng hoặc node không còn trong cây (path cũ sau khi đã sửa tiếp, R5) —
 * người gọi bỏ qua việc tô đỏ nhưng vẫn hiện mã lỗi.
 */
export function resolvePointer(tree: TreeEnvelope, pointer: string): ResolvedPointer | null {
  if (pointer === '') return { nodeId: null, propPath: [] };
  if (!pointer.startsWith('/')) return null;
  const segments = pointer.slice(1).split('/').map(unescapeSegment);

  if (segments[0] !== 'children') return { nodeId: null, propPath: segments };

  let siblings: TreeNode[] = tree.children;
  let node: TreeNode | undefined;
  let i = 0;
  while (segments[i] === 'children') {
    const rawIndex = segments[i + 1];
    if (rawIndex === undefined || !/^\d+$/.test(rawIndex)) return null;
    node = siblings[Number(rawIndex)];
    if (!node) return null;
    siblings = node.children ?? [];
    i += 2;
  }
  if (!node) return null;

  if (segments[i] === 'props') {
    const propPath = segments.slice(i + 1).map((s): PropPathSegment => (/^\d+$/.test(s) ? Number(s) : s));
    return { nodeId: node.id, propPath };
  }
  return { nodeId: node.id, propPath: [] };
}
