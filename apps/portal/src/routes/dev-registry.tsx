import { useMemo, useState } from 'react';
import { createFileRoute } from '@tanstack/react-router';
import type { ComponentNode } from '@vsite/builder-components';
import { Inspector, propertyPanel, setAtPath } from '@vsite/builder-components';
import type { PropertyPanelField } from '@vsite/builder-components';
import { RenderContextProvider, RenderTree } from '@vsite/builder-renderer';

/**
 * `/_dev/registry` (07 §10) — đặt tại `/dev-registry` vì TanStack Router dùng prefix `_` cho
 * pathless layout route (không phải segment literal), xung đột với tên gợi ý trong tài liệu.
 *
 * Ba cột: trái = chọn fixture + JSON thô | giữa = builder-renderer render live |
 * phải = Inspector sinh từ property-panel.ts cho node đang chọn.
 *
 * KHÔNG save, KHÔNG API, KHÔNG undo. Sửa trong Inspector → tree trong React state đổi →
 * render lại. Reload là mất — cố ý (§10). Component này CHẾT sau Bước 5, xoá khi đó.
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

function DevRegistry() {
  const fixtureNames = Object.keys(FIXTURES);
  const [fixtureName, setFixtureName] = useState(DEFAULT_FIXTURE_NAME);
  const [tree, setTree] = useState<ComponentNode>(heroFixture);
  const [rawJson, setRawJson] = useState(() => JSON.stringify(heroFixture, null, 2));
  const [selectedId, setSelectedId] = useState(heroFixture.id);
  const [jsonError, setJsonError] = useState<string | null>(null);

  const nodes = useMemo(() => collectNodes(tree), [tree]);
  const selectedNode = nodes.find((n) => n.id === selectedId) ?? tree;
  const fields: PropertyPanelField[] = (propertyPanel as Record<string, PropertyPanelField[]>)[selectedNode.type] ?? [];

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
        <RenderContextProvider>
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
          <Inspector fields={fields} values={selectedNode.props as Record<string, unknown>} onChange={handleInspectorChange} />
        ) : (
          <p className="text-sm text-muted-foreground">Không có field nào cho type này.</p>
        )}
      </div>
    </div>
  );
}
