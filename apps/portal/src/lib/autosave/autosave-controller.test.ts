import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PageKind, createEditorStore, pointerOf, type EditorStore, type TreeEnvelope } from '@vsite/builder-core';
import { treeEnvelopeSchema } from '@vsite/builder-components';
import { AUTOSAVE_DEBOUNCE_MS, AutosaveController, type FetchedDraft } from './autosave-controller';
import wrongTypeFixture from '../../../../../packages/builder-components/tests/fixtures/tree/invalid-prop-wrong-type.json';

const heroTree = (title = 'a'): TreeEnvelope =>
  treeEnvelopeSchema.parse({
    schemaVersion: 1,
    id: 'root',
    type: 'Page',
    children: [{ id: 'c_hero1', type: 'Hero', variant: 'Hero01', props: { title } }],
  });

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

const ok = (version: number, sanitized = false) => ({ version, isDirty: true, sanitized });
const conflict = (currentVersion = 99) => ({
  status: 409,
  error_code: 'DRAFT_CONFLICT',
  params: { currentVersion, updatedAt: '2026-11-08T01:00:00Z', updatedByUserId: null },
});

function setup(initialVersion = 7) {
  const store: EditorStore = createEditorStore();
  store.getState().loadDraft(heroTree(), PageKind.Composable);
  const save = vi.fn<(tree: TreeEnvelope, version: number) => Promise<ReturnType<typeof ok>>>();
  const fetchDraft = vi.fn<() => Promise<FetchedDraft>>();
  const controller = new AutosaveController({
    store,
    initialVersion,
    save,
    fetchDraft,
    messageFor: (code) => `msg:${code}`,
  });
  controller.start();
  const edit = (value: string, nodeId = 'c_hero1') =>
    store.getState().dispatch({ type: 'update', nodeId, path: ['title'], value });
  const titleOf = () => (store.getState().tree?.children[0]?.props as { title?: string } | undefined)?.title;
  const flushTimers = () => vi.advanceTimersByTimeAsync(AUTOSAVE_DEBOUNCE_MS);
  return { store, save, fetchDraft, controller, edit, titleOf, flushTimers };
}

beforeEach(() => {
  vi.useFakeTimers();
});
afterEach(() => {
  vi.useRealTimers();
});

describe('debounce + version', () => {
  it('không đổi gì → không gửi; nhiều sửa liên tiếp → MỘT PUT với version của GET', async () => {
    const { save, edit, flushTimers, controller, titleOf } = setup();
    save.mockResolvedValue(ok(8));
    await flushTimers();
    expect(save).not.toHaveBeenCalled();

    edit('b');
    await vi.advanceTimersByTimeAsync(AUTOSAVE_DEBOUNCE_MS - 100);
    edit('bc'); // đổi tiếp làm mới hẹn giờ
    await vi.advanceTimersByTimeAsync(AUTOSAVE_DEBOUNCE_MS - 100);
    expect(save).not.toHaveBeenCalled();
    expect(controller.getState().status).toBe('unsaved');

    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);
    expect(save.mock.calls[0]?.[1]).toBe(7);
    expect(titleOf()).toBe('bc');
    expect(controller.getState().status).toBe('saved');
    expect(controller.hasUnsavedChanges).toBe(false);
  });

  it('PUT sau gửi đúng version trả về của PUT trước (nguyên văn, không tăng)', async () => {
    const { save, edit, flushTimers } = setup(7);
    save.mockResolvedValueOnce(ok(42)).mockResolvedValueOnce(ok(5));
    edit('b');
    await flushTimers();
    edit('c');
    await flushTimers();
    expect(save.mock.calls.map((c) => c[1])).toEqual([7, 42]);
  });

  it('undo về đúng bản đã lưu → không gửi gì', async () => {
    const { save, edit, flushTimers, store, controller } = setup();
    edit('b');
    store.getState().undo();
    await flushTimers();
    expect(save).not.toHaveBeenCalled();
    expect(controller.getState().status).toBe('saved');
  });

  it('dispose huỷ hẹn giờ', async () => {
    const { save, edit, flushTimers, controller } = setup();
    edit('b');
    controller.dispose();
    await flushTimers();
    expect(save).not.toHaveBeenCalled();
  });
});

