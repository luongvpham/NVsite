import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  getGetNavigationQueryKey,
  type NavigationDto,
  type NavigationItemDto,
  type PageDto,
  type SaveNavigationRequest,
} from '@vsite/api-sdk';
import { getErrorMessage } from '../../lib/error-messages';
import { server } from '../../test/msw-server';
import { renderWithQuery } from '../../test/render-with-query';
import { NavigationEditor } from './navigation-editor';

function page(overrides: Partial<PageDto>): PageDto {
  return {
    id: 'p-x',
    kind: 'Composable',
    systemType: null,
    slug: 'x',
    title: 'X',
    seoTitle: null,
    seoDescription: null,
    ogImageId: null,
    noIndex: false,
    isEnabled: true,
    sortOrder: 0,
    isHomePage: false,
    isDirty: false,
    deletable: true,
    updatedAt: '2026-11-21T00:00:00Z',
    ...overrides,
  };
}

const pages: PageDto[] = [
  page({
    id: 'p-home',
    slug: '',
    title: 'Trang chủ',
    isHomePage: true,
    deletable: false,
    sortOrder: 0,
  }),
  // Id chữ hoa: khoá `page:` BE trả luôn chữ thường ⇒ phải so chữ thường mới ra tên trang.
  page({ id: 'P-ABOUT', slug: 'gioi-thieu', title: 'Giới thiệu', sortOrder: 1 }),
  page({ id: 'p-off', slug: 'khuyen-mai', title: 'Khuyến mãi', isEnabled: false, sortOrder: 2 }),
  page({
    id: 'p-contact',
    kind: 'System',
    systemType: 'Contact',
    slug: 'lien-he',
    title: 'Liên hệ',
    deletable: false,
    sortOrder: 3,
  }),
  page({
    id: 'p-404',
    kind: 'System',
    systemType: 'NotFound',
    slug: null,
    title: 'Không tìm thấy',
    noIndex: true,
    deletable: false,
    sortOrder: 4,
  }),
];

type Grand = { key: string; label: string | null; url: string | null; hidden: boolean };
function item(key: string, overrides: Partial<NavigationItemDto> = {}): NavigationItemDto {
  return { key, label: null, url: null, hidden: false, children: [], ...overrides };
}
function grand(key: string, overrides: Partial<Grand> = {}): Grand {
  return { key, label: null, url: null, hidden: false, ...overrides };
}

/** Menu tự sinh khi chưa từng lưu (`version: null`). */
const autoMenu: NavigationDto = {
  version: null,
  isDirty: true,
  items: [item('page:p-home'), item('page:p-about'), item('page:p-off'), item('page:p-contact')],
};

const nestedMenu: NavigationDto = {
  version: 7,
  isDirty: false,
  items: [
    item('page:p-home'),
    item('page:p-about', {
      label: 'Về chúng tôi',
      children: [
        {
          key: 'custom',
          label: 'Facebook',
          url: 'https://facebook.com/spa',
          hidden: false,
          children: [grand('page:p-contact')],
        },
      ],
    }),
  ],
};

interface Setup {
  menus?: NavigationDto[];
  put?: (body: SaveNavigationRequest, call: number) => Response;
}

/** GET trả lần lượt `menus` (phần tử cuối lặp lại); PUT ghi lại body. */
function setup({ menus = [autoMenu], put }: Setup = {}) {
  const bodies: SaveNavigationRequest[] = [];
  let gets = 0;
  server.use(
    http.get('*/api/shops/:shopId/website/pages', () => HttpResponse.json(pages)),
    http.get('*/api/shops/:shopId/website/navigation', () => {
      const menu = menus[Math.min(gets, menus.length - 1)];
      gets += 1;
      return HttpResponse.json(menu);
    }),
    http.put('*/api/shops/:shopId/website/navigation', async ({ request }) => {
      const body = (await request.json()) as SaveNavigationRequest;
      bodies.push(body);
      return put
        ? put(body, bodies.length)
        : HttpResponse.json({ version: 100 + bodies.length, isDirty: true });
    }),
  );
  const utils = renderWithQuery(<NavigationEditor shopId="shop-1" />);
  return { ...utils, bodies, getCount: () => gets };
}

function rows(): HTMLElement[] {
  return screen.queryAllByTestId('nav-row');
}

