import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useSessionStore } from '../stores/session-store';
import { server } from '../test/msw-server';
import { draftBody, nodeEl, openFixture, validHome } from '../test/editor-helpers';

/**
 * F5 (WEBSITE-006): nút Xuất bản trong toolbar editor — phối hợp autosave (R9) với dialog xuất bản dùng chung.
 * Chạy controller thật qua route editor; BE giả lập bằng MSW có trạng thái (version, cây) để bắt 409 giả.
 */

const DRAFT_URL = '*/api/shops/:shopId/website/pages/:pageId/draft';
const POST_URL = '*/api/shops/:shopId/website/publications';

interface PutBody {
  tree: { children: { id: string; props?: Record<string, unknown> }[] };
  version: number;
}

type Responder = () => Response | Promise<Response>;

const problem = (status: number, error_code: string, params?: unknown) => () =>
  HttpResponse.json({ status, error_code, title: error_code, params }, { status });

const publication = (version: number) => ({
  id: `pub-${String(version)}`,
  version,
  note: null,
  publishedAt: '2026-11-20T00:00:00Z',
  publishedByUserId: 'u-1',
  isCurrent: true,
});

/**
 * BE giả: draft có `version` + cây; PUT sai version ⇒ 409 DRAFT_CONFLICT; publish thành công hạ `isDirty`
 * ⇒ version draft đổi (cây giữ nguyên — G6). `log` ghi thứ tự request.
 */
function fakeBackend(opts: { publish?: Responder } = {}) {
  const log: string[] = [];
  const puts: PutBody[] = [];
  const db = { version: 1, tree: validHome.tree };
  server.use(
    http.get(DRAFT_URL, () => {
      log.push('GET');
      return HttpResponse.json({ ...draftBody(validHome), tree: db.tree, version: db.version });
    }),
    http.put(DRAFT_URL, async ({ request }) => {
      const body = (await request.json()) as PutBody;
      log.push('PUT');
      puts.push(body);
      if (body.version !== db.version) {
        return problem(409, 'DRAFT_CONFLICT', { currentVersion: db.version, updatedAt: '2026-11-20T00:00:00Z', updatedByUserId: null })();
      }
      db.version += 1;
      db.tree = body.tree;
      return HttpResponse.json({ version: db.version, isDirty: true, sanitized: false });
    }),
    http.post(POST_URL, async () => {
      log.push('POST');
      if (opts.publish) return opts.publish();
      db.version += 1; // publish hạ isDirty ⇒ version draft đổi
      return HttpResponse.json(publication(7), { status: 201 });
    }),
    http.get('*/api/shops/:shopId', () =>
      HttpResponse.json({ id: 'shop-1', name: 'Spa ABC', slug: 'spa-abc', kind: 'Hosted', status: 'Active' }),
    ),
  );
  return { log, puts, db };
}

const panel = () => screen.getByRole('complementary', { name: 'Thuộc tính khối' });
const statusText = () => screen.getByTestId('autosave-status').textContent;
const toolbar = () => screen.getByRole('toolbar', { name: 'Công cụ soạn trang' });
const publishButton = () => within(toolbar()).getByRole('button', { name: 'Xuất bản' });
const dialog = () => screen.findByRole('dialog', { name: 'Xuất bản website' });
const elapse = (ms = 2100) => act(() => vi.advanceTimersByTimeAsync(ms));

function typeTitle(value: string) {
  const [input] = within(panel()).getAllByRole('textbox');
  fireEvent.change(input as HTMLElement, { target: { value } });
}

/** Mở editor rồi dựng BE giả (đăng ký SAU `openFixture` để handler của nó thắng — request chưa đi lúc này). */
async function openWithBackend(opts: { publish?: Responder } = {}) {
  openFixture(validHome);
  const be = fakeBackend(opts);
  await screen.findByText('Chào mừng');
  return be;
}

async function openAndSelectHero(opts: { publish?: Responder } = {}) {
  const be = await openWithBackend(opts);
  await waitFor(() => {
    expect(document.querySelector('[data-node-id="c_hero1"]')).not.toBeNull();
  });
  fireEvent.click(nodeEl('c_hero1'));
  return be;
}