describe('R1 — sửa trong lúc PUT đang bay', () => {
  it('không bao giờ hai PUT song song; response không ghi đè cây; PUT kế tiếp dùng version mới', async () => {
    const { save, edit, flushTimers, titleOf, controller } = setup(7);
    const first = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(first.promise).mockResolvedValueOnce(ok(9));

    edit('b');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);
    expect(controller.getState().status).toBe('saving');

    edit('b-moi'); // sửa trong lúc bay
    await flushTimers(); // hẹn giờ trôi qua khi PUT vẫn bay
    expect(save).toHaveBeenCalledTimes(1);

    first.resolve(ok(8));
    await vi.advanceTimersByTimeAsync(0);

    expect(titleOf()).toBe('b-moi'); // không bị đè
    expect(save).toHaveBeenCalledTimes(2);
    expect(save.mock.calls[1]?.[1]).toBe(8);
    expect(save.mock.calls[1]?.[0].children[0]?.props).toMatchObject({ title: 'b-moi' });
    expect(controller.getState().status).toBe('saved');
  });

  it('sửa trong lúc bay nhưng hẹn giờ chưa tới khi xong → vẫn lưu tiếp sau debounce', async () => {
    const { save, edit, flushTimers, controller } = setup(7);
    const first = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(first.promise).mockResolvedValueOnce(ok(9));
    edit('b');
    await flushTimers();
    edit('c');
    first.resolve(ok(8));
    await vi.advanceTimersByTimeAsync(0);
    expect(save).toHaveBeenCalledTimes(1);
    expect(controller.getState().status).toBe('unsaved');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(2);
  });
});

describe('R2 — sanitized', () => {
  it('sanitized + chưa sửa tiếp → GET rồi thay cây, xoá lịch sử undo, version lấy từ GET', async () => {
    const { save, fetchDraft, edit, flushTimers, store, titleOf, controller } = setup(7);
    save.mockResolvedValueOnce(ok(8, true)).mockResolvedValueOnce(ok(11));
    fetchDraft.mockResolvedValue({ version: 8, tree: heroTree('da-lam-sach') });

    edit('ban-ban');
    await flushTimers();

    expect(fetchDraft).toHaveBeenCalledTimes(1);
    expect(titleOf()).toBe('da-lam-sach');
    expect(store.temporal.getState().pastStates).toHaveLength(0);
    expect(controller.getState().status).toBe('saved');
    expect(controller.hasUnsavedChanges).toBe(false);

    edit('tiep');
    await flushTimers();
    expect(save.mock.calls[1]?.[1]).toBe(8);
  });

  it('sanitized nhưng người dùng đã sửa tiếp → KHÔNG nạp lại; lần lưu sạch kế tiếp mới nạp', async () => {
    const { save, fetchDraft, edit, flushTimers, titleOf } = setup(7);
    const first = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(first.promise).mockResolvedValueOnce(ok(9, true));
    fetchDraft.mockResolvedValue({ version: 9, tree: heroTree('sach-lan-2') });

    edit('b');
    await flushTimers();
    edit('b-them'); // sửa tiếp trong lúc bay
    first.resolve(ok(8, true));
    await vi.advanceTimersByTimeAsync(0);

    expect(fetchDraft).toHaveBeenCalledTimes(1); // GET có thể chạy nhưng kết quả bị bỏ
    expect(titleOf()).toBe('b-them');

    await flushTimers(); // lưu tiếp → sạch → nạp lại
    expect(save.mock.calls[1]?.[1]).toBe(8);
    expect(titleOf()).toBe('sach-lan-2');
  });

  it('sanitized nhưng sửa tiếp trong lúc GET → bỏ kết quả GET, giữ cây đang sửa', async () => {
    const { save, fetchDraft, edit, flushTimers, titleOf } = setup(7);
    const get = deferred<FetchedDraft>();
    save.mockResolvedValue(ok(8, true));
    fetchDraft.mockReturnValueOnce(get.promise);

    edit('b');
    await flushTimers();
    edit('b-them');
    get.resolve({ version: 8, tree: heroTree('sach') });
    await vi.advanceTimersByTimeAsync(0);
    expect(titleOf()).toBe('b-them');
  });

  it('sanitized nhưng GET lỗi → giữ cây local, không treo, không báo lỗi lưu', async () => {
    const { save, fetchDraft, edit, flushTimers, titleOf, controller } = setup(7);
    save.mockResolvedValue(ok(8, true));
    fetchDraft.mockRejectedValue(new Error('net'));
    edit('b');
    await flushTimers();
    expect(titleOf()).toBe('b');
    expect(controller.getState().status).toBe('saved');
  });
});

