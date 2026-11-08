import { describe, expect, it } from 'vitest';
import { PageKind } from '../tree/types';
import { envelope, hero, richText, section } from '../test-support/trees';
import { createEditorStore } from './editor-store';

const addRichText = (id: string, index = 0, parentId = 'root') => ({
  type: 'add' as const,
  parentId,
  index,
  node: { id, type: 'RichText', variant: 'RichText01' },
});

const tokens = () => ({
  schemaVersion: 1,
  colors: {
    primary: '#112233',
    secondary: '#a78bfa',
    background: '#ffffff',
    surface: '#f9fafb',
    text: '#111827',
    textMuted: '#6b7280',
    border: '#e5e7eb',
  },
  typography: { headingFont: 'Be Vietnam Pro', bodyFont: 'Inter', scale: 'comfortable' },
  shape: { radius: 'md', shadow: 'soft' },
  spacing: { sectionGap: 'lg' },
});

const history = (store: ReturnType<typeof createEditorStore>) => store.temporal.getState();

describe('loadDraft', () => {
  it('nạp cây + loại trang, bỏ chọn, xoá lịch sử (nền của undo = bản vừa tải)', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope([hero()]), PageKind.Composable);
    store.getState().dispatch(addRichText('c_new01'));
    expect(history(store).pastStates).toHaveLength(1);

    store.getState().select('c_hero1');
    store.getState().loadDraft(envelope([richText('c_rt001')]), PageKind.System);

    const state = store.getState();
    expect(state.tree?.children.map((n) => n.id)).toEqual(['c_rt001']);
    expect(state.pageKind).toBe(PageKind.System);
    expect(state.selectedId).toBeNull();
    expect(history(store).pastStates).toHaveLength(0);
    expect(history(store).futureStates).toHaveLength(0);
  });
});

describe('dispatch', () => {
  it('chưa nạp draft → ok:false NO_DRAFT', () => {
    const store = createEditorStore();
    expect(store.getState().dispatch(addRichText('c_new01'))).toMatchObject({ ok: false, code: 'NO_DRAFT' });
  });

  it('op hợp lệ → áp vào cây, ghi đúng một bước lịch sử', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope(), PageKind.Composable);
    expect(store.getState().dispatch(addRichText('c_new01'))).toEqual({ ok: true });
    expect(store.getState().tree?.children.map((n) => n.id)).toEqual(['c_new01']);
    expect(history(store).pastStates).toHaveLength(1);
  });

  it('op bị từ chối → ok:false + mã, cây giữ nguyên TAM CHIẾU, không ghi lịch sử', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope([hero()]), PageKind.Composable);
    const before = store.getState().tree;
    const result = store.getState().dispatch({ type: 'remove', nodeId: 'c_nope0' });
    expect(result).toMatchObject({ ok: false, code: 'TARGET_NOT_FOUND' });
    expect(store.getState().tree).toBe(before);
    expect(history(store).pastStates).toHaveLength(0);
  });

  it('op không đổi gì (update cùng giá trị) → ok nhưng KHÔNG tạo bước undo', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope([hero('c_hero1', { title: 'A' })]), PageKind.Composable);
    expect(store.getState().dispatch({ type: 'update', nodeId: 'c_hero1', path: ['title'], value: 'A' })).toEqual({ ok: true });
    expect(history(store).pastStates).toHaveLength(0);
  });

  it('trang System: add bị từ chối, update prop editable được', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope([richText('c_page01')]), PageKind.System);
    expect(store.getState().dispatch(addRichText('c_new01'))).toMatchObject({ ok: false, code: 'SYSTEM_PAGE_STRUCTURE_CHANGED' });
    expect(store.getState().dispatch({ type: 'update', nodeId: 'c_page01', path: ['content'], value: '<p>x</p>' })).toEqual({ ok: true });
  });
});

