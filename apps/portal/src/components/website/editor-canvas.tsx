import { useCallback, type KeyboardEvent, type MouseEvent } from 'react';
import { useStore } from 'zustand';
import type { MediaRef } from '@vsite/builder-components';
import { RenderContextProvider, RenderTree, type DecorateNode, type PageRoot } from '@vsite/builder-renderer';
import type { EditorStore } from '@vsite/builder-core';
import { themeStyle } from '@vsite/theme-engine';

/**
 * Canvas = `builder-renderer` thật + `decorateNode` bọc mỗi node bằng một khung chọn được. Theme đang sửa
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

  const decorateNode = useCallback<DecorateNode>(
    (node, rendered) => {
      const selected = node.id === selectedId;
      const select = () => {
        store.getState().select(node.id);
      };
      return (
        <div
          data-node-id={node.id}
          data-selected={selected ? 'true' : undefined}
          aria-label={`Khối ${node.type}`}
          tabIndex={0}
          className={
            selected
              ? 'cursor-pointer outline-2 -outline-offset-2 outline-primary'
              : 'cursor-pointer hover:outline hover:outline-1 hover:-outline-offset-1 hover:outline-primary/40'
          }
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
          {rendered}
        </div>
      );
    },
    [selectedId, store],
  );

  if (!tree) return null;

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
    </div>
  );
}