/** Dòng có tên hiển thị (tên trang hoặc nhãn liên kết) đúng `name`. */
function row(name: string): HTMLElement {
  const found = rows().find((r) => r.getAttribute('data-nav-name') === name);
  if (!found)
    throw new Error(
      `Không thấy dòng "${name}" — có: ${rows()
        .map((r) => r.getAttribute('data-nav-name'))
        .join(', ')}`,
    );
  return found;
}

function rowNames(): string[] {
  return rows().map((r) => r.getAttribute('data-nav-name') ?? '');
}

const save = () => {
  fireEvent.click(screen.getByRole('button', { name: 'Lưu menu' }));
};

// ── Bàn phím dnd-kit trong jsdom ────────────────────────────────────────────────────────────────────────────
// jsdom không có layout: mọi rect = 0 nên collision của dnd-kit không chọn được dòng nào. Giả rect theo vị trí
// dòng trong danh sách (`data-nav-index`) — đủ để KeyboardSensor + coordinateGetter chạy như trình duyệt.
const ROW_HEIGHT = 40;
const ROW_GAP = 8;
beforeEach(() => {
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (
    this: HTMLElement,
  ) {
    const rowEl = this.closest<HTMLElement>('[data-nav-index]');
    if (!rowEl) return DOMRect.fromRect({ x: 0, y: 0, width: 0, height: 0 });
    const index = Number(rowEl.getAttribute('data-nav-index'));
    return DOMRect.fromRect({
      x: 0,
      y: index * (ROW_HEIGHT + ROW_GAP),
      width: 600,
      height: ROW_HEIGHT,
    });
  });
  Element.prototype.scrollIntoView = () => {};
});
afterEach(() => {
  vi.restoreAllMocks();
});

/** KeyboardSensor gắn listener keydown trong `setTimeout` — chờ một nhịp sau mỗi phím. */
async function press(code: string) {
  const target = document.activeElement ?? document.body;
  await act(async () => {
    fireEvent.keyDown(target, { code, key: code === 'Space' ? ' ' : code });
    await new Promise((resolve) => setTimeout(resolve, 0));
  });
}

async function pickUp(name: string) {
  const handle = within(row(name)).getByRole('button', { name: `Kéo ${name}` });
  handle.focus();
  await press('Space');
}

describe('NavigationEditor — tải', () => {
  it('đang tải: hiện "Đang tải menu..."', () => {
    setup();
    expect(screen.getByText('Đang tải menu...')).toBeInTheDocument();
  });

  it('GET lỗi: báo lỗi + Thử lại tải lại được', async () => {
    let fail = true;
    server.use(
      http.get('*/api/shops/:shopId/website/pages', () => HttpResponse.json(pages)),
      http.get('*/api/shops/:shopId/website/navigation', () =>
        fail
          ? HttpResponse.json(
              { status: 404, title: 'x', error_code: 'WEBSITE_NOT_FOUND' },
              { status: 404 },
            )
          : HttpResponse.json(autoMenu),
      ),
    );
    renderWithQuery(<NavigationEditor shopId="shop-1" />);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      getErrorMessage('WEBSITE_NOT_FOUND'),
    );
    fail = false;
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(await screen.findByText('Trang chủ')).toBeInTheDocument();
  });

  it('menu tự sinh (version null): hiện tên trang (so khoá chữ thường), gắn "Trang đang tắt"', async () => {
    setup();
    await screen.findByText('Trang chủ');

    expect(rowNames()).toEqual(['Trang chủ', 'Giới thiệu', 'Khuyến mãi', 'Liên hệ']);
    expect(within(row('Khuyến mãi')).getByText('Trang đang tắt')).toBeInTheDocument();
    expect(within(row('Trang chủ')).queryByText('Trang đang tắt')).not.toBeInTheDocument();
    // Nhãn trống = dùng tên trang.
    const label = within(row('Giới thiệu')).getByRole('textbox', { name: 'Nhãn hiển thị' });
    expect(label).toHaveValue('');
    expect(label).toHaveAttribute('placeholder', 'Giới thiệu');
  });
});

