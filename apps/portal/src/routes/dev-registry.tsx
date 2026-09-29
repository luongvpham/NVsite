import { useEffect, useMemo, useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import type { ComponentNode, ImagePickerValue, MediaRef } from '@vsite/builder-components';
import { Inspector, mediaUrl, propertyPanel, setAtPath } from '@vsite/builder-components';
import type { PropertyPanelField } from '@vsite/builder-components';
import { RenderContextProvider, RenderTree } from '@vsite/builder-renderer';
import { useGetShopsShopIdMediaAssets, type MediaAssetDto } from '@vsite/api-sdk';
import { UploadSlotDialog } from '../components/media/upload-slot-dialog';
import { MediaLibraryPicker } from '../components/media/media-library-picker';

/**
 * `/_dev/registry` (07 §10) — đặt tại `/dev-registry` vì TanStack Router dùng prefix `_` cho
 * pathless layout route (không phải segment literal), xung đột với tên gợi ý trong tài liệu.
 *
 * Ba cột: trái = chọn fixture + JSON thô + Shop ID | giữa = builder-renderer render live |
 * phải = Inspector sinh từ property-panel.ts cho node đang chọn.
 *
 * KHÔNG save, KHÔNG API cho tree (Operations Engine/PageDraft là Bước 5), KHÔNG undo. Sửa trong
 * Inspector → tree trong React state đổi → render lại. Reload tree là mất — cố ý (§10).
 *
 * F5 (MEDIA-001): field ảnh (`kind: 'image'`) trong Inspector giờ dùng dialog/picker thật
 * (`UploadSlotDialog`/`MediaLibraryPicker`, F2/F3) qua `renderMediaPicker` thay vì STUB nhập
 * imageId tay. Cần một Shop ID thật (đã đăng nhập ở tab/route khác trong cùng phiên) để gọi API —
 * nhập ở cột trái. `mediaMap` điền trực tiếp từ response upload/clone, và tự tra bù bằng
 * `GET /shops/{shopId}/media/assets?ids=` cho các `imageId` có trong tree nhưng chưa có trong map
 * (vd. sau khi tải lại trang hoặc dán JSON có sẵn imageId). Component này CHẾT sau Bước 5, xoá khi đó.
 */
export const Route = createFileRoute('/dev-registry')({
  component: DevRegistry,
});

const heroFixture: ComponentNode = {
  type: 'Hero',
  variant: 'Hero01',
  id: 'c_hero',
  props: {
    title: 'Demo Hero',
    subtitle: 'Sửa ở Inspector bên phải để xem đổi ngay',
    image: { imageId: 'media_demo' },
    align: 'center',
    overlayOpacity: 35,
  },
};

const sectionFixture: ComponentNode = {
  type: 'Section',
  variant: 'Section01',
  id: 'c_root',
  props: { background: 'background', paddingY: 'lg', maxWidth: 'xl' },
  children: [
    {
      type: 'Hero',
      variant: 'Hero01',
      id: 'c_hero',
      props: { title: 'Chào mừng', image: { imageId: 'media_demo' }, align: 'center' },
    },
    {
      type: 'RichText',
      variant: 'RichText01',
      id: 'c_richtext',
      props: { content: '<p>Nội dung <strong>demo</strong> — sửa ở Inspector.</p>', align: 'left' },
    },
  ],
};

const DEFAULT_FIXTURE_NAME = 'Hero (đơn)';

const FIXTURES: Record<string, ComponentNode> = {
  [DEFAULT_FIXTURE_NAME]: heroFixture,
  'Section + Hero + RichText': sectionFixture,
};

function collectNodes(node: ComponentNode, acc: ComponentNode[] = []): ComponentNode[] {
  acc.push(node);
  for (const child of node.children ?? []) {
    collectNodes(child, acc);
  }
  return acc;
}

function updateNodeInTree(tree: ComponentNode, targetId: string, path: Array<string | number>, value: unknown): ComponentNode {
  if (tree.id === targetId) {
    const updatedProps = setAtPath(tree.props as Record<string, unknown>, path, value);
    return { ...tree, props: updatedProps };
  }
  if (!tree.children) return tree;
  return { ...tree, children: tree.children.map((child) => updateNodeInTree(child, targetId, path, value)) };
}

/** Đệ quy tìm mọi `imageId` trong props (top-level như Hero.image, hoặc lồng trong list như
 * Gallery.items[].image) — không cần biết trước shape theo manifest, chỉ cần nhận ra field
 * `{ imageId: string }` bất kỳ đâu trong cây props (F5). */
function collectImageIds(node: ComponentNode, acc: Set<string> = new Set()): Set<string> {
  walkForImageIds(node.props, acc);
  for (const child of node.children ?? []) {
    collectImageIds(child, acc);
  }
  return acc;
}

function walkForImageIds(value: unknown, acc: Set<string>): void {
  if (Array.isArray(value)) {
    for (const item of value) walkForImageIds(item, acc);
    return;
  }
  if (value && typeof value === 'object') {
    const obj = value as Record<string, unknown>;
    if (typeof obj.imageId === 'string' && obj.imageId.length > 0) {
      acc.add(obj.imageId);
    }
    for (const v of Object.values(obj)) walkForImageIds(v, acc);
  }
}

/**
 * Control ảnh thật cho Inspector (F5) — cắm vào `Inspector.renderMediaPicker`. Hai chế độ theo
 * acceptance "08 §9": upload trực tiếp (UploadSlotDialog, F2) hoặc chọn từ thư viện + clone
 * (MediaLibraryPicker, F3, luôn tạo bản clone mới — #71). Cả hai trả về `MediaAssetDto` dùng ngay
 * để điền `mediaMap` (không cần round-trip `assets?ids=` cho ảnh vừa chọn trong phiên này).
 */
function ImagePickerControl({
  shopId,
  preset,
  value,
  onChange,
  mediaMap,
  onAssetResolved,
}: {
  shopId: string;
  preset: string;
  value: ImagePickerValue;
  onChange: (value: ImagePickerValue) => void;
  mediaMap: Record<string, MediaRef>;
  onAssetResolved: (asset: MediaAssetDto) => void;
}) {
  const [mode, setMode] = useState<'closed' | 'upload' | 'library'>('closed');
  const hasShopId = shopId.trim().length > 0;
  const currentRef = value.imageId ? mediaMap[value.imageId] : undefined;

  return (
    <div className="space-y-2">
      {value.imageId ? (
        currentRef ? (
          <img
            src={mediaUrl(currentRef.storageKey)}
            alt={value.alt ?? ''}
            className="max-h-32 rounded border border-border object-contain"
          />
        ) : (
          <p className="text-xs text-muted-foreground">imageId: {value.imageId} (chưa tra được storageKey)</p>
        )
      ) : (
        <p className="text-xs text-muted-foreground">Chưa chọn ảnh.</p>
      )}

      <div className="flex gap-2">
        <button
          type="button"
          className="rounded border border-border px-2 py-1 text-xs disabled:opacity-50"
          disabled={!hasShopId}
          onClick={() => {
            setMode('upload');
          }}
        >
          Tải ảnh lên
        </button>
        <button
          type="button"
          className="rounded border border-border px-2 py-1 text-xs disabled:opacity-50"
          disabled={!hasShopId}
          onClick={() => {
            setMode('library');
          }}
        >
          Chọn từ thư viện
        </button>
      </div>
      {!hasShopId && <p className="text-xs text-destructive">Nhập Shop ID ở cột trái để dùng dialog/picker thật.</p>}

      <input
        className="w-full rounded border border-border px-2 py-1 text-xs"
        placeholder="Alt text (tuỳ chọn)"
        value={value.alt ?? ''}
        onChange={(e) => {
          onChange({ ...value, alt: e.target.value });
        }}
      />

      {mode === 'upload' && (
        <UploadSlotDialog
          shopId={shopId}
          preset={preset}
          open
          onClose={() => {
            setMode('closed');
          }}
          onUploaded={(asset) => {
            onAssetResolved(asset);
            onChange({ ...value, imageId: asset.id });
            setMode('closed');
          }}
        />
      )}
      {mode === 'library' && (
        <MediaLibraryPicker
          shopId={shopId}
          preset={preset}
          open
          onClose={() => {
            setMode('closed');
          }}
          onSelect={(asset) => {
            onAssetResolved(asset);
            onChange({ ...value, imageId: asset.id });
            setMode('closed');
          }}
        />
      )}
    </div>
  );
}

function DevRegistry() {
  const fixtureNames = Object.keys(FIXTURES);
  const [fixtureName, setFixtureName] = useState(DEFAULT_FIXTURE_NAME);
  const [tree, setTree] = useState<ComponentNode>(heroFixture);
  const [rawJson, setRawJson] = useState(() => JSON.stringify(heroFixture, null, 2));
  const [selectedId, setSelectedId] = useState(heroFixture.id);
  const [jsonError, setJsonError] = useState<string | null>(null);
  const [shopId, setShopId] = useState('');
  const [mediaMap, setMediaMap] = useState<Record<string, MediaRef>>({});
  const [requestedIds, setRequestedIds] = useState<Set<string>>(new Set());

  const nodes = useMemo(() => collectNodes(tree), [tree]);
  const selectedNode = nodes.find((n) => n.id === selectedId) ?? tree;
  const fields: PropertyPanelField[] = (propertyPanel as Record<string, PropertyPanelField[]>)[selectedNode.type] ?? [];

  // Điền mediaMap khi tải lại trang/đổi fixture/dán JSON: imageId có trong tree nhưng chưa có
  // trong map (không phải id vừa upload/clone, cái đó đã điền thẳng từ response — xem
  // `addToMediaMap`, gọi từ `onAssetResolved` của `ImagePickerControl`) thì tra bằng
  // GET /shops/{shopId}/media/assets?ids= (brief F5).
  const missingIds = useMemo(() => {
    const all = Array.from(collectImageIds(tree));
    return all.filter((id) => !(id in mediaMap) && !requestedIds.has(id));
  }, [tree, mediaMap, requestedIds]);

  const assetsQuery = useGetShopsShopIdMediaAssets(
    shopId,
    { ids: missingIds },
    { query: { enabled: shopId.trim().length > 0 && missingIds.length > 0 } },
  );

  useEffect(() => {
    if (!assetsQuery.isSuccess) return;
    const fetchedIds = missingIds;
    setMediaMap((prev) => {
      const next = { ...prev };
      for (const asset of assetsQuery.data) {
        next[asset.id] = { storageKey: asset.storageKey, preset: asset.preset };
      }
      return next;
    });
    setRequestedIds((prev) => {
      const next = new Set(prev);
      for (const id of fetchedIds) next.add(id);
      return next;
    });
  }, [assetsQuery.isSuccess, assetsQuery.data]);

  function addToMediaMap(asset: MediaAssetDto) {
    setMediaMap((prev) => ({ ...prev, [asset.id]: { storageKey: asset.storageKey, preset: asset.preset } }));
  }

  function handleShopIdChange(next: string) {
    setShopId(next);
    // Đổi Shop ID = ngữ cảnh media khác hẳn — không giữ lại map/ids đã tra hụt của shop cũ.
    setMediaMap({});
    setRequestedIds(new Set());
  }

  const renderMediaPicker = ({
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
      onAssetResolved={addToMediaMap}
    />
  );

  function loadFixture(name: string) {
    const fixture = FIXTURES[name];
    if (!fixture) return;
    setFixtureName(name);
    setTree(fixture);
    setRawJson(JSON.stringify(fixture, null, 2));
    setSelectedId(fixture.id);
    setJsonError(null);
  }

  function applyRawJson() {
    try {
      const parsed = JSON.parse(rawJson) as ComponentNode;
      setTree(parsed);
      setJsonError(null);
    } catch (error) {
      setJsonError(error instanceof Error ? error.message : String(error));
    }
  }

  function handleInspectorChange(path: Array<string | number>, value: unknown) {
    const updated = updateNodeInTree(tree, selectedId, path, value);
    setTree(updated);
    setRawJson(JSON.stringify(updated, null, 2));
  }

  return (
    <div className="grid h-screen grid-cols-3 gap-4 p-4">
      <div className="space-y-3 overflow-y-auto">
        <div>
          <label className="block text-sm font-medium" htmlFor="dev-registry-shop-id">
            Shop ID (để dùng dialog/picker thật — F5)
          </label>
          <input
            id="dev-registry-shop-id"
            className="mt-1 w-full rounded border border-border px-2 py-1 text-sm"
            placeholder="dán shopId đã đăng nhập"
            value={shopId}
            onChange={(e) => {
              handleShopIdChange(e.target.value);
            }}
          />
        </div>

        <h2 className="font-semibold">Fixture</h2>
        <select
          className="w-full rounded border border-border px-2 py-1"
          value={fixtureName}
          onChange={(e) => {
            loadFixture(e.target.value);
          }}
        >
          {fixtureNames.map((name) => (
            <option key={name} value={name}>
              {name}
            </option>
          ))}
        </select>

        <textarea
          className="h-96 w-full rounded border border-border p-2 font-mono text-xs"
          value={rawJson}
          onChange={(e) => {
            setRawJson(e.target.value);
          }}
        />
        <button type="button" className="rounded border border-border px-3 py-1 text-sm" onClick={applyRawJson}>
          Áp dụng JSON
        </button>
        {jsonError ? <p className="text-sm text-destructive">JSON lỗi: {jsonError}</p> : null}
      </div>

      <div className="overflow-y-auto rounded border border-border">
        <RenderContextProvider value={{ mediaMap }}>
          <RenderTree tree={tree} />
        </RenderContextProvider>
      </div>

      <div className="space-y-3 overflow-y-auto">
        <h2 className="font-semibold">Inspector</h2>
        <select
          className="w-full rounded border border-border px-2 py-1"
          value={selectedId}
          onChange={(e) => {
            setSelectedId(e.target.value);
          }}
        >
          {nodes.map((n) => (
            <option key={n.id} value={n.id}>
              {n.id} ({n.type}/{n.variant})
            </option>
          ))}
        </select>
        {fields.length > 0 ? (
          <Inspector
            fields={fields}
            values={selectedNode.props as Record<string, unknown>}
            onChange={handleInspectorChange}
            renderMediaPicker={renderMediaPicker}
          />
        ) : (
          <p className="text-sm text-muted-foreground">Không có field nào cho type này.</p>
        )}
      </div>
    </div>
  );
}
