import { ROOT_ID, findNode, type TreeEnvelope } from '@vsite/builder-core';

export interface SiblingMove {
  toParentId: string;
  /** Tính trong danh sách con của cha **sau khi gỡ node** (quy ước của op `move`). */
  toIndex: number;
}

/**
 * Đích của nút Lên/Xuống: đổi chỗ với anh em liền kề, **cùng cha**. `null` khi đã ở đầu/cuối (hoặc không có node).
 * Rời Section hay vào Section khác là việc của kéo-thả — nút này cố ý không làm.
 */
export function siblingMove(tree: TreeEnvelope, nodeId: string, direction: 'up' | 'down'): SiblingMove | null {
  const found = findNode(tree, nodeId);
  if (!found) return null;

  const parent = found.parentId === ROOT_ID ? undefined : findNode(tree, found.parentId)?.node;
  const siblingCount = found.parentId === ROOT_ID ? tree.children.length : (parent?.children?.length ?? 0);

  const toIndex = direction === 'up' ? found.index - 1 : found.index + 1;
  if (toIndex < 0 || toIndex >= siblingCount) return null;
  return { toParentId: found.parentId, toIndex };
}
