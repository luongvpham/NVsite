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

function setup(initialVersion = 7, messageFor: (code: string) => string = (code) => `msg:${code}`) {
  const store: EditorStore = createEditorStore();
  store.getState().loadDraft(heroTree(), PageKind.Composable);
  const save = vi.fn<(tree: TreeEnvelope, version: number) => Promise<ReturnType<typeof ok>>>();
  const fetchDraft = vi.fn<() => Promise<FetchedDraft>>();
  const controller = new AutosaveController({
    store,
    initialVersion,
    save,
    fetchDraft,
    messageFor,
  });
  controller.start();
  const edit = (value: string, nodeId = 'c_hero1') =>
    store.getState().dispatch({ type: 'update', nodeId, path: ['title'], value });
  const titleOf = () => (store.getState().tree?.children[0]?.props as { title?: string } | undefined)?.title;
  const flushTimers = () => vi.advanceTimersByTimeAsync(AUTOSAVE_DEBOUNCE_MS);
  const initialTree = store.getState().tree as TreeEnvelope;
  /** GET trả đúng cây đã lưu gần nhất (bản sao JSON, đảo thứ tự key) — như server sau publish (chỉ hạ isDirty). */
  const echoSaved = (version: number) =>
    fetchDraft.mockImplementation(() => {
      const last = save.mock.calls.at(-1)?.[0] ?? initialTree;
      return Promise.resolve({ version, tree: reverseKeys(JSON.parse(JSON.stringify(last))) as TreeEnvelope });
    });
  return { store, save, fetchDraft, controller, edit, titleOf, flushTimers, echoSaved };
}

