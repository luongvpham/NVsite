import {
  Children,
  cloneElement,
  isValidElement,
  useCallback,
  useMemo,
  type KeyboardEvent,
  type MouseEvent,
  type PropsWithChildren,
  type ReactElement,
  type ReactNode,
} from 'react';
import { useDraggable } from '@dnd-kit/core';
import { useStore } from 'zustand';
import { opRules, type MediaRef } from '@vsite/builder-components';
import { RenderContextProvider, RenderTree, type DecorateNode, type PageRoot } from '@vsite/builder-renderer';
import { PageKind, ROOT_ID, type EditorStore, type TreeEnvelope, type TreeNode } from '@vsite/builder-core';
import { themeStyle } from '@vsite/theme-engine';
import type { DragSource } from '../../lib/drop-operation';
import { DropSlotZone, useIsDragging } from './editor-dnd';

interface NodePosition {
  parentId: string;
  /** Vị trí trong danh sách con của cha. */
  index: number;
  isContainer: boolean;
  childCount: number;
}

const containerTypes = new Set(
  Object.entries(opRules as unknown as Record<string, { acceptsChildren: boolean }>)
    .filter(([, rule]) => rule.acceptsChildren)
    .map(([type]) => type),
);

/** Một lần duyệt cây → vị trí của mọi node (tránh `findNode` O(n) cho từng node lúc render). */
function indexPositions(tree: TreeEnvelope): Map<string, NodePosition> {
  const positions = new Map<string, NodePosition>();
  const visit = (children: readonly TreeNode[], parentId: string) => {
    children.forEach((node, index) => {
      positions.set(node.id, {
        parentId,
        index,
        isContainer: containerTypes.has(node.type),
        childCount: node.children?.length ?? 0,
      });
      if (node.children) visit(node.children, node.id);
    });
  };
  visit(tree.children, ROOT_ID);
  return positions;
}

/**
 * Canvas = `builder-renderer` thật + `decorateNode` bọc mỗi node bằng một khung chọn/kéo được. Theme đang sửa
 * (`themeDraft`) áp qua biến CSS ở khung ngoài nên đổi là thấy ngay (Flow 6). Chưa nạp theme → mặc định.
 */
export function EditorCanvas({
  store,
  mediaMap,
}: {
  store: EditorStore;
  mediaMap: Readonly<Record<string, MediaRef>>;
}) {
  const tree = useStore(store, (s) => s.tree);
  const selectedId = useStore(store, (s) => s.selectedId);
  const themeDraft = useStore(store, (s) => s.themeDraft);
  const pageKind = useStore(store, (s) => s.pageKind);
  const dragging = useIsDragging();

  const positions = useMemo(() => (tree ? indexPositions(tree) : new Map<string, NodePosition>()), [tree]);
  const structureEditable = pageKind !== PageKind.System;

  const decorateNode = useCallback<DecorateNode>(
    (node, rendered) => {
      const position = positions.get(node.id);
      if (!position) return rendered;
      return (
        <CanvasNode
          store={store}
          nodeId={node.id}
          nodeType={node.type}
          position={position}
          selected={node.id === selectedId}
          draggable={structureEditable}
          showSlots={structureEditable && dragging}
        >
          {rendered}
        </CanvasNode>
      );
    },
    [positions, selectedId, store, structureEditable, dragging],
  );

  if (!tree) return null;

  const rootEndSlot = structureEditable && dragging && (
    <DropSlotZone store={store} slot={{ parentId: ROOT_ID, index: tree.children.length }} />
  );

  return (
    <div
      data-testid="editor-canvas"
      style={themeStyle(themeDraft)}
      className="min-h-full"
      onClick={() => {
        store.getState().select(null);
      }}
    >
      {tree.children.length === 0 ? (
        <p className="p-8 text-center text-sm" style={{ color: 'var(--vs-color-text-muted, #64748b)' }}>
          Trang trống — thêm khối từ bảng bên trái
        </p>
      ) : (
        <RenderContextProvider value={{ mediaMap }}>
          <RenderTree tree={tree as unknown as PageRoot} decorateNode={decorateNode} />
        </RenderContextProvider>
      )}
      {rootEndSlot}
    </div>
  );
}

function CanvasNode({
  store,
  nodeId,
  nodeType,
  position,
  selected,
  draggable,
  showSlots,
  children,
}: PropsWithChildren<{
  store: EditorStore;
  nodeId: string;
  nodeType: string;
  position: NodePosition;
  selected: boolean;
  draggable: boolean;
  showSlots: boolean;
}>) {
  const source: DragSource = { kind: 'node', nodeId };
  const { setNodeRef, listeners, isDragging } = useDraggable({ id: `node:${nodeId}`, data: source, disabled: !draggable });

  const select = () => {
    store.getState().select(nodeId);
  };

  // Khối chứa: thêm vùng thả "cuối danh sách con" NGAY TRONG khối (renderer không biết gì về editor, nên chèn qua
  // children của phần tử đã render). Khối rỗng cũng nhờ đó có chỗ để thả vào.
  let content: ReactNode = children;
  if (showSlots && position.isContainer && isValidElement(children)) {
    const element = children as ReactElement<PropsWithChildren>;
    content = cloneElement(
      element,
      undefined,
      ...Children.toArray(element.props.children),
      <DropSlotZone key="__end" store={store} slot={{ parentId: nodeId, index: position.childCount }} />,
    );
  }

  return (
    <>
      {showSlots && <DropSlotZone store={store} slot={{ parentId: position.parentId, index: position.index }} />}
      <div
        ref={setNodeRef}
        {...listeners}
        data-node-id={nodeId}
        data-selected={selected ? 'true' : undefined}
        aria-label={`Khối ${nodeType}`}
        tabIndex={0}
        className={`${
          selected
            ? 'cursor-pointer outline-2 -outline-offset-2 outline-primary'
            : 'cursor-pointer hover:outline hover:outline-1 hover:-outline-offset-1 hover:outline-primary/40'
        } ${isDragging ? 'opacity-40' : ''}`}
        onClick={(e: MouseEvent) => {
          // Link/nút bên trong khối chỉ để xem — không được điều hướng khi đang soạn.
          e.preventDefault();
          e.stopPropagation();
          select();
        }}
        onKeyDown={(e: KeyboardEvent) => {
          if (e.target !== e.currentTarget) return;
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            e.stopPropagation();
            select();
          }
        }}
      >
        {content}
      </div>
    </>
  );
}
