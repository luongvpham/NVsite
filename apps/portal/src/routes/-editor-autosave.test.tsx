import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { pointerOf } from '@vsite/builder-core';
import { treeEnvelopeSchema } from '@vsite/builder-components';
import { useSessionStore } from '../stores/session-store';
import { server } from '../test/msw-server';
import { draftBody, nodeEl, openFixture, validHome, type Fixture } from '../test/editor-helpers';
import invalidWrongType from '../../../../packages/builder-components/tests/fixtures/tree/invalid-prop-wrong-type.json';

const DRAFT_URL = '*/api/shops/:shopId/website/pages/:pageId/draft';

interface PutBody {
  tree: { children: { id: string; props?: Record<string, unknown> }[] };
  version: number;
}

/** Ghi lại mọi PUT; `respond` quyết định đáp án cho từng lần (theo thứ tự). */
function mockPut(...responders: (() => Response)[]) {
  const bodies: PutBody[] = [];
  let i = 0;
  server.use(
    http.put(DRAFT_URL, async ({ request }) => {
      bodies.push((await request.json()) as PutBody);
      const responder = responders[Math.min(i, responders.length - 1)];
      i += 1;
      return responder ? responder() : HttpResponse.json({ version: 99, isDirty: true, sanitized: false });
    }),
  );
  return bodies;
}

const saved = (version: number, sanitized = false) => () =>
  HttpResponse.json({ version, isDirty: true, sanitized });

const problem = (status: number, error_code: string, params?: unknown) => () =>
  HttpResponse.json({ status, error_code, title: error_code, params }, { status });

const panel = () => screen.getByRole('complementary', { name: 'Thuộc tính khối' });
const statusText = () => screen.getByTestId('autosave-status').textContent;

/** Sửa ô đầu tiên (Tiêu đề) của Hero đang chọn. */
function typeTitle(value: string) {
  const [input] = within(panel()).getAllByRole('textbox');
  fireEvent.change(input as HTMLElement, { target: { value } });
}

async function openAndSelectHero(fixture: Fixture = validHome) {
  const view = openFixture(fixture);
  await waitFor(() => {
    expect(document.querySelector('[data-node-id="c_hero1"]')).not.toBeNull();
  });
  fireEvent.click(nodeEl('c_hero1'));
  return view;
}

/** Không `await`: khi bị blocker chặn, `navigate` chỉ resolve sau khi người dùng chọn. */
function navigateTo(router: { navigate: (opts: { to: string }) => Promise<void> }, to: string) {
  act(() => {
    void router.navigate({ to });
  });
}

const elapse = (ms = 2100) => act(() => vi.advanceTimersByTimeAsync(ms));

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
});
afterEach(() => {
  vi.useRealTimers();
  useSessionStore.getState().clearSession();
});