/** Bấm Xuất bản ở toolbar → chờ dialog → bấm Xuất bản trong dialog. */
async function publishFromEditor() {
  fireEvent.click(publishButton());
  const d = await dialog();
  // Dialog mở sau một `await` (prepare) ⇒ effect "mở thì reset" của nó chưa chạy dưới fake timer; xả trước khi bấm,
  // không thì reset() xoá kết quả POST đã về (trình duyệt thật xả effect trước khi người dùng kịp bấm).
  await elapse(0);
  fireEvent.click(within(d).getByRole('button', { name: 'Xuất bản' }));
  return d;
}

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
});
afterEach(() => {
  vi.useRealTimers();
  useSessionStore.getState().clearSession();
});

describe('Xuất bản từ editor (F5)', () => {
  it('theme còn thay đổi chưa lưu ⇒ báo chặn, không mở dialog, không PUT/POST', async () => {
    const be = await openWithBackend();
    fireEvent.click(screen.getByRole('tab', { name: /Giao diện/ }));
    const color = await screen.findByLabelText('Màu chính', { selector: 'input[type="text"]' });
    fireEvent.change(color, { target: { value: '#112233' } });
    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /Giao diện •/ })).toBeInTheDocument();
    });

    fireEvent.click(publishButton());

    expect(await within(toolbar()).findByRole('alert')).toHaveTextContent(/Giao diện còn thay đổi chưa lưu/);
    await elapse();
    expect(screen.queryByRole('dialog', { name: 'Xuất bản website' })).not.toBeInTheDocument();
    expect(be.log.filter((r) => r !== 'GET')).toEqual([]);
  });

  it('autosave đang lỗi nội dung (invalid) ⇒ báo, không mở dialog, không POST', async () => {
    const be = await openAndSelectHero();
    server.use(
      http.put(DRAFT_URL, () => {
        be.log.push('PUT');
        return problem(422, 'TREE_INVALID', {
          errors: [{ path: '/children/0/props/title', code: 'PROP_INVALID', message: 'Tiêu đề sai' }],
          truncated: false,
        })();
      }),
    );
    typeTitle('Sai');
    await elapse();
    await waitFor(() => {
      expect(statusText()).toMatch(/chưa hợp lệ/);
    });

    fireEvent.click(publishButton());

    expect(await within(toolbar()).findByRole('alert')).toHaveTextContent(/sửa các lỗi/i);
    expect(screen.queryByRole('dialog', { name: 'Xuất bản website' })).not.toBeInTheDocument();
    expect(be.log).not.toContain('POST');
    expect(be.log.filter((r) => r === 'PUT')).toHaveLength(1); // không gửi lại cây y hệt
    expect(publishButton()).toBeEnabled();
  });

  it('có thay đổi chưa lưu ⇒ PUT ngay (không chờ debounce) rồi mới mở dialog và POST', async () => {
    const be = await openAndSelectHero();
    typeTitle('Bản mới');

    const d = await publishFromEditor();
    await within(d).findByText('Đã xuất bản bản v7');

    expect(be.log.filter((r) => r !== 'GET')).toEqual(['PUT', 'POST']);
    expect(be.puts[0]?.tree.children[0]?.props).toMatchObject({ title: 'Bản mới' });
    // Lời nhắc dành riêng cho editor.
    expect(within(d).getByText(/không nằm trong bản này/)).toBeInTheDocument();
  });

  it('R1: sau 201 và đóng dialog, gõ tiếp ⇒ PUT mang version của GET mới, không có dialog 409', async () => {
    const be = await openAndSelectHero();
    typeTitle('Trước publish');

    const d = await publishFromEditor();
    await within(d).findByText('Đã xuất bản bản v7');
    const versionAfterPublish = be.db.version; // PUT (2) → publish (3)
    expect(versionAfterPublish).toBe(3);
    fireEvent.click(within(d).getByRole('button', { name: 'Xong' }));

    await waitFor(() => {
      expect(be.log.at(-1)).toBe('GET'); // finishPublish lấy lại version
    });
    expect(publishButton()).toBeEnabled();

    typeTitle('Sau publish');
    await elapse();
    await waitFor(() => {
      expect(statusText()).toBe('Đã lưu');
    });
    const last = be.puts.at(-1);
    expect(last?.version).toBe(versionAfterPublish);
    expect(last?.tree.children[0]?.props).toMatchObject({ title: 'Sau publish' });
    expect(screen.queryByRole('dialog', { name: 'Có bản mới hơn' })).not.toBeInTheDocument();
  });

  it('gõ trong lúc dialog mở ⇒ không PUT tới khi đóng; đóng xong lưu ngay với version mới', async () => {
    const be = await openAndSelectHero();

    const d = await publishFromEditor();
    await within(d).findByText('Đã xuất bản bản v7');

    typeTitle('Gõ lúc khoá');
    await elapse(5000);
    expect(be.log).not.toContain('PUT');
    expect(statusText()).toBe('Chưa lưu');

    fireEvent.click(within(d).getByRole('button', { name: 'Xong' }));
    await waitFor(() => {
      expect(statusText()).toBe('Đã lưu');
    });
    expect(be.log.filter((r) => r !== 'GET')).toEqual(['POST', 'PUT']);
    expect(be.puts[0]?.version).toBe(2); // publish đổi 1 → 2; GET lấy 2
    expect(be.puts[0]?.tree.children[0]?.props).toMatchObject({ title: 'Gõ lúc khoá' });
    expect(screen.queryByRole('dialog', { name: 'Có bản mới hơn' })).not.toBeInTheDocument();
  });

  it('R2 + R5: 422 PUBLISH_BLOCKED ⇒ danh sách lỗi, lỗi trang đang mở hiện "trang này"; đóng ⇒ autosave chạy lại', async () => {
    const be = await openAndSelectHero({
      publish: problem(422, 'PUBLISH_BLOCKED', {
        truncated: false,
        errors: [
          { pageId: 'p-1', pageTitle: 'Trang chủ', path: '/seoTitle', code: 'SEO_TITLE_MISSING', message: 'Thiếu tiêu đề SEO' },
          { pageId: 'p-2', pageTitle: 'Dịch vụ', path: '/tree/a', code: 'REQUIRED_PROP_MISSING', message: 'Thiếu tên' },
        ],
      }),
    });

    const d = await publishFromEditor();
    const list = await within(d).findByRole('list', { name: 'Lỗi chặn xuất bản' });
    const groups = within(list).getAllByTestId('publish-error-group');
    expect(groups[0]).toHaveTextContent('trang này');
    expect(within(groups[0] as HTMLElement).queryByRole('link', { name: 'Mở trang' })).toBeNull();
    expect(within(groups[1] as HTMLElement).getByRole('link', { name: 'Mở trang' })).toBeInTheDocument();

    fireEvent.click(within(d).getByRole('button', { name: 'Huỷ' }));
    await waitFor(() => {
      expect(screen.queryByRole('dialog', { name: 'Xuất bản website' })).not.toBeInTheDocument();
    });
    await waitFor(() => {
      expect(be.log.at(-1)).toBe('GET');
    });

    typeTitle('Sửa lỗi');
    await elapse();
    await waitFor(() => {
      expect(statusText()).toBe('Đã lưu');
    });
    expect(be.puts).toHaveLength(1);
    expect(be.puts[0]?.version).toBe(1); // 422 không ghi gì
  });

  it('huỷ dialog (không POST) ⇒ mở khoá, autosave chạy lại', async () => {
    const be = await openAndSelectHero();

    fireEvent.click(publishButton());
    const d = await dialog();
    expect(publishButton()).toBeDisabled();
    fireEvent.click(within(d).getByRole('button', { name: 'Huỷ' }));
    await waitFor(() => {
      expect(publishButton()).toBeEnabled();
    });

    typeTitle('Sau huỷ');
    await elapse();
    await waitFor(() => {
      expect(statusText()).toBe('Đã lưu');
    });
    expect(be.log).not.toContain('POST');
    expect(be.puts).toHaveLength(1);
  });

  it('không đóng được dialog khi POST đang chạy; xong rồi đóng thì autosave chạy lại', async () => {
    const be = await openAndSelectHero({
      publish: async () => {
        await delay(300);
        return HttpResponse.json(publication(7), { status: 201 });
      },
    });

    const d = await publishFromEditor();
    await within(d).findByRole('button', { name: 'Đang xuất bản…' });
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.getByRole('dialog', { name: 'Xuất bản website' })).toBeInTheDocument();
    expect(within(d).getByRole('button', { name: 'Huỷ' })).toBeDisabled();

    await within(d).findByText('Đã xuất bản bản v7');
    fireEvent.click(within(d).getByRole('button', { name: 'Xong' }));
    typeTitle('Sau publish chậm');
    await elapse();
    await waitFor(() => {
      expect(statusText()).toBe('Đã lưu');
    });
    expect(be.puts.at(-1)?.tree.children[0]?.props).toMatchObject({ title: 'Sau publish chậm' });
  });
});
