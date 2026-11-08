import { act, render, screen } from '@testing-library/react';
import type { DndContextProps, DragEndEvent, DragStartEvent } from '@dnd-kit/core';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PageKind, createEditorStore, type EditorStore, type TreeEnvelope } from '@vsite/builder-core';
import type { DragSource, DropSlot } from '../../lib/drop-operation';
import { EditorDnd } from './editor-dnd';

// jsdom không có layout nên không kéo thật được. Bắt props của DndContext để gọi thẳng handler — kiểm phần
// nối dây của ta (dispatch, chọn node mới, báo lỗi), không kiểm dnd-kit.
let captured: DndContextProps | undefined;
vi.mock('@dnd-kit/core', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@dnd-kit/core')>();
  return {
    ...actual,
    DndContext: (props: DndContextProps) => {
      captured = props;
      return <actual.DndContext {...props} />;
    },
  };
});

const baseTree = (): TreeEnvelope => ({
  schemaVersion: 1,
  id: 'root',
  type: 'Page',
  children: [
    { id: 'c_hero1', type: 'Hero', variant: 'Hero01' },
    { id: 'c_sect1', type: 'Section', variant: 'Section01', children: [{ id: 'c_rich1', type: 'RichText', variant: 'RichText01' }] },
    { id: 'c_gall1', type: 'Gallery', variant: 'Gallery01' },
  ],
});

function event(source: DragSource, slot: DropSlot | null): DragEndEvent {
  return {
    active: { id: 'a', data: { current: source }, rect: { current: { initial: null, translated: null } } },
    over: slot ? { id: 's', data: { current: slot }, rect: {}, disabled: false } : null,
  } as unknown as DragEndEvent;
}

function setup(pageKind: PageKind = PageKind.Composable, tree: TreeEnvelope = baseTree()) {
  const store: EditorStore = createEditorStore();
  store.getState().loadDraft(tree, pageKind);
  render(
    <EditorDnd store={store}>
      <div />
    </EditorDnd>,
  );
  const drop = (source: DragSource, slot: DropSlot | null) => {
    act(() => {
      captured?.onDragEnd?.(event(source, slot));
    });
  };
  return { store, drop };
}

const ids = (store: EditorStore) => store.getState().tree?.children.map((n) => n.id) ?? [];

beforeEach(() => {
  captured = undefined;
});

describe('EditorDnd — thả từ palette (add)', () => {
  it('thả vào slot → thêm node đúng chỗ, id mới khớp pattern, được chọn', () => {
    const { store, drop } = setup();

    drop({ kind: 'palette', type: 'RichText', variant: 'RichText01' }, { parentId: 'root', index: 1 });

    const added = store.getState().tree?.children[1];
    expect(added?.type).toBe('RichText');
    expect(added?.id).toMatch(/^c_[A-Za-z0-9_-]{8}$/);
    expect(store.getState().selectedId).toBe(added?.id);
    expect(store.getState().canUndo()).toBe(true);
  });

  it('vào Section', () => {
    const { store, drop } = setup();

    drop({ kind: 'palette', type: 'RichText', variant: 'RichText01' }, { parentId: 'c_sect1', index: 0 });

    expect(store.getState().tree?.children[1]?.children).toHaveLength(2);
    expect(store.getState().tree?.children[1]?.children?.[0]?.type).toBe('RichText');
  });

  it('Hero thứ hai bị chặn: không đổi cây, hiện "Không thả được" kèm lý do', () => {
    const { store, drop } = setup();
    const before = store.getState().tree;

    drop({ kind: 'palette', type: 'Hero', variant: 'Hero02' }, { parentId: 'root', index: 0 });

    expect(store.getState().tree).toBe(before);
    expect(screen.getByRole('alert')).toHaveTextContent(/Không thả được:.*tối đa một lần/);
  });
});

