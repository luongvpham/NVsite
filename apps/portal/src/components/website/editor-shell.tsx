import { useEffect, useMemo, useState } from 'react';
import { useStore } from 'zustand';
import { Button } from '@vsite/ui';
import { getErrorCode } from '@vsite/shared';
import { useGetPageDraft, type PageDraftDto } from '@vsite/api-sdk';
import {
  Inspector,
  opRules,
  propertyPanel,
  treeEnvelopeSchema,
  type ImagePickerValue,
  type PropertyPanelField,
} from '@vsite/builder-components';
import {
  PageKind,
  createEditorStore,
  findNode,
  type EditorStore,
  type PropPathSegment,
  type TreeEnvelope,
} from '@vsite/builder-core';
import { getErrorMessage } from '../../lib/error-messages';
import { useMediaMap } from '../../hooks/use-media-map';
import { ImagePickerControl } from '../media/image-picker-control';
import { EditorCanvas } from './editor-canvas';

/**
 * Khung editor (Flow 4). Server state ở TanStack Query, cây đang sửa ở store (#20):
 * draft chỉ được nạp vào store MỘT lần khi phiên mở (`EditorSession` mount) — refetch sau đó không đè lên bản đang sửa.
 */
export function EditorShell({ shopId, pageId }: { shopId: string; pageId: string }) {
  const draftQuery = useGetPageDraft(shopId, pageId, {
    query: { retry: false, staleTime: Infinity, refetchOnWindowFocus: false, refetchOnReconnect: false },
  });

  const parsed = useMemo(
    () => (draftQuery.data ? treeEnvelopeSchema.safeParse(draftQuery.data.tree) : undefined),
    [draftQuery.data],
  );

  if (draftQuery.isPending) return <p className="p-6 text-muted-foreground">Đang tải trang...</p>;

  if (draftQuery.isError) {
    return (
      <div className="space-y-3 p-6">
        <p role="alert" className="text-destructive">
          {getErrorMessage(getErrorCode(draftQuery.error))}
        </p>
        <Button type="button" variant="outline" size="sm" onClick={() => void draftQuery.refetch()}>
          Thử lại
        </Button>
      </div>
    );
  }

  if (!parsed?.success) {
    // Không cho sửa: lưu đè một cây ta không đọc được sẽ phá bản nháp của chủ shop.
    return (
      <p role="alert" className="p-6 text-destructive">
        Dữ liệu trang không đọc được. Vui lòng liên hệ hỗ trợ.
      </p>
    );
  }

  return <EditorSession key={pageId} shopId={shopId} draft={draftQuery.data} tree={parsed.data} />;
}

function EditorSession({ shopId, draft, tree }: { shopId: string; draft: PageDraftDto; tree: TreeEnvelope }) {
  // Nạp một lần lúc mở phiên — "mở bản nháp để sửa", không phải mirror Query → Zustand.
  const [store] = useState<EditorStore>(() => {
    const created = createEditorStore();
    created.getState().loadDraft(tree, draft.page.kind === 'System' ? PageKind.System : PageKind.Composable);
    return created;
  });

  const workingTree = useStore(store, (s) => s.tree);
  const { mediaMap, addAsset } = useMediaMap(shopId, workingTree);

  useUndoRedoShortcuts(store);

  return (
    <div className="flex h-[calc(100vh-4rem)] flex-col">
      <Toolbar store={store} />
      <div className="grid min-h-0 flex-1 grid-cols-[1fr_20rem]">
        <div className="overflow-y-auto border-r border-border">
          <EditorCanvas store={store} mediaMap={mediaMap} />
        </div>
        <aside aria-label="Thuộc tính khối" className="overflow-y-auto p-4">
          <InspectorPanel store={store} shopId={shopId} mediaMap={mediaMap} onAssetResolved={addAsset} />
        </aside>
      </div>
    </div>
  );
}