describe('409 DRAFT_CONFLICT', () => {
  async function conflicted() {
    const ctx = setup(7);
    ctx.save.mockRejectedValueOnce(conflict(99));
    ctx.edit('cua-toi');
    await ctx.flushTimers();
    return ctx;
  }

  it('dừng autosave, hiện params; sửa tiếp không gửi nữa', async () => {
    const { controller, edit, flushTimers, save } = await conflicted();
    expect(controller.getState().status).toBe('conflict');
    expect(controller.getState().conflict?.params?.updatedAt).toBe('2026-11-08T01:00:00Z');
    edit('them');
    await flushTimers();
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);
    expect(controller.getState().status).toBe('conflict');
  });

  it('"Tải bản mới": nạp bản server, bỏ thay đổi local, xoá lịch sử, hết dialog', async () => {
    const { controller, fetchDraft, store, titleOf, save, edit, flushTimers } = await conflicted();
    fetchDraft.mockResolvedValue({ version: 12, tree: heroTree('cua-server') });
    await controller.reloadLatest();

    expect(titleOf()).toBe('cua-server');
    expect(store.temporal.getState().pastStates).toHaveLength(0);
    expect(controller.getState().conflict).toBeNull();
    expect(controller.getState().status).toBe('saved');

    save.mockResolvedValue(ok(13));
    edit('sau-khi-tai');
    await flushTimers();
    expect(save.mock.calls[1]?.[1]).toBe(12); // version của GET
  });

  it('R8 "Giữ bản của tôi": GET lấy version mới, PUT cây local — KHÔNG dùng params.currentVersion', async () => {
    const { controller, fetchDraft, titleOf, save } = await conflicted();
    fetchDraft.mockResolvedValue({ version: 123, tree: heroTree('cua-server') });
    save.mockResolvedValueOnce(ok(124));

    await controller.keepMine();

    expect(fetchDraft).toHaveBeenCalledTimes(1);
    expect(save).toHaveBeenCalledTimes(2);
    expect(save.mock.calls[1]?.[1]).toBe(123); // ≠ 99 (currentVersion trong 409), ≠ 7
    expect(save.mock.calls[1]?.[0].children[0]?.props).toMatchObject({ title: 'cua-toi' });
    expect(titleOf()).toBe('cua-toi');
    expect(controller.getState().status).toBe('saved');
    expect(controller.getState().conflict).toBeNull();
  });

  it('"Giữ bản của tôi" mà lại 409 → dialog hiện lại', async () => {
    const { controller, fetchDraft, save } = await conflicted();
    fetchDraft.mockResolvedValue({ version: 123, tree: heroTree('x') });
    save.mockRejectedValueOnce(conflict(200));
    await controller.keepMine();
    expect(controller.getState().status).toBe('conflict');
    expect(controller.getState().conflict?.params?.currentVersion).toBe(200);
  });

  it('GET lỗi khi giải quyết → ở lại dialog với resolveFailed, không mất cây local', async () => {
    const { controller, fetchDraft, titleOf, save } = await conflicted();
    fetchDraft.mockRejectedValue(new Error('net'));
    await controller.keepMine();
    expect(controller.getState().conflict).toMatchObject({ resolving: false, resolveFailed: true });
    expect(save).toHaveBeenCalledTimes(1);
    expect(titleOf()).toBe('cua-toi');
    await controller.reloadLatest();
    expect(controller.getState().conflict?.resolveFailed).toBe(true);
    expect(titleOf()).toBe('cua-toi');
  });

  it('bản server không đọc được (tree null) → "Tải bản mới" không nạp, không mất cây local', async () => {
    const { controller, fetchDraft, titleOf } = await conflicted();
    fetchDraft.mockResolvedValue({ version: 12, tree: null });
    await controller.reloadLatest();
    expect(controller.getState().conflict?.resolveFailed).toBe(true);
    expect(titleOf()).toBe('cua-toi');
  });
});

describe('422 — lỗi nội dung', () => {
  const invalid422 = (path: string) => ({
    status: 422,
    error_code: 'TREE_INVALID',
    params: { errors: [{ path, code: wrongTypeFixture.errorCodes[0], message: 'Sai kiểu' }], truncated: true },
  });

  it('map path → node + prop trên cây HIỆN TẠI (fixture invalid-prop-wrong-type); không tự gửi lại khi cây không đổi', async () => {
    const { store, save, controller, flushTimers, edit } = setup();
    store.getState().loadDraft(treeEnvelopeSchema.parse(wrongTypeFixture.tree), PageKind.Composable);
    const path = pointerOf(store.getState().tree as TreeEnvelope, 'c_hero1', ['overlayOpacity']) ?? '';
    save.mockRejectedValue(invalid422(path));

    edit('x');
    await flushTimers();

    const { status, invalid } = controller.getState();
    expect(status).toBe('invalid');
    expect(invalid?.code).toBe('TREE_INVALID');
    expect(invalid?.mapped.byNode.get('c_hero1')?.propPaths).toEqual([['overlayOpacity']]);
    expect(invalid?.mapped.truncated).toBe(true);

    await flushTimers();
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);
  });

  it('R5: path trỏ node không còn → không crash, lỗi nằm ở banner; lưu thành công kế tiếp xoá lỗi', async () => {
    const { save, controller, edit, flushTimers } = setup();
    save.mockRejectedValueOnce(invalid422('/children/5/props/title')).mockResolvedValueOnce(ok(8));
    edit('x');
    await flushTimers();
    expect(controller.getState().invalid?.mapped.byNode.size).toBe(0);
    expect(controller.getState().invalid?.mapped.banner).toHaveLength(1);

    edit('sua-lai'); // người dùng sửa → thử lại, lần này thành công
    await flushTimers();
    expect(controller.getState().invalid).toBeNull();
    expect(controller.getState().status).toBe('saved');
  });

  it('422 không kèm params → vẫn ở trạng thái invalid theo mã, banner rỗng', async () => {
    const { save, controller, edit, flushTimers } = setup();
    save.mockRejectedValueOnce({ status: 422, error_code: 'RICHTEXT_UNSAFE' });
    edit('x');
    await flushTimers();
    expect(controller.getState()).toMatchObject({ status: 'invalid', invalid: { code: 'RICHTEXT_UNSAFE' } });
  });
});

