import { executeOperation } from '../operations/apply';
import { PageKind, type TreeEnvelope, type TreeNode } from '../tree/types';
import { envelope } from './trees';

/**
 * Bộ fixture cây dùng chung FE + BE (`packages/builder-components/tests/fixtures/tree/README.md`). Nạp thẳng từ đó bằng
 * `import.meta.glob` của Vite — KHÔNG chép sang chỗ khác (brief WEBSITE-002 "Fixture dùng chung = bộ test hợp đồng") và
 * không cần `node:fs`/`@types/node`.
 */
const modules = import.meta.glob('../../../builder-components/tests/fixtures/tree/*.json', {
  eager: true,
  import: 'default',
});

export interface TreeFixture {
  file: string;
  description: string;
  pageKind: 'Composable' | 'System';
  systemType?: string;
  tree?: TreeEnvelope;
  stored?: TreeEnvelope;
  incoming?: TreeEnvelope;
  rulesOverride?: unknown;
  expect: 'valid' | 'invalid';
  errorCodes?: string[];
}

export function loadFixtures(): TreeFixture[] {
  return Object.entries(modules)
    .map(([modulePath, content]) => ({ file: modulePath.slice(modulePath.lastIndexOf('/') + 1), content }))
    .filter(({ file }) => file !== 'xss-richtext.json')
    .sort((a, b) => (a.file < b.file ? -1 : a.file > b.file ? 1 : 0))
    .map(({ file, content }) => ({ file, ...(content as Omit<TreeFixture, 'file'>) }));
}

/** Bản sao sâu — Immer đóng băng kết quả nên không để fixture gốc (module dùng chung) bị đóng băng giữa các test. */
export const clone = <T>(value: T): T => structuredClone(value);

export type ReplayResult =
  | { ok: true; tree: TreeEnvelope }
  | { ok: false; code: string; nodeId: string; tree: TreeEnvelope };

/**
 * Dựng lại `source` từ cây rỗng bằng chuỗi op `add` (cha trước con, đúng thứ tự anh em), dừng ở op đầu tiên bị
 * Engine từ chối. Chứng minh Engine chấp nhận đúng những cây BE chấp nhận và từ chối đúng mã BE từ chối.
 */
export function replayAsAdds(source: TreeEnvelope, pageKind: PageKind = PageKind.Composable): ReplayResult {
  let tree = envelope();
  const queue: { parentId: string; index: number; node: TreeNode }[] = source.children.map((node, index) => ({
    parentId: 'root',
    index,
    node,
  }));
  while (queue.length > 0) {
    const item = queue.shift();
    if (!item) break;
    const { children, ...flat } = item.node;
    const result = executeOperation({ type: 'add', parentId: item.parentId, index: item.index, node: flat }, { tree, pageKind });
    if (!result.ok) return { ok: false, code: result.code, nodeId: item.node.id, tree };
    tree = result.tree;
    (children ?? []).forEach((child, index) => queue.push({ parentId: item.node.id, index, node: child }));
  }
  return { ok: true, tree };
}
