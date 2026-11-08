import { produce } from 'immer';
import { findNode } from '../tree/query';
import { ROOT_ID, type TreeEnvelope, type TreeNode } from '../tree/types';
import { setIn } from './paths';
import type { OperationResult } from './result';
import { parseOperation, type AddOperation, type MoveOperation, type Operation, type RemoveOperation, type UpdateOperation } from './types';
import { validateOperation, type ValidationContext } from './validate';

/** Danh sách con (có thể tạo mới) của `parentId` trong draft Immer. */
function childrenOf(draft: TreeEnvelope, parentId: string): TreeNode[] {
  if (parentId === ROOT_ID) return draft.children;
  const parent = findNode(draft, parentId)?.node;
  if (!parent) throw new Error(`applyOperation: không có cha '${parentId}' (op chưa được validate?)`);
  parent.children ??= [];
  return parent.children;
}

function applyAdd(draft: TreeEnvelope, op: AddOperation): void {
  childrenOf(draft, op.parentId).splice(op.index, 0, op.node as TreeNode);
}

function applyRemove(draft: TreeEnvelope, op: RemoveOperation): void {
  const found = findNode(draft, op.nodeId);
  if (!found) throw new Error(`applyOperation: không có node '${op.nodeId}' (op chưa được validate?)`);
  childrenOf(draft, found.parentId).splice(found.index, 1);
}

function applyMove(draft: TreeEnvelope, op: MoveOperation): void {
  const found = findNode(draft, op.nodeId);
  if (!found) throw new Error(`applyOperation: không có node '${op.nodeId}' (op chưa được validate?)`);
  const [moved] = childrenOf(draft, found.parentId).splice(found.index, 1);
  if (moved) childrenOf(draft, op.toParentId).splice(op.toIndex, 0, moved);
}

function applyUpdate(draft: TreeEnvelope, op: UpdateOperation, nextProps: Record<string, unknown>): void {
  const node = findNode(draft, op.nodeId)?.node;
  if (!node) throw new Error(`applyOperation: không có node '${op.nodeId}' (op chưa được validate?)`);
  node.props = nextProps;
}

/**
 * Áp một op ĐÃ qua `validateOperation` lên cây bằng Immer → cây mới (structural sharing, cây cũ không bị đổi). Op không làm
 * đổi gì (update cùng giá trị, move về đúng chỗ cũ) trả lại CHÍNH cây cũ để Store không ghi một bước undo rỗng.
 * `changeTheme` không đụng cây (theme nằm ngoài tree).
 */
export function applyOperation(tree: TreeEnvelope, op: Operation): TreeEnvelope {
  switch (op.type) {
    case 'changeTheme':
      return tree;
    case 'add':
      return produce(tree, (draft) => {
        applyAdd(draft, op);
      });
    case 'remove':
      return produce(tree, (draft) => {
        applyRemove(draft, op);
      });
    case 'move': {
      const found = findNode(tree, op.nodeId);
      if (found && found.parentId === op.toParentId && found.index === op.toIndex) return tree;
      return produce(tree, (draft) => {
        applyMove(draft, op);
      });
    }
    case 'update': {
      const node = findNode(tree, op.nodeId)?.node;
      const before = node?.props ?? {};
      const nextProps = setIn(before, op.path, op.value) as Record<string, unknown>;
      if (JSON.stringify(nextProps) === JSON.stringify(before)) return tree;
      return produce(tree, (draft) => {
        applyUpdate(draft, op, nextProps);
      });
    }
  }
}

export type ExecuteResult = { ok: true; op: Operation; tree: TreeEnvelope } | (OperationResult & { ok: false });

/** parse → validate → apply. Không ném lỗi: op từ UI/AI có thể sai, kết quả là `ok:false` kèm mã và thông báo. */
export function executeOperation(input: unknown, ctx: ValidationContext): ExecuteResult {
  const parsed = parseOperation(input);
  if (!parsed.ok) return { ok: false, code: parsed.code, message: parsed.message };
  const checked = validateOperation(parsed.op, ctx);
  if (!checked.ok) return checked;
  return { ok: true, op: parsed.op, tree: applyOperation(ctx.tree, parsed.op) };
}
