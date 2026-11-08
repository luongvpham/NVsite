import { opRules } from '@vsite/builder-components';
import { ROOT_ID, findNode, type TreeEnvelope } from '@vsite/builder-core';

export interface AddTarget {
  parentId: string;
  index: number;
}

const rules = opRules as unknown as Record<string, { acceptsChildren: boolean }>;

/**
 * Khối mới đặt ở đâu khi người bấm "Thêm" trong palette:
 * - chọn khối chứa được con (Section) → cuối danh sách con của nó;
 * - chọn khối khác → ngay sau nó, cùng cha;
 * - chưa chọn / id chọn không còn trong cây → cuối trang.
 * Chỉ là gợi ý vị trí; Engine vẫn kiểm luật ([1]–[5], `allowedChildTypes`, độ sâu…).
 */
export function resolveAddTarget(tree: TreeEnvelope, selectedId: string | null): AddTarget {
  const found = selectedId ? findNode(tree, selectedId) : undefined;
  if (!found) return { parentId: ROOT_ID, index: tree.children.length };

  const acceptsChildren = Object.hasOwn(rules, found.node.type) && rules[found.node.type]?.acceptsChildren === true;
  if (acceptsChildren) return { parentId: found.node.id, index: found.node.children?.length ?? 0 };
  return { parentId: found.parentId, index: found.index + 1 };
}