describe('413 + giới hạn kích thước + lỗi tạm', () => {
  it('BE 413 → blocked kèm thông báo, không vòng lặp gửi; đổi tiếp thì thử lại', async () => {
    const { save, controller, edit, flushTimers } = setup();
    save.mockRejectedValueOnce({ status: 413, error_code: 'TREE_BODY_TOO_LARGE' }).mockResolvedValueOnce(ok(8));
    edit('x');
    await flushTimers();
    expect(controller.getState()).toMatchObject({
      status: 'blocked',
      blocked: { code: 'TREE_BODY_TOO_LARGE', message: 'msg:TREE_BODY_TOO_LARGE' },
    });
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);

    edit('nho-hon');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(2);
    expect(controller.getState()).toMatchObject({ status: 'saved', blocked: null });
  });

  it('R6: cây vượt 2000 node → KHÔNG gửi; quay về bản đã lưu → hết chặn', async () => {
    const { store, save, controller, flushTimers } = setup();
    const small = store.getState().tree as TreeEnvelope; // đúng cây controller coi là "đã lưu"
    const many = treeEnvelopeSchema.parse({
      schemaVersion: 1,
      id: 'root',
      type: 'Page',
      children: Array.from({ length: 2001 }, (_, i) => ({
        id: `c_n${String(i).padStart(6, '0')}`,
        type: 'RichText',
        variant: 'RichText01',
        props: { content: '<p>a</p>' },
      })),
    });
    // Engine đã chặn add/move vượt 2000 node, nên giả lập cây phình bằng cách đặt thẳng vào store (bỏ qua Engine).
    store.setState({ tree: many });
    await flushTimers();

    expect(save).not.toHaveBeenCalled();
    expect(controller.getState()).toMatchObject({ status: 'blocked', blocked: { code: 'TREE_TOO_MANY_NODES' } });

    store.setState({ tree: small });
    await flushTimers();
    expect(save).not.toHaveBeenCalled();
    expect(controller.getState()).toMatchObject({ status: 'saved', blocked: null });
  });

  it('R6: cây vượt 1 MB → không gửi', async () => {
    const { store, save, controller, flushTimers } = setup();
    // Engine chặn từng prop quá dài; 1 MB thật ra do nhiều khối/ảnh cộng lại → đặt thẳng vào store (bỏ qua Engine).
    store.setState({ tree: heroTree('x'.repeat(1_100_000)) });
    await flushTimers();
    expect(save).not.toHaveBeenCalled();
    expect(controller.getState().blocked?.code).toBe('TREE_BODY_TOO_LARGE');
  });

  it('mạng/5xx → status error, giữ dirty, không vòng lặp; Thử lại gửi ngay; đổi tiếp cũng thử lại', async () => {
    const { save, controller, edit, flushTimers } = setup();
    save
      .mockRejectedValueOnce(new Error('Network Error'))
      .mockRejectedValueOnce({ status: 500, error_code: 'INTERNAL' })
      .mockResolvedValueOnce(ok(8));
    edit('x');
    await flushTimers();
    expect(controller.getState().status).toBe('error');
    expect(controller.hasUnsavedChanges).toBe(true);
    await flushTimers();
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);

    controller.retry();
    await vi.advanceTimersByTimeAsync(0);
    expect(save).toHaveBeenCalledTimes(2);
    expect(controller.getState().status).toBe('error');

    edit('y');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(3);
    expect(controller.getState().status).toBe('saved');
    expect(controller.hasUnsavedChanges).toBe(false);
  });
});