describe('EditorDnd — thả node (move)', () => {
  it('đổi chỗ trong cùng cha', () => {
    const { store, drop } = setup();

    drop({ kind: 'node', nodeId: 'c_hero1' }, { parentId: 'root', index: 3 });

    expect(ids(store)).toEqual(['c_sect1', 'c_gall1', 'c_hero1']);
  });

  it('vào Section khác cha, rồi ra ngoài lại', () => {
    const { store, drop } = setup();

    drop({ kind: 'node', nodeId: 'c_gall1' }, { parentId: 'c_sect1', index: 1 });
    expect(ids(store)).toEqual(['c_hero1', 'c_sect1']);
    expect(store.getState().tree?.children[1]?.children?.map((n) => n.id)).toEqual(['c_rich1', 'c_gall1']);

    drop({ kind: 'node', nodeId: 'c_gall1' }, { parentId: 'root', index: 0 });
    expect(ids(store)).toEqual(['c_gall1', 'c_hero1', 'c_sect1']);
  });

  it('thả ngay trước/sau chính nó: không đổi cây, không thêm bước undo', () => {
    const { store, drop } = setup();
    const before = store.getState().tree;

    drop({ kind: 'node', nodeId: 'c_sect1' }, { parentId: 'root', index: 1 });
    drop({ kind: 'node', nodeId: 'c_sect1' }, { parentId: 'root', index: 2 });

    expect(store.getState().tree).toBe(before);
    expect(store.getState().canUndo()).toBe(false);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('thả Section vào chính nó: bị chặn, báo lỗi', () => {
    const { store, drop } = setup();
    const before = store.getState().tree;

    drop({ kind: 'node', nodeId: 'c_sect1' }, { parentId: 'c_sect1', index: 0 });

    expect(store.getState().tree).toBe(before);
    expect(screen.getByRole('alert')).toHaveTextContent(/chính nó/);
  });

  it('move rồi undo → về cây cũ', () => {
    const { store, drop } = setup();
    drop({ kind: 'node', nodeId: 'c_hero1' }, { parentId: 'root', index: 3 });

    act(() => {
      store.getState().undo();
    });

    expect(ids(store)).toEqual(['c_hero1', 'c_sect1', 'c_gall1']);
  });
});

describe('EditorDnd — ngoài vùng thả / System / trạng thái lỗi', () => {
  it('thả ra ngoài mọi vùng (over = null) → không làm gì', () => {
    const { store, drop } = setup();
    const before = store.getState().tree;

    drop({ kind: 'node', nodeId: 'c_hero1' }, null);

    expect(store.getState().tree).toBe(before);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('dữ liệu kéo sai hình dạng → bỏ qua, không ném lỗi', () => {
    const { store } = setup();
    const before = store.getState().tree;

    act(() => {
      captured?.onDragEnd?.({
        active: { id: 'x', data: { current: { kind: 'lạ' } } },
        over: { id: 'y', data: { current: { parentId: 'root', index: 0 } } },
      } as unknown as DragEndEvent);
    });

    expect(store.getState().tree).toBe(before);
  });

  it('trang System: thả bị chặn (cấu trúc cố định)', () => {
    const tree: TreeEnvelope = {
      schemaVersion: 1,
      id: 'root',
      type: 'Page',
      children: [{ id: 'c_cont1', type: 'RichText', variant: 'RichText01' }],
    };
    const { store, drop } = setup(PageKind.System, tree);
    const before = store.getState().tree;

    drop({ kind: 'palette', type: 'RichText', variant: 'RichText01' }, { parentId: 'root', index: 0 });
    drop({ kind: 'node', nodeId: 'c_cont1' }, { parentId: 'root', index: 1 });

    expect(store.getState().tree).toBe(before);
    expect(screen.getByRole('alert')).toHaveTextContent(/Không thả được/);
  });

  it('lỗi cũ bị xoá khi bắt đầu kéo lần mới và khi thả thành công', () => {
    const { drop } = setup();
    drop({ kind: 'palette', type: 'Hero', variant: 'Hero01' }, { parentId: 'root', index: 0 });
    expect(screen.getByRole('alert')).toBeInTheDocument();

    act(() => {
      captured?.onDragStart?.({
        active: { id: 'a', data: { current: { kind: 'node', nodeId: 'c_hero1' } } },
      } as unknown as DragStartEvent);
    });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    drop({ kind: 'palette', type: 'Hero', variant: 'Hero01' }, { parentId: 'root', index: 0 });
    drop({ kind: 'palette', type: 'RichText', variant: 'RichText01' }, { parentId: 'root', index: 0 });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