describe('Autosave trong editor (F8)', () => {
  it('sửa → debounce 2 s → MỘT PUT {tree, version}; trạng thái Chưa lưu → Đã lưu; PUT sau dùng version vừa nhận', async () => {
    const puts = mockPut(saved(5), saved(6));
    await openAndSelectHero();
    expect(statusText()).toBe('Đã lưu');

    typeTitle('A');
    typeTitle('AB');
    expect(statusText()).toBe('Chưa lưu');
    await elapse(1500);
    expect(puts).toHaveLength(0);
    await elapse(700);
    await waitFor(() => {
      expect(statusText()).toBe('Đã lưu');
    });

    expect(puts).toHaveLength(1);
    expect(puts[0]?.version).toBe(1); // version của GET (draftBody)
    expect(puts[0]?.tree.children[0]?.props).toMatchObject({ title: 'AB' });

    typeTitle('ABC');
    await elapse();
    await waitFor(() => {
      expect(puts).toHaveLength(2);
    });
    expect(puts[1]?.version).toBe(5);
  });

  it('sanitized → GET lại và canvas hiện bản BE đã làm sạch', async () => {
    mockPut(saved(5, true));
    await openAndSelectHero();
    const cleaned = structuredClone(validHome.tree) as { children: { props: Record<string, unknown> }[] };
    const firstProps = cleaned.children[0]?.props;
    if (firstProps) firstProps.title = 'Đã làm sạch';
    server.use(
      http.get(DRAFT_URL, () =>
        HttpResponse.json({ ...draftBody(validHome), tree: cleaned, version: 5 }),
      ),
    );

    typeTitle('<b>bẩn</b>');
    await elapse();

    await screen.findByText('Đã làm sạch');
    expect(statusText()).toBe('Đã lưu');
  });

  describe('409 DRAFT_CONFLICT', () => {
    const conflictBody = { currentVersion: 77, updatedAt: '2026-11-08T01:00:00Z', updatedByUserId: null };

    async function conflicted() {
      const puts = mockPut(problem(409, 'DRAFT_CONFLICT', conflictBody), saved(81));
      await openAndSelectHero();
      typeTitle('Của tôi');
      await elapse();
      await screen.findByRole('dialog', { name: 'Có bản mới hơn' });
      return puts;
    }

    it('hiện dialog bắt buộc chọn (Escape không đóng), autosave dừng', async () => {
      const puts = await conflicted();
      fireEvent.keyDown(document, { key: 'Escape' });
      expect(screen.getByRole('dialog', { name: 'Có bản mới hơn' })).toBeInTheDocument();
      expect(screen.getByText(/Chưa lưu — có bản mới hơn/)).toBeInTheDocument();

      typeTitle('Sửa thêm');
      await elapse();
      expect(puts).toHaveLength(1);
    });

    it('"Tải bản mới" → nạp bản server, bỏ thay đổi local, dialog đóng', async () => {
      await conflicted();
      const serverTree = structuredClone(validHome.tree) as { children: { props: Record<string, unknown> }[] };
      const props = serverTree.children[0]?.props;
      if (props) props.title = 'Bản server';
      server.use(
        http.get(DRAFT_URL, () => HttpResponse.json({ ...draftBody(validHome), tree: serverTree, version: 80 })),
      );

      fireEvent.click(screen.getByRole('button', { name: 'Tải bản mới' }));

      await screen.findByText('Bản server');
      expect(screen.queryByRole('dialog', { name: 'Có bản mới hơn' })).not.toBeInTheDocument();
      expect(statusText()).toBe('Đã lưu');
    });

    it('R8 "Giữ bản của tôi" → xác nhận → GET lấy version → PUT bản local với version của GET (≠ currentVersion)', async () => {
      const puts = await conflicted();
      server.use(http.get(DRAFT_URL, () => HttpResponse.json({ ...draftBody(validHome), version: 80 })));

      fireEvent.click(screen.getByRole('button', { name: 'Giữ bản của tôi' }));
      expect(puts).toHaveLength(1); // chưa làm gì cho tới khi xác nhận
      fireEvent.click(await screen.findByRole('button', { name: 'Ghi đè bằng bản của tôi' }));

      await waitFor(() => {
        expect(puts).toHaveLength(2);
      });
      expect(puts[1]?.version).toBe(80);
      expect(puts[1]?.tree.children[0]?.props).toMatchObject({ title: 'Của tôi' });
      await waitFor(() => {
        expect(screen.queryByRole('dialog', { name: 'Có bản mới hơn' })).not.toBeInTheDocument();
      });
      expect(statusText()).toBe('Đã lưu');
    });
  });

  describe('422', () => {
    it('fixture invalid-prop-wrong-type: node viền đỏ + ô Inspector đỏ + banner; không lỗi nào gắn node → chỉ banner', async () => {
      const fixture: Fixture = invalidWrongType;
      const tree = treeEnvelopeSchema.parse(fixture.tree);
      const path = pointerOf(tree, 'c_hero1', ['overlayOpacity']) ?? '';
      mockPut(
        problem(422, 'TREE_INVALID', {
          errors: [
            { path, code: invalidWrongType.errorCodes[0], message: 'Độ mờ phải là số' },
            { path: '/schemaVersion', code: 'SCHEMA_VERSION_UNSUPPORTED', message: 'Phiên bản lạ' },
          ],
          truncated: true,
        }),
      );
      await openAndSelectHero(fixture);

      typeTitle('Có tiêu đề');
      await elapse();

      await waitFor(() => {
        expect(nodeEl('c_hero1')).toHaveAttribute('data-invalid', 'true');
      });
      const marked = panel().querySelectorAll('[data-invalid="true"]');
      expect(marked).toHaveLength(1);
      expect(marked[0]?.textContent).toContain('Độ mờ lớp phủ');
      expect(within(panel()).getByText(/Độ mờ phải là số/)).toBeInTheDocument();
      const banner = screen.getAllByRole('alert').find((el) => el.textContent.includes('Phiên bản lạ'));
      expect(banner).toBeDefined();
      expect(banner?.textContent).toContain('Còn lỗi khác');
      expect(statusText()).toMatch(/chưa hợp lệ/);
    });

    it('R5: path trỏ node không tồn tại → không crash, không viền đỏ, banner vẫn hiện mã lỗi', async () => {
      mockPut(
        problem(422, 'TREE_INVALID', {
          errors: [{ path: '/children/9/props/title', code: 'PROP_INVALID', message: 'Lỗi của node cũ' }],
          truncated: false,
        }),
      );
      await openAndSelectHero();
      typeTitle('x');
      await elapse();

      await screen.findByText(/Lỗi của node cũ/);
      expect(document.querySelector('[data-invalid="true"]')).toBeNull();
    });

    it('lưu thành công kế tiếp xoá lỗi', async () => {
      mockPut(
        problem(422, 'TREE_INVALID', { errors: [{ path: '/children/0', code: 'X', message: 'Lỗi tạm' }], truncated: false }),
        saved(3),
      );
      await openAndSelectHero();
      typeTitle('x');
      await elapse();
      await waitFor(() => {
        expect(nodeEl('c_hero1')).toHaveAttribute('data-invalid', 'true');
      });

      typeTitle('xy');
      await elapse();
      await waitFor(() => {
        expect(nodeEl('c_hero1')).not.toHaveAttribute('data-invalid');
      });
      expect(statusText()).toBe('Đã lưu');
    });
  });

  it('413 → banner "Trang quá lớn", không gửi lại cho tới khi sửa tiếp', async () => {
    const puts = mockPut(problem(413, 'TREE_BODY_TOO_LARGE'), saved(2));
    await openAndSelectHero();
    typeTitle('x');
    await elapse();
    await screen.findByText(/Trang quá lớn/, { selector: 'strong' });
    await elapse(5000);
    expect(puts).toHaveLength(1);

    typeTitle('xy');
    await elapse();
    await waitFor(() => {
      expect(statusText()).toBe('Đã lưu');
    });
    expect(puts).toHaveLength(2);
  });

  it('cây vượt 2000 node → KHÔNG gửi, banner rõ', async () => {
    const puts = mockPut(saved(2));
    const many = {
      pageKind: 'Composable',
      tree: {
        schemaVersion: 1,
        id: 'root',
        type: 'Page',
        children: [
          { id: 'c_hero1', type: 'Hero', variant: 'Hero01', props: { title: 'a' } },
          ...Array.from({ length: 2000 }, (_, i) => ({
            id: `c_n${String(i).padStart(6, '0')}`,
            type: 'RichText',
            variant: 'RichText01',
            props: { content: '<p>a</p>' },
          })),
        ],
      },
    };
    await openAndSelectHero(many);
    typeTitle('b');
    await elapse();

    await screen.findByText(/quá 2000 khối/);
    expect(puts).toHaveLength(0);
    expect(statusText()).toMatch(/quá lớn/);
  }, 30000);

  it('lỗi mạng/5xx → "Lỗi", giữ chưa lưu, nút Thử lại lưu được', async () => {
    const puts = mockPut(problem(500, 'INTERNAL_ERROR'), saved(2));
    await openAndSelectHero();
    typeTitle('x');
    await elapse();
    await waitFor(() => {
      expect(statusText()).toContain('Lỗi');
    });
    await elapse(5000);
    expect(puts).toHaveLength(1);

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    await waitFor(() => {
      expect(statusText()).toBe('Đã lưu');
    });
    expect(puts).toHaveLength(2);
  });

  describe('rời trang khi còn chưa lưu', () => {
    it('có thay đổi chưa lưu → điều hướng bị chặn, "Ở lại" giữ nguyên; đã lưu xong thì rời không hỏi', async () => {
      mockPut(saved(2));
      const { router } = await openAndSelectHero();
      typeTitle('x');

      navigateTo(router, '/shops/shop-1/website');
      const prompt = await screen.findByRole('dialog', { name: 'Còn thay đổi chưa lưu' });
      fireEvent.click(within(prompt).getByRole('button', { name: 'Ở lại' }));
      expect(router.state.location.pathname).toBe('/shops/shop-1/website/pages/p-1');

      await elapse();
      await waitFor(() => {
        expect(statusText()).toBe('Đã lưu');
      });
      navigateTo(router, '/shops/shop-1/website');
      await waitFor(() => {
        expect(router.state.location.pathname).toBe('/shops/shop-1/website');
      });
      expect(screen.queryByRole('dialog', { name: 'Còn thay đổi chưa lưu' })).not.toBeInTheDocument();
    });

    it('"Rời đi" cho đi', async () => {
      mockPut(saved(2));
      const { router } = await openAndSelectHero();
      typeTitle('x');
      navigateTo(router, '/shops/shop-1/website');
      fireEvent.click(await screen.findByRole('button', { name: 'Rời đi' }));
      await waitFor(() => {
        expect(router.state.location.pathname).toBe('/shops/shop-1/website');
      });
    });
  });
  it('rời editor rồi mở lại → GET draft mới (không dùng cây + version cũ trong cache của lần mở trước)', async () => {
    mockPut(saved(5));
    const versions = [1, 5];
    let gets = 0;
    const { router } = await openAndSelectHero();
    server.use(
      http.get(DRAFT_URL, () => HttpResponse.json({ ...draftBody(validHome), version: versions[Math.min(gets++, 1)] })),
    );
    typeTitle('x');
    await elapse();
    await waitFor(() => {
      expect(statusText()).toBe('Đã lưu');
    });

    navigateTo(router, '/shops/shop-1/website');
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/shops/shop-1/website');
    });
    navigateTo(router, '/shops/shop-1/website/pages/p-1');
    await waitFor(() => {
      expect(gets).toBe(1);
    });
  });
});
