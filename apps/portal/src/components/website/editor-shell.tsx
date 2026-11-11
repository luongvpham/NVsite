import { useEffect, useMemo, useState } from 'react';
import { useBlocker } from '@tanstack/react-router';
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
import { useAutosave } from '../../hooks/use-autosave';
import type { AutosaveController, AutosaveState } from '../../lib/autosave/autosave-controller';
import type { NodeErrors } from '../../lib/autosave/map-tree-errors';
import { ImagePickerControl } from '../media/image-picker-control';
import { siblingMove } from '../../lib/sibling-move';
import { ComponentPalette } from './component-palette';
import { EditorCanvas } from './editor-canvas';
import { EditorDnd } from './editor-dnd';
import { useThemeDraft } from '../../hooks/use-theme-draft';
import { AutosaveStatus, ConflictDialog, LeavePrompt, LeaveThemeTabPrompt, ValidationBanner } from './autosave-ui';
import { ThemePanel } from './theme-panel';

/**
 * Khung editor (Flow 4). Server state ở TanStack Query, cây đang sửa ở store (#20):
 * draft chỉ được nạp vào store MỘT lần khi phiên mở (`EditorSession` mount) — refetch sau đó không đè lên bản đang sửa.
 */
export function EditorShell({ shopId, pageId }: { shopId: string; pageId: string }) {
  const draftQuery = useGetPageDraft(shopId, pageId, {
    query: {
      retry: false,
      staleTime: Infinity,
      refetchOnWindowFocus: false,
      refetchOnReconnect: false,
      // Bản trong cache chỉ đúng lúc mở phiên (autosave làm nó cũ dần). Bỏ ngay khi rời editor để lần mở sau
      // GET lại — không thì mở lại với cây + version cũ → 409 giả hoặc mất thay đổi đã lưu.
      gcTime: 0,
    },
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

  return <EditorSession key={pageId} shopId={shopId} pageId={pageId} draft={draftQuery.data} tree={parsed.data} />;
}

function EditorSession({
  shopId,
  pageId,
  draft,
  tree,
}: {
  shopId: string;
  pageId: string;
  draft: PageDraftDto;
  tree: TreeEnvelope;
}) {
  // Nạp một lần lúc mở phiên — "mở bản nháp để sửa", không phải mirror Query → Zustand.
  const [store] = useState<EditorStore>(() => {
    const created = createEditorStore();
    created
      .getState()
      .loadDraft(tree, draft.page.kind === 'System' ? PageKind.System : PageKind.Composable);
    return created;
  });

  const workingTree = useStore(store, (s) => s.tree);
  const { mediaMap, addAsset } = useMediaMap(shopId, workingTree);

  useEditorShortcuts(store);

  const { controller, state: autosave } = useAutosave({
    store,
    shopId,
    pageId,
    initialVersion: draft.version,
  });

  const theme = useThemeDraft(store, shopId);
  const [tab, setTab] = useState<SideTab>('props');
  const [leaveThemeTo, setLeaveThemeTo] = useState<SideTab | null>(null);

  const requestTab = (next: SideTab) => {
    if (next === tab) return;
    // Rời tab Giao diện khi còn bản chưa lưu → nhắc (Flow 6). Bản sửa vẫn nằm ở canvas, nhưng chưa lên server.
    if (tab === 'theme' && theme.dirty) setLeaveThemeTo(next);
    else setTab(next);
  };

  // Rời trang khi còn thay đổi chưa lên server: cảnh báo (đóng tab/reload qua beforeunload, điều hướng trong app qua dialog).
  // Theme không đi qua autosave nên tính riêng.
  const hasUnsaved = () => controller.hasUnsavedChanges || theme.dirty;
  const blocker = useBlocker({
    shouldBlockFn: hasUnsaved,
    enableBeforeUnload: hasUnsaved,
    withResolver: true,
  });

  const nodeErrors = autosave.invalid?.mapped.byNode;
  const invalidNodeIds = useMemo(() => new Set(nodeErrors?.keys()), [nodeErrors]);

  return (
    <div className="flex h-[calc(100vh-4rem)] flex-col">
      <Toolbar store={store} autosave={autosave} controller={controller} />
      <ValidationBanner state={autosave} />
      <ConflictDialog state={autosave} controller={controller} />
      {blocker.status === 'blocked' && <LeavePrompt onStay={blocker.reset} onLeave={blocker.proceed} />}
      {leaveThemeTo && (
        <LeaveThemeTabPrompt
          onStay={() => { setLeaveThemeTo(null); }}
          onLeave={() => { setTab(leaveThemeTo); setLeaveThemeTo(null); }}
        />
      )}
      <EditorDnd store={store}>
        <div className="grid min-h-0 flex-1 grid-cols-[14rem_1fr_20rem]">
          <nav aria-label="Bảng khối" className="overflow-y-auto border-r border-border p-3">
            <ComponentPalette store={store} />
          </nav>
          <div className="overflow-y-auto border-r border-border">
            <EditorCanvas store={store} mediaMap={mediaMap} invalidNodeIds={invalidNodeIds} />
          </div>
          <aside aria-label="Thuộc tính khối" className="overflow-y-auto p-4">
            <div role="tablist" aria-label="Bảng bên phải" className="mb-3 flex gap-1 border-b border-border">
              <SideTabButton selected={tab === 'props'} onClick={() => { requestTab('props'); }}>
                Thuộc tính
              </SideTabButton>
              <SideTabButton selected={tab === 'theme'} onClick={() => { requestTab('theme'); }}>
                Giao diện{theme.dirty ? ' •' : ''}
              </SideTabButton>
            </div>
            {tab === 'props' ? (
              <InspectorPanel
                store={store}
                shopId={shopId}
                mediaMap={mediaMap}
                onAssetResolved={addAsset}
                nodeErrors={nodeErrors}
              />
            ) : (
              <ThemePanel theme={theme} />
            )}
          </aside>
        </div>
      </EditorDnd>
    </div>
  );
}

type SideTab = 'props' | 'theme';

function SideTabButton({
  selected,
  onClick,
  children,
}: {
  selected: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={selected}
      onClick={onClick}
      className={`-mb-px border-b-2 px-3 py-1.5 text-sm ${
        selected ? 'border-primary font-medium text-foreground' : 'border-transparent text-muted-foreground'
      }`}
    >
      {children}
    </button>
  );
}

function Toolbar({
  store,
  autosave,
  controller,
}: {
  store: EditorStore;
  autosave: AutosaveState;
  controller: AutosaveController;
}) {
  const canUndo = useStore(store.temporal, (s) => s.pastStates.length > 0);
  const canRedo = useStore(store.temporal, (s) => s.futureStates.length > 0);

  return (
    <div
      role="toolbar"
      aria-label="Công cụ soạn trang"
      className="flex items-center gap-2 border-b border-border px-4 py-2"
    >
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
      <AutosaveStatus state={autosave} controller={controller} />
    </div>
  );
}

/** Đang gõ trong ô nhập → phím tắt của editor phải nhường cho trình duyệt (undo chữ, xoá chữ…). */
function isTypingTarget(target: EventTarget | null): boolean {
  return (
    target instanceof HTMLElement &&
    (target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName))
  );
}

/** Ctrl/Cmd+Z, Ctrl/Cmd+Shift+Z, Ctrl+Y (undo/redo) và Delete (xoá khối đang chọn) — bỏ qua khi đang gõ trong ô nhập. */
function useEditorShortcuts(store: EditorStore) {
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (isTypingTarget(e.target)) return;

      if (e.key === 'Delete' && !e.ctrlKey && !e.metaKey && !e.altKey && !e.shiftKey) {
        const { selectedId, pageKind } = store.getState();
        // Trang System có cấu trúc cố định; Engine cũng từ chối, nhưng không cần nuốt phím khi chắc chắn vô ích.
        if (!selectedId || pageKind === PageKind.System) return;
        e.preventDefault();
        store.getState().dispatch({ type: 'remove', nodeId: selectedId });
        return;
      }

      if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
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
const placementRules = opRules as unknown as Record<
  string,
  { editableInSystemPageProps: readonly string[] }
>;

function InspectorPanel({
  store,
  shopId,
  mediaMap,
  onAssetResolved,
  nodeErrors,
}: {
  store: EditorStore;
  shopId: string;
  mediaMap: Parameters<typeof ImagePickerControl>[0]['mediaMap'];
  onAssetResolved: Parameters<typeof ImagePickerControl>[0]['onAssetResolved'];
  /** Lỗi 422 theo node (R5: có thể trỏ node không còn — lúc đó không có mục ở đây). */
  nodeErrors: ReadonlyMap<string, NodeErrors> | undefined;
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
    return (
      <p className="text-sm text-muted-foreground">Chọn một khối trên trang để sửa thuộc tính.</p>
    );
  }

  const thisNodeErrors = nodeErrors?.get(node.id);
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
      {pageKind === PageKind.System ? (
        <p className="text-xs text-muted-foreground">Trang hệ thống: chỉ sửa được phần nội dung.</p>
      ) : (
        tree && <NodeActions store={store} tree={tree} nodeId={node.id} onError={setOpError} />
      )}
      {opError && (
        <p role="alert" className="text-sm text-destructive">
          {opError}
        </p>
      )}
      {thisNodeErrors && (
        <ul aria-label="Lỗi của khối này" className="list-disc space-y-1 pl-5 text-sm text-destructive">
          {thisNodeErrors.errors.map((error, i) => (
            <li key={`${error.path}:${error.code}:${String(i)}`}>
              {error.message} <span className="text-xs opacity-70">({error.code})</span>
            </li>
          ))}
        </ul>
      )}
      {fields.length === 0 ? (
        <p className="text-sm text-muted-foreground">Khối này không có thuộc tính nào để sửa.</p>
      ) : (
        <Inspector
          key={node.id}
          fields={fields}
          values={node.props ?? {}}
          onChange={handleChange}
          invalidPaths={thisNodeErrors?.propPaths}
          isFieldDisabled={(field) =>
            pageKind === PageKind.System && !editableOnSystem.includes(field.name)
          }
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

/**
 * Lên/Xuống/Xoá cho khối đang chọn — đường bàn phím (a11y) cho cùng các op `move`/`remove` mà kéo-thả dùng.
 * Ẩn ở trang System (cấu trúc cố định).
 */
function NodeActions({
  store,
  tree,
  nodeId,
  onError,
}: {
  store: EditorStore;
  tree: TreeEnvelope;
  nodeId: string;
  onError: (message: string | null) => void;
}) {
  const up = siblingMove(tree, nodeId, 'up');
  const down = siblingMove(tree, nodeId, 'down');

  const run = (op: unknown) => {
    const result = store.getState().dispatch(op);
    onError(result.ok ? null : result.message);
  };

  return (
    <div role="group" aria-label="Thao tác với khối" className="flex flex-wrap gap-2">
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={!up}
        onClick={() => {
          if (up) run({ type: 'move', nodeId, ...up });
        }}
      >
        Lên
      </Button>
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={!down}
        onClick={() => {
          if (down) run({ type: 'move', nodeId, ...down });
        }}
      >
        Xuống
      </Button>
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={() => {
          run({ type: 'remove', nodeId });
        }}
      >
        Xoá khối
      </Button>
    </div>
  );
}
