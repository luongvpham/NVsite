import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';
import { getListShopsQueryKey, type PageDto, type ShopSummaryDto, type WebsiteDto } from '@vsite/api-sdk';
import { useSessionStore } from '../stores/session-store';
import { renderApp } from '../test/render-app';
import { server } from '../test/msw-server';

afterEach(() => {
  useSessionStore.getState().clearSession();
});

function shop(roleCode: string): ShopSummaryDto {
  return { id: 'shop-1', name: 'Spa ABC', slug: 'spa-abc', kind: 'Hosted', status: 'Active', roleCode, logoUrl: null };
}

const website: WebsiteDto = {
  id: 'w-1',
  status: 'Draft',
  defaultLocale: 'vi',
  locales: ['vi'],
  defaultSeoTitle: null,
  defaultSeoDescription: null,
  defaultOgImageId: null,
  faviconAssetId: null,
  trackingSnippets: {},
  homePageId: 'p-home',
};

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
    updatedAt: '2026-10-21T00:00:00Z',
    ...overrides,
  };
}

/** Bộ trang BE seed khi tạo website (brief "Tạo website"): trang chủ + Contact + NotFound. */
const seededPages: PageDto[] = [
  page({ id: 'p-home', slug: '', title: 'Trang chủ', isHomePage: true, deletable: false, sortOrder: 0 }),
  page({ id: 'p-contact', kind: 'System', systemType: 'Contact', slug: 'lien-he', title: 'Liên hệ', deletable: false, sortOrder: 1 }),
  page({ id: 'p-404', kind: 'System', systemType: 'NotFound', slug: null, title: 'Không tìm thấy', noIndex: true, deletable: false, sortOrder: 2 }),
];

function asRole(roleCode: string) {
  useSessionStore.getState().setSession({ accessToken: 't' });
  server.use(http.get('*/api/shops', () => HttpResponse.json([shop(roleCode)])));
}

const notFound = () =>
  HttpResponse.json({ status: 404, title: 'x', error_code: 'WEBSITE_NOT_FOUND' }, { status: 404 });

describe('Website — quyền (R4)', () => {
  it('Owner thấy link Website ở màn shop', async () => {
    asRole('Owner');
    renderApp('/shops/shop-1');
    expect(await screen.findByRole('link', { name: 'Website' })).toHaveAttribute('href', '/shops/shop-1/website');
  });

  it('Manager không thấy link Website', async () => {
    asRole('Manager');
    const { queryClient } = renderApp('/shops/shop-1');
    // Link phụ thuộc roleCode từ listShops — chờ query đó về rồi mới khẳng định vắng mặt.
    await waitFor(() => {
      expect(queryClient.getQueryState(getListShopsQueryKey())?.status).toBe('success');
    });
    expect(screen.queryByRole('link', { name: 'Website' })).not.toBeInTheDocument();
  });

  it('Manager mở thẳng URL → "Chỉ chủ shop", không gọi API website', async () => {
    asRole('Manager');
    let websiteCalls = 0;
    server.use(
      http.get('*/api/shops/:shopId/website', () => {
        websiteCalls += 1;
        return HttpResponse.json(website);
      }),
      http.get('*/api/shops/:shopId/website/pages', () => {
        websiteCalls += 1;
        return HttpResponse.json([]);
      }),
    );
    renderApp('/shops/shop-1/website');
    // Gate chỉ hiện sau khi listShops về; con của gate chưa từng mount → không có request website.
    expect(await screen.findByText(/Chỉ chủ shop/)).toBeInTheDocument();
    expect(websiteCalls).toBe(0);
  });
});