/** Đảo thứ tự key mọi object (đệ quy) — để chắc phép so cây không phụ thuộc thứ tự key. */
function reverseKeys(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(reverseKeys);
  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value)
        .reverse()
        .map(([k, v]) => [k, reverseKeys(v)]),
    );
  }
  return value;
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

  it('sanitized nhưng người dùng đã sửa tiếp → KHÔNG nạp lại; PUT kế tiếp (BE làm sạch lại) mới nạp', async () => {
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

  it('C1 — "Giữ bản của tôi" rồi PUT lỗi mạng, undo về đúng bản đã lưu → KHÔNG nạp cây server (tab khác), giữ lịch sử undo/redo', async () => {
    const { controller, fetchDraft, store, titleOf, save, flushTimers } = await conflicted();
    fetchDraft.mockResolvedValue({ version: 123, tree: heroTree('tab-khac') });
    save.mockRejectedValueOnce(new Error('net'));
    await controller.keepMine();
    expect(controller.getState().status).toBe('error');
    expect(titleOf()).toBe('cua-toi');

    store.getState().undo(); // về đúng savedTree (cây lúc mở phiên)
    await flushTimers();
    expect(titleOf()).toBe('a'); // không bị thay bằng 'tab-khac'
    expect(store.temporal.getState().futureStates).toHaveLength(1);
    store.getState().redo();
    expect(titleOf()).toBe('cua-toi'); // phần sửa chưa lưu vẫn còn
  });

  it('W1 — "Giữ bản của tôi" → PUT lỗi → undo về cây lúc mở phiên: KHÔNG báo "Đã lưu" (server giữ cây tab khác); publish PUT cây A trước', async () => {
    const { controller, fetchDraft, store, titleOf, save } = await conflicted();
    fetchDraft.mockResolvedValue({ version: 123, tree: heroTree('tab-khac') });
    save.mockRejectedValueOnce(new Error('net'));
    await controller.keepMine();
    expect(controller.getState().status).toBe('error');

    store.getState().undo(); // về đúng cây lúc mở phiên — nhưng server đang giữ cây 'tab-khac'
    expect(titleOf()).toBe('a');
    expect(controller.getState().status).not.toBe('saved');
    expect(controller.hasUnsavedChanges).toBe(true);

    save.mockResolvedValueOnce(ok(124));
    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: true });
    expect(save).toHaveBeenCalledTimes(3);
    expect(save.mock.calls[2]?.[0].children[0]?.props).toMatchObject({ title: 'a' });
    expect(save.mock.calls[2]?.[1]).toBe(123);
  });

  it('W1 — "Giữ bản của tôi" mà GET trả tree null → coi là bẩn: undo về cây lúc mở phiên vẫn chưa "Đã lưu"', async () => {
    const { controller, fetchDraft, store, save } = await conflicted();
    fetchDraft.mockResolvedValue({ version: 123, tree: null });
    save.mockRejectedValueOnce(new Error('net'));
    await controller.keepMine();
    store.getState().undo();
    expect(controller.getState().status).not.toBe('saved');
    expect(controller.hasUnsavedChanges).toBe(true);
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

describe('R9 — nhường chỗ cho publish (prepareForPublish / finishPublish)', () => {
  const invalid422 = { status: 422, error_code: 'RICHTEXT_UNSAFE' };

  it('không có thay đổi → khoá ngay, không PUT, ok', async () => {
    const { save, controller, flushTimers } = setup();
    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: true });
    await flushTimers();
    expect(save).not.toHaveBeenCalled();
    expect(controller.getState().status).toBe('saved');
  });

  it('đang chờ debounce → huỷ hẹn giờ, PUT ngay đúng MỘT lần rồi ok', async () => {
    const { save, controller, edit, flushTimers } = setup(7);
    save.mockResolvedValue(ok(8));
    edit('b');
    const result = await controller.prepareForPublish(); // không đợi debounce
    expect(result).toEqual({ ok: true });
    expect(save).toHaveBeenCalledTimes(1);
    expect(save.mock.calls[0]?.[1]).toBe(7);
    expect(save.mock.calls[0]?.[0].children[0]?.props).toMatchObject({ title: 'b' });
    await flushTimers();
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);
    expect(controller.getState().status).toBe('saved');
    expect(controller.hasUnsavedChanges).toBe(false);
  });

  it('PUT đang bay → chờ xong (không PUT song song), rồi lưu phần sửa thêm bằng version mới', async () => {
    const { save, controller, edit, flushTimers } = setup(7);
    const first = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(first.promise).mockResolvedValueOnce(ok(9));
    edit('b');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);
    edit('b-them'); // sửa trong lúc bay

    let settled = false;
    const pending = controller.prepareForPublish().then((r) => {
      settled = true;
      return r;
    });
    await vi.advanceTimersByTimeAsync(AUTOSAVE_DEBOUNCE_MS * 3);
    expect(settled).toBe(false);
    expect(save).toHaveBeenCalledTimes(1); // R1: không PUT thứ hai khi cái đầu còn bay

    first.resolve(ok(8));
    await expect(pending).resolves.toEqual({ ok: true });
    expect(save).toHaveBeenCalledTimes(2);
    expect(save.mock.calls[1]?.[1]).toBe(8);
    expect(save.mock.calls[1]?.[0].children[0]?.props).toMatchObject({ title: 'b-them' });
    expect(controller.hasUnsavedChanges).toBe(false);
  });

  it('PUT đang bay và không sửa thêm → chờ xong, không PUT lại', async () => {
    const { save, controller, edit, flushTimers } = setup(7);
    const first = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(first.promise);
    edit('b');
    await flushTimers();
    const pending = controller.prepareForPublish();
    first.resolve(ok(8));
    await expect(pending).resolves.toEqual({ ok: true });
    expect(save).toHaveBeenCalledTimes(1);
  });

  it('422 → ok:false invalid, không PUT, không khoá (sửa tiếp vẫn autosave)', async () => {
    const { save, controller, edit, flushTimers } = setup();
    save.mockRejectedValueOnce(invalid422).mockResolvedValueOnce(ok(8));
    edit('x');
    await flushTimers();
    expect(controller.getState().status).toBe('invalid');

    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: false, reason: 'invalid' });
    expect(save).toHaveBeenCalledTimes(1);

    edit('sua');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(2);
    expect(controller.getState().status).toBe('saved');
  });

  it('422 của PUT đang bay → ok:false invalid sau khi chờ, không PUT thêm', async () => {
    const { save, controller, edit, flushTimers } = setup();
    const first = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(first.promise);
    edit('x');
    await flushTimers();
    const pending = controller.prepareForPublish();
    first.reject(invalid422);
    await expect(pending).resolves.toEqual({ ok: false, reason: 'invalid' });
    expect(save).toHaveBeenCalledTimes(1);
  });

  it('409 → ok:false conflict, không PUT', async () => {
    const { save, controller, edit, flushTimers } = setup();
    save.mockRejectedValueOnce(conflict(99));
    edit('x');
    await flushTimers();
    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: false, reason: 'conflict' });
    expect(save).toHaveBeenCalledTimes(1);
    expect(controller.getState().status).toBe('conflict');
  });

  it('413 → ok:false blocked, không PUT lại', async () => {
    const { save, controller, edit, flushTimers } = setup();
    save.mockRejectedValueOnce({ status: 413, error_code: 'TREE_BODY_TOO_LARGE' });
    edit('x');
    await flushTimers();
    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: false, reason: 'blocked' });
    expect(save).toHaveBeenCalledTimes(1);
  });

  it('cây vượt giới hạn đang chờ debounce → ok:false blocked, không PUT', async () => {
    const { store, save, controller } = setup();
    store.setState({ tree: heroTree('x'.repeat(1_100_000)) });
    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: false, reason: 'blocked' });
    expect(save).not.toHaveBeenCalled();
  });

  it('lỗi mạng trước đó → thử lưu MỘT lần; vẫn lỗi → ok:false error, không khoá', async () => {
    const { save, controller, edit, flushTimers } = setup();
    save
      .mockRejectedValueOnce(new Error('Network Error'))
      .mockRejectedValueOnce(new Error('Network Error'))
      .mockResolvedValueOnce(ok(8));
    edit('x');
    await flushTimers();
    expect(controller.getState().status).toBe('error');

    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: false, reason: 'error' });
    expect(save).toHaveBeenCalledTimes(2);

    controller.retry(); // không khoá → Thử lại vẫn gửi
    await vi.advanceTimersByTimeAsync(0);
    expect(save).toHaveBeenCalledTimes(3);
    expect(controller.getState().status).toBe('saved');
  });

  it('lỗi mạng trước đó, thử lại thành công → ok', async () => {
    const { save, controller, edit, flushTimers } = setup();
    save.mockRejectedValueOnce(new Error('Network Error')).mockResolvedValueOnce(ok(8));
    edit('x');
    await flushTimers();
    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: true });
    expect(save).toHaveBeenCalledTimes(2);
  });

  it('R1 — không PUT khi đang publish: gõ trong lúc khoá chỉ đánh dấu unsaved; retry cũng không gửi', async () => {
    const { save, controller, edit, flushTimers } = setup();
    await controller.prepareForPublish();
    edit('trong-luc-khoa');
    expect(controller.getState().status).toBe('unsaved');
    expect(controller.hasUnsavedChanges).toBe(true);
    expect(vi.getTimerCount()).toBe(0); // không hẹn giờ PUT nào
    await flushTimers();
    await flushTimers();
    controller.retry();
    await vi.advanceTimersByTimeAsync(0);
    expect(save).not.toHaveBeenCalled();
  });

  it('gõ trong lúc PUT của prepare đang bay → prepare vẫn ok; phần gõ thêm chờ tới finishPublish', async () => {
    const { save, controller, edit, flushTimers, echoSaved } = setup(7);
    const put = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(put.promise).mockResolvedValueOnce(ok(21));
    echoSaved(20);
    edit('a1');
    const pending = controller.prepareForPublish();
    edit('a2');
    put.resolve(ok(8));
    await expect(pending).resolves.toEqual({ ok: true });
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);
    expect(controller.getState().status).toBe('unsaved');

    await controller.finishPublish();
    await vi.advanceTimersByTimeAsync(0);
    expect(save).toHaveBeenCalledTimes(2);
    expect(save.mock.calls[1]?.[1]).toBe(20);
  });

  it('refreshVersion giữ cây: GET trả đúng cây đã lưu (khác thứ tự key) → dùng version của GET, KHÔNG đè cây, giữ tham chiếu', async () => {
    const { save, fetchDraft, controller, edit, flushTimers, store, titleOf, echoSaved } = setup(7);
    save.mockResolvedValueOnce(ok(8)).mockResolvedValueOnce(ok(31));
    echoSaved(30);
    edit('cua-toi');
    await controller.prepareForPublish();
    const before = store.getState().tree;

    await controller.finishPublish();
    expect(fetchDraft).toHaveBeenCalledTimes(1);
    expect(store.getState().tree).toBe(before);
    expect(titleOf()).toBe('cua-toi');
    expect(controller.getState().status).toBe('saved');
    expect(controller.hasUnsavedChanges).toBe(false);
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1); // không có gì mới để gửi

    edit('sau-publish');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(2);
    expect(save.mock.calls[1]?.[1]).toBe(30); // version của GET, không phải 8 → không 409
  });

  it('W3 — GET trả cây KHÁC cây đã lưu (tab khác vừa lưu) → giữ version cũ, không đụng cây local; PUT sau sẽ 409', async () => {
    const { save, fetchDraft, controller, edit, flushTimers, store, titleOf } = setup(7);
    save.mockResolvedValueOnce(ok(8)).mockResolvedValueOnce(ok(9));
    fetchDraft.mockResolvedValue({ version: 30, tree: heroTree('tab-khac') });
    edit('cua-toi');
    await controller.prepareForPublish();
    const before = store.getState().tree;

    await controller.finishPublish();
    expect(store.getState().tree).toBe(before);
    expect(titleOf()).toBe('cua-toi');
    expect(controller.getState().status).toBe('saved');

    edit('sau-publish'); // vẫn mở khoá → autosave chạy
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(2);
    expect(save.mock.calls[1]?.[1]).toBe(8); // version cũ — BE sẽ 409 DRAFT_CONFLICT, dialog có sẵn lo
  });

  it('W3 — GET trả tree null (không đọc được) → giữ version cũ, mở khoá', async () => {
    const { save, fetchDraft, controller, edit, flushTimers } = setup(7);
    save.mockResolvedValue(ok(8));
    fetchDraft.mockResolvedValue({ version: 30, tree: null });
    await controller.prepareForPublish();
    await controller.finishPublish();
    edit('sau');
    await flushTimers();
    expect(save.mock.calls[0]?.[1]).toBe(7);
  });

  it('R1 — gõ trong lúc khoá → lưu NGAY sau finishPublish (không đợi debounce) bằng version của GET', async () => {
    const { save, controller, edit, echoSaved } = setup(7);
    save.mockResolvedValue(ok(41));
    echoSaved(40);
    await controller.prepareForPublish();
    edit('go-trong-luc-khoa');

    await controller.finishPublish();
    await vi.advanceTimersByTimeAsync(0);
    expect(save).toHaveBeenCalledTimes(1);
    expect(save.mock.calls[0]?.[1]).toBe(40);
    expect(save.mock.calls[0]?.[0].children[0]?.props).toMatchObject({ title: 'go-trong-luc-khoa' });
    expect(controller.getState().status).toBe('saved');
  });

  it('finishPublish luôn GET rồi mở khoá (không phụ thuộc kết quả publish — R2 thật test ở F5)', async () => {
    const { save, fetchDraft, controller, edit, flushTimers } = setup(7);
    save.mockResolvedValue(ok(51));
    fetchDraft.mockResolvedValue({ version: 7, tree: heroTree() });
    await controller.prepareForPublish();
    await controller.finishPublish();
    expect(fetchDraft).toHaveBeenCalledTimes(1);

    edit('tiep');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);
    expect(save.mock.calls[0]?.[1]).toBe(7);
  });

  it('R3 — GET lỗi → vẫn mở khoá, giữ version cũ, không ném', async () => {
    const { save, fetchDraft, controller, edit, flushTimers, titleOf } = setup(7);
    save.mockResolvedValueOnce(ok(8)).mockResolvedValueOnce(ok(9)).mockResolvedValueOnce(ok(10));
    fetchDraft.mockRejectedValue(new Error('net'));
    edit('truoc-publish'); // ≠ 'a' (title mặc định) để có PUT thật
    await controller.prepareForPublish();
    expect(save).toHaveBeenCalledTimes(1);
    edit('b'); // gõ trong lúc khoá

    await expect(controller.finishPublish()).resolves.toBeUndefined();
    await vi.advanceTimersByTimeAsync(0);
    expect(save).toHaveBeenCalledTimes(2);
    expect(save.mock.calls[1]?.[1]).toBe(8); // version cũ (của PUT trước)
    expect(titleOf()).toBe('b');

    edit('c');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(3); // autosave chạy lại bình thường
  });

  it('finishPublish khi không khoá → no-op (không GET)', async () => {
    const { fetchDraft, controller } = setup();
    await controller.finishPublish();
    expect(fetchDraft).not.toHaveBeenCalled();
  });

  it('gọi prepareForPublish hai lần → chung một lượt lưu, không PUT thừa', async () => {
    const { save, controller, edit } = setup(7);
    const put = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(put.promise);
    edit('b');
    const a = controller.prepareForPublish();
    const b = controller.prepareForPublish();
    put.resolve(ok(8));
    await expect(a).resolves.toEqual({ ok: true });
    await expect(b).resolves.toEqual({ ok: true });
    expect(save).toHaveBeenCalledTimes(1);
    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: true }); // đã khoá
    expect(save).toHaveBeenCalledTimes(1);
  });

  it('dispose giữa lúc prepare chờ PUT → không ném, ok:false', async () => {
    const { save, controller, edit, flushTimers } = setup();
    const first = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(first.promise);
    edit('b');
    await flushTimers();
    const pending = controller.prepareForPublish();
    controller.dispose();
    await expect(pending).resolves.toEqual({ ok: false, reason: 'error' });
    first.resolve(ok(8)); // response tới muộn không làm gì
    await vi.advanceTimersByTimeAsync(0);
  });

  it('dispose giữa lúc finishPublish chờ GET → không ném, không PUT; start lại thì không còn khoá', async () => {
    const { save, fetchDraft, controller, edit, flushTimers } = setup();
    const get = deferred<FetchedDraft>();
    fetchDraft.mockReturnValueOnce(get.promise);
    save.mockResolvedValue(ok(8));
    await controller.prepareForPublish();
    edit('b');
    const pending = controller.finishPublish();
    controller.dispose();
    get.resolve({ version: 99, tree: heroTree() });
    await expect(pending).resolves.toBeUndefined();
    await vi.advanceTimersByTimeAsync(0);
    expect(save).not.toHaveBeenCalled();

    controller.start(); // StrictMode mount lại
    edit('c');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);
    expect(save.mock.calls[0]?.[1]).toBe(7); // GET bị bỏ sau dispose
  });

  it('W1 — prepare #2 khi finishPublish #1 còn chờ GET → chờ finish xong rồi chuẩn bị thật; publish #2 vẫn khoá và lấy version', async () => {
    const { save, fetchDraft, controller, edit, flushTimers, echoSaved } = setup(7);
    const get1 = deferred<FetchedDraft>();
    echoSaved(30);
    fetchDraft.mockReturnValueOnce(get1.promise);
    save.mockResolvedValueOnce(ok(21)).mockResolvedValueOnce(ok(31));

    await controller.prepareForPublish();
    edit('trong-khoa-1');
    const finish1 = controller.finishPublish();
    let settled = false;
    const prepare2 = controller.prepareForPublish().then((r) => {
      settled = true;
      return r;
    });
    await vi.advanceTimersByTimeAsync(0);
    expect(settled).toBe(false); // không trả ok ngay khi khoá đang được mở

    get1.resolve({ version: 20, tree: heroTree() }); // = cây đã lưu (ban đầu)
    await finish1;
    await expect(prepare2).resolves.toEqual({ ok: true });
    expect(save).toHaveBeenCalledTimes(1);
    expect(save.mock.calls[0]?.[1]).toBe(20);

    edit('trong-khoa-2'); // publish #2 đang khoá
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);

    await controller.finishPublish();
    expect(fetchDraft).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(0);
    expect(save).toHaveBeenCalledTimes(2);
    expect(save.mock.calls[1]?.[1]).toBe(30); // version của GET #2
  });

  it('W1 — finishPublish khi prepare còn đang chạy → chờ prepare, rồi finish thật (không kẹt khoá)', async () => {
    const { save, fetchDraft, controller, edit, flushTimers, echoSaved } = setup(7);
    const put = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(put.promise).mockResolvedValueOnce(ok(41));
    echoSaved(40);
    edit('b');
    const prepare = controller.prepareForPublish();
    const finish = controller.finishPublish();
    put.resolve(ok(8));
    await expect(prepare).resolves.toEqual({ ok: true });
    await finish;
    expect(fetchDraft).toHaveBeenCalledTimes(1);

    edit('sau'); // đã mở khoá
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(2);
    expect(save.mock.calls[1]?.[1]).toBe(40);
  });

  it('W1 — finishPublish khi prepare đang chạy mà prepare thất bại → không GET, không khoá', async () => {
    const { save, fetchDraft, controller, edit, flushTimers } = setup(7);
    const put = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(put.promise).mockResolvedValueOnce(ok(8));
    edit('b');
    const prepare = controller.prepareForPublish();
    const finish = controller.finishPublish();
    put.reject({ status: 422, error_code: 'RICHTEXT_UNSAFE' });
    await expect(prepare).resolves.toEqual({ ok: false, reason: 'invalid' });
    await finish;
    expect(fetchDraft).not.toHaveBeenCalled();
    edit('sua');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(2);
  });

  /** PUT của prepare trả `sanitized` trong lúc người dùng vẫn gõ → reloadSanitized không nạp được cây vào store. */
  async function sanitizedWhileTyping(reloadGet: () => Promise<FetchedDraft>, finishGet: FetchedDraft) {
    const ctx = setup(7);
    const put = deferred<ReturnType<typeof ok>>();
    ctx.save.mockReturnValueOnce(put.promise).mockResolvedValue(ok(31));
    ctx.fetchDraft.mockImplementationOnce(reloadGet).mockResolvedValueOnce(finishGet);
    ctx.edit('ban-ban');
    const prepare = ctx.controller.prepareForPublish();
    ctx.edit('go-tiep'); // gõ trong lúc PUT bay → reloadSanitized bỏ qua việc nạp cây
    put.resolve(ok(8, true));
    await expect(prepare).resolves.toEqual({ ok: true });
    expect(ctx.titleOf()).toBe('go-tiep');
    await ctx.controller.finishPublish();
    await vi.advanceTimersByTimeAsync(0);
    expect(ctx.save).toHaveBeenCalledTimes(2);
    expect(ctx.titleOf()).toBe('go-tiep'); // finishPublish không bao giờ nạp cây
    return ctx;
  }

  it('W-new-1 — PUT của prepare bị sanitize trong lúc vẫn gõ → cây server biết từ GET của reloadSanitized ⇒ nhận version, không 409 giả', async () => {
    const { save } = await sanitizedWhileTyping(
      () => Promise.resolve({ version: 8, tree: heroTree('da-lam-sach') }),
      { version: 30, tree: heroTree('da-lam-sach') },
    );
    expect(save.mock.calls[1]?.[1]).toBe(30);
  });

  it('W3+sanitize — như trên nhưng tab khác đã lưu trong lúc publish (GET khác cây server đã biết) ⇒ giữ version cũ', async () => {
    const { save } = await sanitizedWhileTyping(
      () => Promise.resolve({ version: 8, tree: heroTree('da-lam-sach') }),
      { version: 30, tree: heroTree('tab-khac') },
    );
    expect(save.mock.calls[1]?.[1]).toBe(8); // BE sẽ 409 → dialog có sẵn
  });

  it('W3+sanitize — GET của reloadSanitized lỗi (không biết cây server) ⇒ giữ version cũ', async () => {
    const { save } = await sanitizedWhileTyping(
      () => Promise.reject(new Error('net')),
      { version: 30, tree: heroTree('da-lam-sach') },
    );
    expect(save.mock.calls[1]?.[1]).toBe(8);
  });

  it('W3+sanitize — GET của reloadSanitized trả version khác version vừa PUT (đã có ghi khác chen vào) ⇒ không coi là cây server ⇒ giữ version cũ', async () => {
    const { save } = await sanitizedWhileTyping(
      () => Promise.resolve({ version: 9, tree: heroTree('da-lam-sach') }),
      { version: 30, tree: heroTree('da-lam-sach') },
    );
    expect(save.mock.calls[1]?.[1]).toBe(8);
  });

  it('W-new-1 — autosave thường bỏ qua nạp bản sanitize, publish sau không cần PUT → vẫn nhận version của GET', async () => {
    const { save, fetchDraft, controller, edit, flushTimers, store, titleOf } = setup(7);
    const put = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(put.promise).mockResolvedValue(ok(51));
    fetchDraft.mockResolvedValue({ version: 8, tree: heroTree('da-lam-sach') });
    edit('ban-ban');
    await flushTimers(); // PUT bay
    edit('go-tiep'); // bước undo riêng (đã qua nhịp gộp)
    put.resolve(ok(8, true)); // sanitize, nhưng reloadSanitized bỏ qua vì đã gõ tiếp
    await vi.advanceTimersByTimeAsync(0);
    expect(titleOf()).toBe('go-tiep');
    store.getState().undo(); // về đúng cây đã gửi (chưa làm sạch) → không còn gì để PUT
    await flushTimers();
    expect(controller.hasUnsavedChanges).toBe(false);
    expect(save).toHaveBeenCalledTimes(1);
    expect(titleOf()).toBe('ban-ban'); // không nạp bản sạch khi undo (khoảng trống vô hại, theo dõi riêng)

    fetchDraft.mockResolvedValue({ version: 50, tree: heroTree('da-lam-sach') });
    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: true });
    expect(save).toHaveBeenCalledTimes(1); // không cần PUT
    await controller.finishPublish();

    edit('sau-publish');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(2);
    expect(save.mock.calls[1]?.[1]).toBe(50); // không phải 8 → không 409 giả
  });

  it('S-new-2 — fetchDraft ném đồng bộ → finishPublish không ném, không để lại "finishing" treo; publish sau vẫn chạy', async () => {
    const { save, fetchDraft, controller, edit, flushTimers } = setup(7);
    save.mockResolvedValue(ok(8));
    fetchDraft.mockImplementation(() => {
      throw new Error('sync');
    });
    await controller.prepareForPublish();
    await expect(controller.finishPublish()).resolves.toBeUndefined();
    await vi.advanceTimersByTimeAsync(0);
    expect((controller as unknown as { finishing: unknown }).finishing).toBeUndefined();

    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: true });
    edit('khoa'); // đang khoá thật
    await flushTimers();
    expect(save).not.toHaveBeenCalled();
  });

  it('runPrepare không reject khi lõi ném (messageFor ném lúc xử lý 413) ⇒ ok:false error, không kẹt khoá; finish chờ nó cũng không ném', async () => {
    const { save, fetchDraft, controller, edit, flushTimers } = setup(7, () => {
      throw new Error('boom');
    });
    save.mockRejectedValueOnce({ status: 413, error_code: 'TREE_BODY_TOO_LARGE' }).mockResolvedValue(ok(8));
    edit('b');
    const prepare = controller.prepareForPublish();
    const finish = controller.finishPublish(); // xếp hàng sau prepare
    await expect(prepare).resolves.toEqual({ ok: false, reason: 'error' });
    await expect(finish).resolves.toBeUndefined();
    expect(fetchDraft).not.toHaveBeenCalled();

    edit('c'); // không khoá → autosave chạy
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(2);
  });

  it('chuỗi chờ vẫn chạy khi phía kia reject (white-box: tiêm promise reject vào finishing/preparing)', async () => {
    const { save, controller } = setup(7);
    save.mockResolvedValue(ok(8));
    const internals = controller as unknown as {
      finishing: Promise<void> | undefined;
      preparing: Promise<unknown> | undefined;
    };
    internals.finishing = Promise.reject(new Error('finish')).finally(() => {
      internals.finishing = undefined;
    });
    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: true });
    await controller.finishPublish();

    internals.preparing = Promise.reject(new Error('prepare')).finally(() => {
      internals.preparing = undefined;
    });
    await expect(controller.finishPublish()).resolves.toBeUndefined();
  });

  it('W2 — dispose + start (remount) giữa lúc prepare chờ PUT → prepare cũ không PUT nữa, phiên mới không bị khoá', async () => {
    const { save, controller, edit, flushTimers } = setup(7);
    const first = deferred<ReturnType<typeof ok>>();
    save.mockReturnValueOnce(first.promise).mockResolvedValue(ok(50));
    edit('b');
    await flushTimers();
    edit('b-them');
    const pending = controller.prepareForPublish();
    controller.dispose();
    controller.start();
    await vi.advanceTimersByTimeAsync(0);
    first.resolve(ok(8));
    await expect(pending).resolves.toEqual({ ok: false, reason: 'error' });
    await vi.advanceTimersByTimeAsync(0);
    expect(save).toHaveBeenCalledTimes(1); // prepare cũ không gửi PUT thứ hai

    edit('phien-moi'); // phiên mới: không khoá → autosave bình thường
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(2);
    await expect(controller.prepareForPublish()).resolves.toEqual({ ok: true }); // khoá mới thật, không phải khoá sót
  });

  it('W2 — dispose + start giữa lúc finishPublish chờ GET → GET cũ không đổi version, không mở/khoá phiên mới', async () => {
    const { save, fetchDraft, controller, edit, flushTimers } = setup(7);
    const get = deferred<FetchedDraft>();
    fetchDraft.mockReturnValueOnce(get.promise);
    save.mockResolvedValue(ok(8));
    await controller.prepareForPublish();
    const pending = controller.finishPublish();
    controller.dispose();
    controller.start();
    get.resolve({ version: 99, tree: heroTree() });
    await expect(pending).resolves.toBeUndefined();
    edit('c');
    await flushTimers();
    expect(save).toHaveBeenCalledTimes(1);
    expect(save.mock.calls[0]?.[1]).toBe(7);
  });
});