describe('NavigationEditor — lưu', () => {
  it('gửi đủ field mọi mục theo đúng cây; lần sau gửi version của kết quả PUT', async () => {
    const { bodies } = setup({ menus: [nestedMenu] });
    await screen.findByText('Trang chủ');

    // Trang đã có trong menu và trang NotFound không có trong danh sách thêm.
    const picker = screen.getByRole('combobox', { name: 'Thêm trang vào menu' });
    const options = within(picker)
      .getAllByRole('option')
      .map((o) => o.textContent);
    expect(options).toEqual(['Chọn trang…', 'Khuyến mãi']);

    fireEvent.change(within(row('Trang chủ')).getByRole('textbox', { name: 'Nhãn hiển thị' }), {
      target: { value: 'Nhà' },
    });
    fireEvent.change(within(row('Giới thiệu')).getByRole('textbox', { name: 'Nhãn hiển thị' }), {
      target: { value: '' },
    });
    fireEvent.click(within(row('Facebook')).getByRole('button', { name: 'Ẩn' }));

    fireEvent.click(screen.getByRole('button', { name: 'Thêm liên kết' }));
    const newRow = row('');
    fireEvent.change(within(newRow).getByRole('textbox', { name: 'Nhãn hiển thị' }), {
      target: { value: 'Zalo' },
    });
    fireEvent.change(within(row('Zalo')).getByRole('textbox', { name: 'Địa chỉ liên kết' }), {
      target: { value: 'https://zalo.me/123' },
    });

    fireEvent.change(picker, { target: { value: 'p-off' } });
    fireEvent.click(screen.getByRole('button', { name: 'Thêm trang' }));

    save();
    await screen.findByText('Đã lưu menu.');

    expect(bodies[0]).toEqual({
      version: 7,
      items: [
        { key: 'page:p-home', label: 'Nhà', url: null, hidden: false, children: [] },
        {
          key: 'page:p-about',
          label: null,
          url: null,
          hidden: false,
          children: [
            {
              key: 'custom',
              label: 'Facebook',
              url: 'https://facebook.com/spa',
              hidden: true,
              children: [{ key: 'page:p-contact', label: null, url: null, hidden: false }],
            },
          ],
        },
        { key: 'custom', label: 'Zalo', url: 'https://zalo.me/123', hidden: false, children: [] },
        { key: 'page:p-off', label: null, url: null, hidden: false, children: [] },
      ],
    });
    // Cấp 3 không có `children` (BE trả 400 nếu có).
    expect(bodies[0]?.items[1]?.children[0]?.children[0]).not.toHaveProperty('children');

    save();
    await waitFor(() => {
      expect(bodies).toHaveLength(2);
    });
    expect(bodies[1]?.version).toBe(101);
  });

  it('liên kết thiếu nhãn hoặc URL không an toàn: tô dòng, không gửi PUT', async () => {
    const { bodies } = setup();
    await screen.findByText('Trang chủ');

    fireEvent.click(screen.getByRole('button', { name: 'Thêm liên kết' }));
    fireEvent.change(within(row('')).getByRole('textbox', { name: 'Địa chỉ liên kết' }), {
      target: { value: 'javascript:alert(1)' },
    });
    save();

    const bad = row('');
    expect(bad).toHaveAttribute('data-invalid', 'true');
    expect(within(bad).getByText(getErrorMessage('NAV_LABEL_REQUIRED'))).toBeInTheDocument();
    expect(within(bad).getByText(getErrorMessage('NAV_URL_UNSAFE'))).toBeInTheDocument();
    expect(bodies).toHaveLength(0);
  });

  it('đủ 12 mục cấp 1: chặn thêm liên kết / trang', async () => {
    const twelve: NavigationDto = {
      version: 1,
      isDirty: false,
      items: Array.from({ length: 12 }, (_, i) =>
        item('custom', { label: `L${String(i)}`, url: `/l${String(i)}` }),
      ),
    };
    setup({ menus: [twelve] });
    await screen.findByText('L0');

    expect(screen.getByRole('button', { name: 'Thêm liên kết' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Thêm trang' })).toBeDisabled();
    expect(screen.getByText(/tối đa 12 mục cấp 1/)).toBeInTheDocument();
  });

  it('422 NAVIGATION_INVALID: tô đúng dòng theo `path` (JSON Pointer)', async () => {
    setup({
      menus: [nestedMenu],
      put: () =>
        HttpResponse.json(
          {
            status: 422,
            title: 'x',
            error_code: 'NAVIGATION_INVALID',
            params: {
              errors: [{ path: '/items/1/children/0/url', code: 'NAV_URL_UNSAFE', message: '' }],
              truncated: false,
            },
          },
          { status: 422 },
        ),
    });
    await screen.findByText('Facebook');

    save();

    await waitFor(() => {
      expect(row('Facebook')).toHaveAttribute('data-invalid', 'true');
    });
    expect(
      within(row('Facebook')).getByText(getErrorMessage('NAV_URL_UNSAFE')),
    ).toBeInTheDocument();
    expect(row('Giới thiệu')).not.toHaveAttribute('data-invalid');
    expect(row('Liên hệ')).not.toHaveAttribute('data-invalid');
    expect(screen.getByRole('alert')).toHaveTextContent(getErrorMessage('NAVIGATION_INVALID'));
  });

  it('409 NAVIGATION_CONFLICT: báo "menu vừa được sửa ở nơi khác", tải lại, lần lưu sau dùng version mới', async () => {
    const fresh: NavigationDto = { version: 3, isDirty: true, items: [item('page:p-contact')] };
    const { bodies, getCount } = setup({
      menus: [autoMenu, fresh],
      put: (_body, call) =>
        call === 1
          ? HttpResponse.json(
              {
                status: 409,
                title: 'x',
                error_code: 'NAVIGATION_CONFLICT',
                params: { currentVersion: null, updatedAt: null },
              },
              { status: 409 },
            )
          : HttpResponse.json({ version: 4, isDirty: true }),
    });
    await screen.findByText('Trang chủ');

    save();

    expect(await screen.findByText(/menu vừa được sửa ở nơi khác/i)).toBeInTheDocument();
    await waitFor(() => {
      expect(rowNames()).toEqual(['Liên hệ']);
    });
    expect(getCount()).toBe(2);
    expect(bodies[0]?.version).toBeNull();

    save();
    await waitFor(() => {
      expect(bodies).toHaveLength(2);
    });
    expect(bodies[1]).toEqual({
      version: 3,
      items: [{ key: 'page:p-contact', label: null, url: null, hidden: false, children: [] }],
    });
  });
});

describe('NavigationEditor — chính sách query của bản nháp', () => {
  it('refetch nền sau khi lưu bị lỗi: bản đang sửa vẫn còn, không thay bằng màn lỗi', async () => {
    let gets = 0;
    server.use(
      http.get('*/api/shops/:shopId/website/pages', () => HttpResponse.json(pages)),
      http.get('*/api/shops/:shopId/website/navigation', () => {
        gets += 1;
        return gets === 1
          ? HttpResponse.json(autoMenu)
          : HttpResponse.json({ status: 500, title: 'x', error_code: 'X' }, { status: 500 });
      }),
      http.put('*/api/shops/:shopId/website/navigation', () =>
        HttpResponse.json({ version: 1, isDirty: true }),
      ),
    );
    renderWithQuery(<NavigationEditor shopId="shop-1" />);
    await screen.findByText('Trang chủ');

    fireEvent.change(within(row('Trang chủ')).getByRole('textbox', { name: 'Nhãn hiển thị' }), {
      target: { value: 'Nhà' },
    });
    save();
    await screen.findByText('Đã lưu menu.');
    // Lưu xong invalidate getNavigation ⇒ refetch (lỗi 500).
    await waitFor(() => {
      expect(gets).toBe(2);
    });
    fireEvent.change(within(row('Giới thiệu')).getByRole('textbox', { name: 'Nhãn hiển thị' }), {
      target: { value: 'Về' },
    });

    expect(screen.queryByRole('button', { name: 'Thử lại' })).not.toBeInTheDocument();
    expect(within(row('Trang chủ')).getByRole('textbox', { name: 'Nhãn hiển thị' })).toHaveValue(
      'Nhà',
    );
    expect(within(row('Giới thiệu')).getByRole('textbox', { name: 'Nhãn hiển thị' })).toHaveValue(
      'Về',
    );
  });

  it('mở lại màn: luôn GET bản mới (không dùng cache có version cũ)', async () => {
    const { bodies, getCount, queryClient, unmount } = setup({
      menus: [autoMenu, { version: 9, isDirty: true, items: [item('page:p-contact')] }],
    });
    await screen.findByText('Trang chủ');
    unmount();
    // gcTime 0: cache bị bỏ ngay sau khi rời màn (GC chạy ở nhịp timer kế tiếp).
    await waitFor(() => {
      expect(
        queryClient.getQueryCache().find({ queryKey: getGetNavigationQueryKey('shop-1') }),
      ).toBeUndefined();
    });

    renderWithQuery(<NavigationEditor shopId="shop-1" />, { queryClient });
    await waitFor(() => {
      expect(rowNames()).toEqual(['Liên hệ']);
    });
    expect(getCount()).toBe(2);
    save();
    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]?.version).toBe(9);
  });

  it('thay đổi trong lúc đang lưu: không báo "Đã lưu menu."', async () => {
    let release: () => void = () => undefined;
    setup();
    server.use(
      http.put('*/api/shops/:shopId/website/navigation', async () => {
        await new Promise<void>((resolve) => {
          release = resolve;
        });
        return HttpResponse.json({ version: 1, isDirty: true });
      }),
    );
    await screen.findByText('Trang chủ');

    save();
    await screen.findByRole('button', { name: 'Đang lưu...' });
    fireEvent.change(within(row('Trang chủ')).getByRole('textbox', { name: 'Nhãn hiển thị' }), {
      target: { value: 'Nhà' },
    });
    release();

    await screen.findByRole('button', { name: 'Lưu menu' });
    expect(screen.queryByText('Đã lưu menu.')).not.toBeInTheDocument();
  });
});

describe('NavigationEditor — sắp xếp bằng bàn phím (dnd-kit KeyboardSensor)', () => {
  it('Space nhấc, ↑ đổi chỗ, Space thả ⇒ thứ tự mới được lưu', async () => {
    const { bodies } = setup();
    await screen.findByText('Trang chủ');

    await pickUp('Giới thiệu');
    await press('ArrowUp');
    await press('Space');

    await waitFor(() => {
      expect(rowNames()).toEqual(['Giới thiệu', 'Trang chủ', 'Khuyến mãi', 'Liên hệ']);
    });
    save();
    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]?.items.map((i) => i.key)).toEqual([
      'page:p-about',
      'page:p-home',
      'page:p-off',
      'page:p-contact',
    ]);
  });

  it('→ lồng vào mục phía trên (cấp 2), ← đưa ra lại', async () => {
    const { bodies } = setup();
    await screen.findByText('Trang chủ');

    await pickUp('Giới thiệu');
    await press('ArrowRight');
    await press('Space');

    save();
    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]?.items.map((i) => i.key)).toEqual([
      'page:p-home',
      'page:p-off',
      'page:p-contact',
    ]);
    expect(bodies[0]?.items[0]?.children.map((c) => c.key)).toEqual(['page:p-about']);

    await pickUp('Giới thiệu');
    await press('ArrowLeft');
    await press('Space');
    save();
    await waitFor(() => {
      expect(bodies).toHaveLength(2);
    });
    expect(bodies[1]?.items.map((i) => i.key)).toEqual([
      'page:p-home',
      'page:p-about',
      'page:p-off',
      'page:p-contact',
    ]);
  });

  it('chặn lồng cấp 4: mục có con không xuống quá cấp 2', async () => {
    const deep: NavigationDto = {
      version: 1,
      isDirty: false,
      items: [
        item('page:p-home', {
          children: [{ ...item('page:p-about'), children: [grand('page:p-contact')] }],
        }),
        item('custom', {
          label: 'Facebook',
          url: 'https://facebook.com/spa',
          children: [{ ...item('page:p-off'), children: [] }],
        }),
      ],
    };
    const { bodies } = setup({ menus: [deep] });
    await screen.findByText('Facebook');

    await pickUp('Facebook');
    // Ngay dưới "Liên hệ" (cấp 3): bình thường → → → sẽ thành cấp 4 cho con của nó. Phải dừng ở cấp 2.
    await press('ArrowRight');
    await press('ArrowRight');
    await press('ArrowRight');
    await press('Space');

    save();
    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    const home = bodies[0]?.items[0];
    expect(bodies[0]?.items).toHaveLength(1);
    expect(home?.children.map((c) => c.key)).toEqual(['page:p-about', 'custom']);
    expect(home?.children[1]?.children).toEqual([
      { key: 'page:p-off', label: null, url: null, hidden: false },
    ]);
    expect(home?.children[0]?.children.map((c) => c.key)).toEqual(['page:p-contact']);
  });
});
