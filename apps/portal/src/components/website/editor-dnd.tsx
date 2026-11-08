import { useState, type ReactNode } from 'react';
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  pointerWithin,
  useDndContext,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core';
import { componentManifests } from '@vsite/builder-components';
import { collectIds, findNode, generateNodeId, type EditorStore } from '@vsite/builder-core';
import {
  evaluateDrop,
  isDragSource,
  isDropSlot,
  type DragSource,
  type DropSlot,
} from '../../lib/drop-operation';

/** Mô tả ngắn thứ đang kéo, cho lớp phủ theo con trỏ. */
function describeSource(store: EditorStore, source: DragSource): string {
  if (source.kind === 'palette') {
    const manifest = componentManifests.find((m) => m.type === source.type);
    const variant = manifest?.variants.find((v) => v.key === source.variant);
    return `Thêm ${manifest?.label ?? source.type}${variant ? ` — ${variant.label}` : ''}`;
  }
  const tree = store.getState().tree;
  const type = tree ? findNode(tree, source.nodeId)?.node.type : undefined;
  return `Di chuyển ${type ?? 'khối'}`;
}

/**
 * Bối cảnh kéo-thả của editor. Kéo từ palette → op `add`; kéo node → op `move`. Mọi thay đổi vẫn qua
 * `store.dispatch` (Engine kiểm luật), và vùng thả kiểm bằng cùng `evaluateDrop` TRƯỚC khi thả nên báo được lý do.
 *
 * Chỉ dùng chuột/cảm ứng (PointerSensor, phải kéo ≥ 8px nên bấm để chọn vẫn bình thường). Đường bàn phím là nút
 * Thêm / Lên / Xuống / Xoá — cùng các op này — nên không cần KeyboardSensor (nó tranh Enter/Space của việc chọn khối).
 */
export function EditorDnd({ store, children }: { store: EditorStore; children: ReactNode }) {
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 8 } }));
  const [source, setSource] = useState<DragSource | null>(null);
  const [dropError, setDropError] = useState<string | null>(null);

  const onDragStart = (event: DragStartEvent) => {
    const data: unknown = event.active.data.current;
    setSource(isDragSource(data) ? data : null);
    setDropError(null);
  };

  const onDragEnd = (event: DragEndEvent) => {
    setSource(null);
    const dragged: unknown = event.active.data.current;
    const slot: unknown = event.over?.data.current;
    if (!isDragSource(dragged) || !isDropSlot(slot)) return;

    const { tree, pageKind } = store.getState();
    if (!tree) return;

    // Id thật cấp ngay lúc thả (không dùng id giả của lúc rê chuột).
    const newNodeId = dragged.kind === 'palette' ? generateNodeId(collectIds(tree)) : undefined;
    const evaluation = evaluateDrop(dragged, slot, { tree, pageKind }, newNodeId);

    if (evaluation.status === 'noop') return;
    if (evaluation.status === 'invalid') {
      setDropError(evaluation.message);
      return;
    }
    const result = store.getState().dispatch(evaluation.op);
    if (!result.ok) {
      setDropError(result.message);
      return;
    }
    setDropError(null);
    if (evaluation.op.type === 'add') store.getState().select(evaluation.op.node.id);
  };

  return (
    <DndContext
      sensors={sensors}
      // Chỉ tính vùng thả đang nằm DƯỚI con trỏ — không tự đoán "gần nhất" rồi thả nhầm chỗ.
      collisionDetection={pointerWithin}
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
      onDragCancel={() => {
        setSource(null);
      }}
    >
      {dropError && (
        <p role="alert" className="border-b border-destructive/40 bg-destructive/10 px-4 py-2 text-sm text-destructive">
          Không thả được: {dropError}
        </p>
      )}
      {children}
      <DragOverlay dropAnimation={null}>
        {source ? (
          <div className="rounded border border-border bg-background px-2 py-1 text-sm shadow-md">
            {describeSource(store, source)}
          </div>
        ) : null}
      </DragOverlay>
    </DndContext>
  );
}

/** Đang có thứ được kéo hay không — vùng thả chỉ hiện trong lúc kéo (không chiếm chỗ ở canvas lúc bình thường). */
export function useIsDragging(): boolean {
  return useDndContext().active !== null;
}

/**
 * Một vị trí thả giữa các khối (hoặc cuối một khối chứa). Thanh mỏng nhưng vùng bắt chuột cao hơn nhờ đệm âm,
 * để không đẩy layout của trang. Rê vào: xanh nếu thả được, đỏ kèm lý do nếu Engine sẽ từ chối.
 */
export function DropSlotZone({ store, slot }: { store: EditorStore; slot: DropSlot }) {
  const { active } = useDndContext();
  const { setNodeRef, isOver } = useDroppable({ id: `slot:${slot.parentId}:${slot.index}`, data: slot });

  const dragged: unknown = active?.data.current;
  let invalidMessage: string | null = null;
  let isNoop = false;
  if (isOver && isDragSource(dragged)) {
    const { tree, pageKind } = store.getState();
    if (tree) {
      const evaluation = evaluateDrop(dragged, slot, { tree, pageKind });
      if (evaluation.status === 'invalid') invalidMessage = evaluation.message;
      if (evaluation.status === 'noop') isNoop = true;
    }
  }

  const bar = !isOver
    ? 'bg-primary/15'
    : invalidMessage
      ? 'bg-destructive'
      : isNoop
        ? 'bg-muted-foreground/40'
        : 'bg-primary';

  return (
    <div
      ref={setNodeRef}
      data-drop-slot={`${slot.parentId}:${slot.index}`}
      data-over={isOver ? (invalidMessage ? 'invalid' : 'ok') : undefined}
      className="relative z-10 -my-2 flex h-4 items-center"
    >
      <div className={`h-1 w-full rounded ${bar}`} />
      {invalidMessage && (
        <span className="absolute left-2 top-full z-20 rounded bg-background px-1.5 py-0.5 text-xs text-destructive shadow">
          Không thả được: {invalidMessage}
        </span>
      )}
    </div>
  );
}
