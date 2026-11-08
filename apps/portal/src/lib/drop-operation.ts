import {
  fail,
  findNode,
  OperationErrorCode,
  validateOperation,
  type Operation,
  type PageKind,
  type TreeEnvelope,
} from '@vsite/builder-core';

/** Id giả chỉ để hỏi Engine "thêm được không" mà chưa sinh id thật (khớp NODE_ID_PATTERN, không trùng id nào). */
export const PROBE_NODE_ID = 'c_probe000';

/** Thứ đang được kéo: một kiểu khối từ palette, hoặc một node đã có trên trang. */
export type DragSource =
  | { kind: 'palette'; type: string; variant: string }
  | { kind: 'node'; nodeId: string };

/** Một vùng thả = một vị trí trong danh sách con của `parentId` (`index` tính TRƯỚC khi gỡ node đang kéo). */
export interface DropSlot {
  parentId: string;
  index: number;
}

export type DropEvaluation =
  | { status: 'noop' }
  | { status: 'ok'; op: Operation }
  | { status: 'invalid'; message: string };

/**
 * Thả `source` vào `slot` thì sẽ ra op gì, và Engine có chấp nhận không — **gọi trước khi thả** để vùng thả báo
 * "không thả được" kèm lý do, và gọi lại lúc thả để ra op thật. Thuần, không đụng store.
 *
 * - `noop`: thả node ngay trước/sau chính nó (không đổi gì).
 * - `toIndex` của `move` tính SAU khi gỡ node khỏi chỗ cũ (quy ước của op), nên cùng cha mà thả phía sau thì trừ 1.
 */
export function evaluateDrop(
  source: DragSource,
  slot: DropSlot,
  ctx: { tree: TreeEnvelope; pageKind: PageKind },
  newNodeId: string = PROBE_NODE_ID,
): DropEvaluation {
  let op: Operation;

  if (source.kind === 'palette') {
    op = {
      type: 'add',
      parentId: slot.parentId,
      index: slot.index,
      node: { id: newNodeId, type: source.type, variant: source.variant },
    };
  } else {
    const found = findNode(ctx.tree, source.nodeId);
    if (!found) return { status: 'invalid', message: fail(OperationErrorCode.TargetNotFound).message };

    let toIndex = slot.index;
    if (found.parentId === slot.parentId) {
      if (slot.index === found.index || slot.index === found.index + 1) return { status: 'noop' };
      if (slot.index > found.index) toIndex = slot.index - 1;
    }
    op = { type: 'move', nodeId: source.nodeId, toParentId: slot.parentId, toIndex };
  }

  const checked = validateOperation(op, ctx);
  return checked.ok ? { status: 'ok', op } : { status: 'invalid', message: checked.message };
}

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null;

/** Dữ liệu `data.current` của dnd-kit là `unknown` về bản chất — kiểm hình dạng thay vì cast mù. */
export function isDragSource(value: unknown): value is DragSource {
  if (!isRecord(value)) return false;
  if (value.kind === 'palette') return typeof value.type === 'string' && typeof value.variant === 'string';
  if (value.kind === 'node') return typeof value.nodeId === 'string';
  return false;
}

export function isDropSlot(value: unknown): value is DropSlot {
  return isRecord(value) && typeof value.parentId === 'string' && Number.isInteger(value.index);
}