describe('Website — tạo website', () => {
  it('đang tải → hiện loading', async () => {
    asRole('Owner');
    server.use(http.get('*/api/shops/:shopId/website', () => new Promise(() => undefined)));
    renderApp('/shops/shop-1/website');
    expect(await screen.findByText('Đang tải website...')).toBeInTheDocument();
  });

  it('404 WEBSITE_NOT_FOUND → empty + nút tạo; tạo xong hiện danh sách 3 trang seed', async () => {
    asRole('Owner');
    let created = false;
    server.use(
      http.get('*/api/shops/:shopId/website', () => (created ? HttpResponse.json(website) : notFound())),
      http.get('*/api/shops/:shopId/website/pages', () => HttpResponse.json(created ? seededPages : [])),
      http.post('*/api/shops/:shopId/website', () => {
        created = true;
        return HttpResponse.json(website, { status: 201 });
      }),
    );
    renderApp('/shops/shop-1/website');

    fireEvent.click(await screen.findByRole('button', { name: 'Tạo website' }));

    const list = await screen.findByRole('list', { name: 'Danh sách trang' });
    expect(within(list).getAllByRole('listitem')).toHaveLength(3);
    expect(within(list).getByText('Trang chủ')).toBeInTheDocument();
    expect(within(list).getByText('Liên hệ')).toBeInTheDocument();
  });

  it('422 WEBSITE_REQUIRES_HOSTED_SHOP → giải thích, vẫn ở màn tạo', async () => {
    asRole('Owner');
    server.use(
      http.get('*/api/shops/:shopId/website', notFound),
      http.post('*/api/shops/:shopId/website', () =>
        HttpResponse.json({ status: 422, title: 'x', error_code: 'WEBSITE_REQUIRES_HOSTED_SHOP' }, { status: 422 }),
      ),
    );
    renderApp('/shops/shop-1/website');
    fireEvent.click(await screen.findByRole('button', { name: 'Tạo website' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Website trên vsite');
    expect(screen.getByRole('button', { name: 'Tạo website' })).toBeInTheDocument();
  });

  it('409 WEBSITE_ALREADY_EXISTS → tải lại website, hiện danh sách', async () => {
    asRole('Owner');
    let exists = false;
    server.use(
      http.get('*/api/shops/:shopId/website', () => (exists ? HttpResponse.json(website) : notFound())),
      http.get('*/api/shops/:shopId/website/pages', () => HttpResponse.json(seededPages)),
      http.post('*/api/shops/:shopId/website', () => {
        exists = true;
        return HttpResponse.json({ status: 409, title: 'x', error_code: 'WEBSITE_ALREADY_EXISTS' }, { status: 409 });
      }),
    );
    renderApp('/shops/shop-1/website');
    fireEvent.click(await screen.findByRole('button', { name: 'Tạo website' }));
    expect(await screen.findByRole('list', { name: 'Danh sách trang' })).toBeInTheDocument();
  });

  it('lỗi 500 → báo lỗi + Thử lại', async () => {
    asRole('Owner');
    let fail = true;
    server.use(
      http.get('*/api/shops/:shopId/website', () =>
        fail ? HttpResponse.json({ status: 500, title: 'x' }, { status: 500 }) : HttpResponse.json(website),
      ),
      http.get('*/api/shops/:shopId/website/pages', () => HttpResponse.json(seededPages)),
    );
    renderApp('/shops/shop-1/website');
    expect(await screen.findByRole('alert')).toHaveTextContent('Có lỗi xảy ra');
    fail = false;
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(await screen.findByRole('list', { name: 'Danh sách trang' })).toBeInTheDocument();
  });
});

describe('Website — danh sách trang', () => {
  it('chỉ có trang chủ + System → gợi ý tạo trang; nhãn loại trang + trạng thái', async () => {
    asRole('Owner');
    server.use(
      http.get('*/api/shops/:shopId/website', () => HttpResponse.json(website)),
      http.get('*/api/shops/:shopId/website/pages', () =>
        HttpResponse.json([...seededPages.slice(0, 2), { ...seededPages[2], isEnabled: false, isDirty: true }]),
      ),
    );
    renderApp('/shops/shop-1/website');

    expect(await screen.findByText(/Chưa có trang nội dung nào/)).toBeInTheDocument();
    const rows = within(screen.getByRole('list', { name: 'Danh sách trang' })).getAllByRole('listitem');
    expect(rows[0]).toHaveTextContent('Trang chủ');
    expect(rows[1]).toHaveTextContent('Trang hệ thống');
    expect(rows[2]).toHaveTextContent('Đang tắt');
    expect(rows[2]).toHaveTextContent('Có thay đổi chưa xuất bản');
  });

  it('có trang Composable → không hiện gợi ý; lỗi tải trang → báo lỗi', async () => {
    asRole('Owner');
    server.use(
      http.get('*/api/shops/:shopId/website', () => HttpResponse.json(website)),
      http.get('*/api/shops/:shopId/website/pages', () =>
        HttpResponse.json([...seededPages, page({ id: 'p-1', slug: 'dich-vu', title: 'Dịch vụ', sortOrder: 3 })]),
      ),
    );
    const { unmount } = renderApp('/shops/shop-1/website');
    expect(await screen.findByText('Dịch vụ')).toBeInTheDocument();
    expect(screen.queryByText(/Chưa có trang nội dung nào/)).not.toBeInTheDocument();
    unmount();

    server.use(
      http.get('*/api/shops/:shopId/website/pages', () =>
        HttpResponse.json({ status: 403, title: 'x', error_code: 'WEBSITE_OWNER_REQUIRED' }, { status: 403 }),
      ),
    );
    renderApp('/shops/shop-1/website');
    expect(await screen.findByRole('alert')).toHaveTextContent('Chỉ chủ shop');
  });
});