describe('undo / redo (#42: chỉ client, trong một phiên)', () => {
  it('undo gỡ op gần nhất, redo áp lại; canUndo/canRedo phản ánh lịch sử', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope(), PageKind.Composable);
    store.getState().dispatch(addRichText('c_aaaaa'));
    store.getState().dispatch(addRichText('c_bbbbb', 1));
    expect(store.getState().canUndo()).toBe(true);
    expect(store.getState().canRedo()).toBe(false);

    store.getState().undo();
    expect(store.getState().tree?.children.map((n) => n.id)).toEqual(['c_aaaaa']);
    expect(store.getState().canRedo()).toBe(true);

    store.getState().undo();
    expect(store.getState().tree?.children).toEqual([]);
    expect(store.getState().canUndo()).toBe(false);

    store.getState().redo();
    store.getState().redo();
    expect(store.getState().tree?.children.map((n) => n.id)).toEqual(['c_aaaaa', 'c_bbbbb']);
  });

  it('op mới sau undo xoá nhánh redo', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope(), PageKind.Composable);
    store.getState().dispatch(addRichText('c_aaaaa'));
    store.getState().undo();
    store.getState().dispatch(addRichText('c_bbbbb'));
    expect(store.getState().canRedo()).toBe(false);
  });

  it('undo/redo khi hết lịch sử là no-op, không ném lỗi', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope([hero()]), PageKind.Composable);
    expect(() => {
      store.getState().undo();
      store.getState().redo();
    }).not.toThrow();
    expect(store.getState().tree?.children).toHaveLength(1);
  });

  it('giữ tối đa 100 bước', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope(), PageKind.Composable);
    for (let i = 0; i < 105; i++) store.getState().dispatch(addRichText(`c_n${String(i).padStart(5, '0')}`, i));
    expect(history(store).pastStates).toHaveLength(100);
    expect(store.getState().tree?.children).toHaveLength(105);
  });

  it('update cùng một prop nhiều lần vẫn là từng bước undo được', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope([hero()]), PageKind.Composable);
    store.getState().dispatch({ type: 'update', nodeId: 'c_hero1', path: ['title'], value: 'A' });
    store.getState().dispatch({ type: 'update', nodeId: 'c_hero1', path: ['title'], value: 'AB' });
    store.getState().undo();
    expect(store.getState().tree?.children[0]?.props).toEqual({ title: 'A' });
  });
});

describe('selection', () => {
  it('select node có thật; id lạ bị bỏ qua; null bỏ chọn', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope([hero()]), PageKind.Composable);
    store.getState().select('c_hero1');
    expect(store.getState().selectedId).toBe('c_hero1');
    store.getState().select('c_nope0');
    expect(store.getState().selectedId).toBe('c_hero1');
    store.getState().select(null);
    expect(store.getState().selectedId).toBeNull();
  });

  it('select KHÔNG vào lịch sử undo', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope([hero(), richText()]), PageKind.Composable);
    store.getState().select('c_hero1');
    store.getState().select('c_rt001');
    expect(history(store).pastStates).toHaveLength(0);
  });

  it('gỡ node đang chọn (hoặc tổ tiên của nó) → bỏ chọn', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope([section('c_sec01', [richText('c_rt001')]), hero()]), PageKind.Composable);
    store.getState().select('c_rt001');
    store.getState().dispatch({ type: 'remove', nodeId: 'c_sec01' });
    expect(store.getState().selectedId).toBeNull();
  });

  it('undo làm node đang chọn biến mất → bỏ chọn; undo lại gỡ xoá thì chọn không tự quay lại', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope(), PageKind.Composable);
    store.getState().dispatch(addRichText('c_aaaaa'));
    store.getState().select('c_aaaaa');
    store.getState().undo();
    expect(store.getState().selectedId).toBeNull();
    store.getState().redo();
    expect(store.getState().selectedId).toBeNull();
  });

  it('move giữ nguyên lựa chọn', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope([richText('c_aaaaa'), richText('c_bbbbb')]), PageKind.Composable);
    store.getState().select('c_aaaaa');
    store.getState().dispatch({ type: 'move', nodeId: 'c_aaaaa', toParentId: 'root', toIndex: 1 });
    expect(store.getState().selectedId).toBe('c_aaaaa');
  });
});

describe('theme (changeTheme — ngoài lịch sử của cây)', () => {
  it('changeTheme hợp lệ đặt themeDraft; không tạo bước undo; undo cây không hoàn tác theme', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope(), PageKind.Composable);
    store.getState().dispatch(addRichText('c_aaaaa'));
    expect(store.getState().dispatch({ type: 'changeTheme', tokens: tokens() })).toEqual({ ok: true });
    expect(store.getState().themeDraft).toEqual(tokens());
    expect(history(store).pastStates).toHaveLength(1);

    store.getState().undo();
    expect(store.getState().tree?.children).toEqual([]);
    expect(store.getState().themeDraft).toEqual(tokens());
  });

  it('tokens sai → THEME_TOKEN_INVALID, themeDraft không đổi', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope(), PageKind.Composable);
    store.getState().loadTheme(tokens());
    const bad = { ...tokens(), colors: { ...tokens().colors, primary: 'tím' } };
    expect(store.getState().dispatch({ type: 'changeTheme', tokens: bad })).toMatchObject({ ok: false, code: 'THEME_TOKEN_INVALID' });
    expect(store.getState().themeDraft).toEqual(tokens());
  });

  it('changeTheme dùng được cả khi chưa nạp draft của trang (theme thuộc website, không phải trang)', () => {
    const store = createEditorStore();
    expect(store.getState().dispatch({ type: 'changeTheme', tokens: tokens() })).toEqual({ ok: true });
    expect(store.getState().themeDraft).toEqual(tokens());
  });

  it('loadTheme đặt bản gốc, không vào lịch sử', () => {
    const store = createEditorStore();
    store.getState().loadDraft(envelope(), PageKind.Composable);
    store.getState().loadTheme(tokens());
    expect(history(store).pastStates).toHaveLength(0);
    expect(store.getState().themeDraft).toEqual(tokens());
  });
});