function Toolbar({ store }: { store: EditorStore }) {
  const canUndo = useStore(store.temporal, (s) => s.pastStates.length > 0);
  const canRedo = useStore(store.temporal, (s) => s.futureStates.length > 0);

  return (
    <div role="toolbar" aria-label="Công cụ soạn trang" className="flex items-center gap-2 border-b border-border px-4 py-2">
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={!canUndo}
        title="Hoàn tác (Ctrl+Z)"
        onClick={() => {
          store.getState().undo();
        }}
      >
        Hoàn tác
      </Button>
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={!canRedo}
        title="Làm lại (Ctrl+Shift+Z)"
        onClick={() => {
          store.getState().redo();
        }}
      >
        Làm lại
      </Button>
    </div>
  );
}

/** Ctrl/Cmd+Z, Ctrl/Cmd+Shift+Z, Ctrl+Y — bỏ qua khi đang gõ trong ô nhập (trình duyệt tự undo chữ ở đó). */
function useUndoRedoShortcuts(store: EditorStore) {
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
      const target = e.target;
      if (
        target instanceof HTMLElement &&
        (target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName))
      ) {
        return;
      }
      const key = e.key.toLowerCase();
      if (key === 'z' && !e.shiftKey) {
        e.preventDefault();
        store.getState().undo();
      } else if ((key === 'z' && e.shiftKey) || key === 'y') {
        e.preventDefault();
        store.getState().redo();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [store]);
}

const propertyPanels = propertyPanel as Record<string, PropertyPanelField[]>;
const placementRules = opRules as unknown as Record<string, { editableInSystemPageProps: readonly string[] }>;

function InspectorPanel({
  store,
  shopId,
  mediaMap,
  onAssetResolved,
}: {
  store: EditorStore;
  shopId: string;
  mediaMap: Parameters<typeof ImagePickerControl>[0]['mediaMap'];
  onAssetResolved: Parameters<typeof ImagePickerControl>[0]['onAssetResolved'];
}) {
  const tree = useStore(store, (s) => s.tree);
  const selectedId = useStore(store, (s) => s.selectedId);
  const pageKind = useStore(store, (s) => s.pageKind);
  const [opError, setOpError] = useState<string | null>(null);

  // Đổi node chọn thì lỗi của lần sửa trước hết liên quan.
  useEffect(() => {
    setOpError(null);
  }, [selectedId]);

  const node = tree && selectedId ? findNode(tree, selectedId)?.node : undefined;
  if (!node) {
    return <p className="text-sm text-muted-foreground">Chọn một khối trên trang để sửa thuộc tính.</p>;
  }

  const fields = propertyPanels[node.type] ?? [];
  const editableOnSystem = Object.hasOwn(placementRules, node.type)
    ? (placementRules[node.type]?.editableInSystemPageProps ?? [])
    : [];

  const handleChange = (path: PropPathSegment[], value: unknown) => {
    const result = store.getState().dispatch({ type: 'update', nodeId: node.id, path, value });
    setOpError(result.ok ? null : result.message);
  };

  return (
    <div className="space-y-3">
      <h2 className="text-sm font-semibold text-foreground">
        {node.type} <span className="font-normal text-muted-foreground">({node.variant})</span>
      </h2>
      {pageKind === PageKind.System && (
        <p className="text-xs text-muted-foreground">Trang hệ thống: chỉ sửa được phần nội dung.</p>
      )}
      {opError && (
        <p role="alert" className="text-sm text-destructive">
          {opError}
        </p>
      )}
      {fields.length === 0 ? (
        <p className="text-sm text-muted-foreground">Khối này không có thuộc tính nào để sửa.</p>
      ) : (
        <Inspector
          key={node.id}
          fields={fields}
          values={node.props ?? {}}
          onChange={handleChange}
          isFieldDisabled={(field) => pageKind === PageKind.System && !editableOnSystem.includes(field.name)}
          renderMediaPicker={({
            field,
            value,
            onChange,
          }: {
            field: PropertyPanelField;
            value: ImagePickerValue;
            onChange: (value: ImagePickerValue) => void;
          }) => (
            <ImagePickerControl
              shopId={shopId}
              preset={field.preset ?? ''}
              value={value}
              onChange={onChange}
              mediaMap={mediaMap}
              onAssetResolved={onAssetResolved}
            />
          )}
        />
      )}
    </div>
  );
}
