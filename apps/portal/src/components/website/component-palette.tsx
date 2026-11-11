import { useDraggable } from '@dnd-kit/core';
import { useStore } from 'zustand';
import { componentManifests } from '@vsite/builder-components';
import {
  PageKind,
  collectIds,
  createNode,
  validateOperation,
  type EditorStore,
  type TreeEnvelope,
} from '@vsite/builder-core';
import { resolveAddTarget } from '../../lib/add-target';
import { PROBE_NODE_ID, type DragSource } from '../../lib/drop-operation';

/**
 * Bảng khối (Flow 4.3): liệt kê theo manifest, lọc theo loại trang. Mỗi kiểu hiển thị là một nút "Thêm" — kiểm bằng cùng
 * `validateOperation` mà `dispatch` dùng, nên nút tắt kèm lý do khớp đúng lý do Engine sẽ từ chối. Ẩn hẳn ở trang System.
 */
export function ComponentPalette({ store }: { store: EditorStore }) {
  const tree = useStore(store, (s) => s.tree);
  const selectedId = useStore(store, (s) => s.selectedId);
  const pageKind = useStore(store, (s) => s.pageKind);

  if (!tree) return null;
  if (pageKind === PageKind.System) {
    return <p className="text-xs text-muted-foreground">Trang hệ thống có cấu trúc cố định — không thêm khối.</p>;
  }

  const target = resolveAddTarget(tree, selectedId);

  return (
    <section aria-label="Thêm khối" className="space-y-4">
      <h2 className="text-sm font-semibold text-foreground">Thêm khối</h2>
      {componentManifests
        .filter((manifest) => (manifest.allowedInPageKinds as readonly string[]).includes(pageKind))
        .map((manifest) => (
          <div key={manifest.type} className="space-y-1.5">
            <h3 className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{manifest.label}</h3>
            <ul className="space-y-1">
              {manifest.variants.map((variant) => {
                const check = validateOperation(
                  {
                    type: 'add',
                    parentId: target.parentId,
                    index: target.index,
                    node: { id: PROBE_NODE_ID, type: manifest.type, variant: variant.key },
                  },
                  { tree, pageKind },
                );
                return (
                  <li key={variant.key}>
                    <PaletteButton
                      label={`Thêm ${manifest.label} — ${variant.label}`}
                      source={{ kind: 'palette', type: manifest.type, variant: variant.key }}
                      enabled={check.ok}
                      onAdd={() => {
                        addNode(store, tree, manifest.type, variant.key);
                      }}
                    >
                      {variant.label}
                    </PaletteButton>
                    {!check.ok && <p className="mt-0.5 px-1 text-xs text-muted-foreground">{check.message}</p>}
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
    </section>
  );
}

function addNode(store: EditorStore, tree: TreeEnvelope, type: string, variant: string) {
  // Đọc lại trạng thái lúc bấm (không dùng `tree`/vị trí đã render) — tránh dùng bản cũ nếu có thay đổi xen giữa.
  const state = store.getState();
  const current = state.tree ?? tree;
  const target = resolveAddTarget(current, state.selectedId);
  const node = createNode(type, variant, collectIds(current));
  const result = state.dispatch({ type: 'add', parentId: target.parentId, index: target.index, node });
  if (result.ok) store.getState().select(node.id);
}

/**
 * Nút "Thêm" cũng là nguồn kéo: bấm = thêm vào vị trí gợi ý (đường bàn phím/a11y), kéo ≥ 8px = thả vào chỗ chọn
 * trên canvas. Tắt khi Engine từ chối ở vị trí gợi ý; nút tắt không phát sự kiện chuột nên cũng không kéo được —
 * chấp nhận được vì các luật chặn hiện có (maxPerPage, loại trang) không phụ thuộc vị trí; lý do hiện ngay dưới nút.
 */
function PaletteButton({
  label,
  source,
  enabled,
  onAdd,
  children,
}: {
  label: string;
  source: DragSource;
  enabled: boolean;
  onAdd: () => void;
  children: string;
}) {
  const { setNodeRef, listeners } = useDraggable({
    id: `palette:${source.kind === 'palette' ? `${source.type}/${source.variant}` : ''}`,
    data: source,
  });

  return (
    <button
      ref={setNodeRef}
      {...listeners}
      type="button"
      aria-label={label}
      disabled={!enabled}
      className="w-full rounded border border-border px-2 py-1.5 text-left text-sm hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-transparent"
      onClick={onAdd}
    >
      {children}
    </button>
  );
}
