import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { getListShopsQueryKey, type PageDto, type ShopSummaryDto, type WebsiteDto } from '@vsite/api-sdk';
import { useSessionStore } from '../stores/session-store';
import { renderApp } from '../test/render-app';
import { server } from '../test/msw-server';

afterEach(() => {
  vi.unstubAllEnvs();
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
  currentPublicationId: null,
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

describe('Website — lối vào màn Menu (WEBSITE-007 F4)', () => {
  it('Owner: tổng quan có link "Sắp menu" → mở màn Menu', async () => {
    asRole('Owner');
    server.use(
      http.get('*/api/shops/:shopId/website', () => HttpResponse.json(website)),
      http.get('*/api/shops/:shopId/website/pages', () => HttpResponse.json(seededPages)),
      http.get('*/api/shops/:shopId/website/publications', () => HttpResponse.json([])),
      http.get('*/api/shops/:shopId/website/navigation', () =>
        HttpResponse.json({
          version: null,
          isDirty: true,
          items: [{ key: 'page:p-home', label: null, url: null, hidden: false, children: [] }],
        }),
      ),
    );
    renderApp('/shops/shop-1/website');

    const link = await screen.findByRole('link', { name: 'Sắp menu' });
    expect(link).toHaveAttribute('href', '/shops/shop-1/website/menu');
    fireEvent.click(link);

    expect(await screen.findByRole('heading', { name: 'Menu website' })).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Kéo Trang chủ' })).toBeInTheDocument();
  });

  it('Manager mở thẳng URL menu → "Chỉ chủ shop", không gọi API menu', async () => {
    asRole('Manager');
    let navigationCalls = 0;
    server.use(
      http.get('*/api/shops/:shopId/website/navigation', () => {
        navigationCalls += 1;
        return HttpResponse.json({ version: null, isDirty: true, items: [] });
      }),
    );
    renderApp('/shops/shop-1/website/menu');
    expect(await screen.findByText(/Chỉ chủ shop/)).toBeInTheDocument();
    expect(navigationCalls).toBe(0);
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

describe('Website — trạng thái xuất bản', () => {
  const publications = [
    { id: 'pub-2', version: 2, publishedAt: '2026-10-21T17:30:00Z', publishedByUserId: null, note: null, isCurrent: true },
    { id: 'pub-1', version: 1, publishedAt: '2026-10-20T10:00:00Z', publishedByUserId: null, note: null, isCurrent: false },
  ];

  function serve(w: WebsiteDto, pubs: unknown[] = publications) {
    asRole('Owner');
    server.use(
      http.get('*/api/shops/:shopId', () =>
        HttpResponse.json({ id: 'shop-1', name: 'Spa ABC', slug: 'spa-abc', kind: 'Hosted', externalUrl: null, status: 'Active', logoId: null, logoUrl: null }),
      ),
      http.get('*/api/shops/:shopId/website', () => HttpResponse.json(w)),
      http.get('*/api/shops/:shopId/website/pages', () => HttpResponse.json(seededPages)),
      http.get('*/api/shops/:shopId/website/publications', () => HttpResponse.json(pubs)),
    );
  }

  it('Draft chưa xuất bản → "Chưa xuất bản", không có link Mở site, có nút Xuất bản + "Chưa có bản nào"', async () => {
    serve(website, []);
    renderApp('/shops/shop-1/website');
    expect(await screen.findByText('Chưa xuất bản')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Xuất bản' })).toBeInTheDocument();
    expect(await screen.findByText('Chưa có bản nào')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Mở site' })).not.toBeInTheDocument();
  });

  it('Published → "Đang chạy bản v2" + link Mở site (tab mới)', async () => {
    serve({ ...website, status: 'Published', currentPublicationId: 'pub-2' });
    renderApp('/shops/shop-1/website');
    expect(await screen.findByText('Đang chạy bản v2')).toBeInTheDocument();
    const link = screen.getByRole('link', { name: 'Mở site' });
    expect(link).toHaveAttribute('href', 'http://spa-abc.vsite.local:3000');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link.getAttribute('rel')).toContain('noopener');
  });

  it('R8: Suspended → ghi rõ đình chỉ, vẫn có nút Xuất bản', async () => {
    serve({ ...website, status: 'Suspended', currentPublicationId: 'pub-2' });
    renderApp('/shops/shop-1/website');
    expect(await screen.findByText(/Đang bị đình chỉ — site công khai không hiển thị nội dung/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Xuất bản' })).toBeEnabled();
  });

  it('bấm Xuất bản → mở dialog; xuất bản xong nạp lại trạng thái', async () => {
    let published = false;
    serve(website, []);
    server.use(
      http.get('*/api/shops/:shopId/website', () =>
        HttpResponse.json(published ? { ...website, status: 'Published', currentPublicationId: 'pub-1' } : website),
      ),
      http.get('*/api/shops/:shopId/website/publications', () =>
        HttpResponse.json(published ? [{ ...publications[1], isCurrent: true }] : []),
      ),
      http.post('*/api/shops/:shopId/website/publications', () => {
        published = true;
        return HttpResponse.json({ ...publications[1], isCurrent: true }, { status: 201 });
      }),
    );
    renderApp('/shops/shop-1/website');
    fireEvent.click(await screen.findByRole('button', { name: 'Xuất bản' }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Xuất bản' }));
    expect(await screen.findByText('Đang chạy bản v1')).toBeInTheDocument();
  });

  it('S6: lần xuất bản đầu → dialog thành công có link "Mở site" ngay, không chờ getWebsite refetch', async () => {
    serve(website, []);
    let releaseWebsite: () => void = () => undefined;
    let published = false;
    server.use(
      // Sau publish, GET website treo — khối trạng thái vẫn là "chưa xuất bản" (hasPublication = false).
      http.get('*/api/shops/:shopId/website', async () => {
        if (published) await new Promise<void>((resolve) => { releaseWebsite = resolve; });
        return HttpResponse.json(website);
      }),
      http.post('*/api/shops/:shopId/website/publications', () => {
        published = true;
        return HttpResponse.json({ ...publications[1], isCurrent: true }, { status: 201 });
      }),
    );
    renderApp('/shops/shop-1/website');
    fireEvent.click(await screen.findByRole('button', { name: 'Xuất bản' }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Xuất bản' }));
    expect(await within(dialog).findByText(/Đã xuất bản bản v1/)).toBeInTheDocument();
    const link = within(dialog).getByRole('link', { name: 'Mở site' });
    expect(link).toHaveAttribute('href', 'http://spa-abc.vsite.local:3000');
    expect(link).toHaveAttribute('target', '_blank');
    expect(screen.getByText('Chưa xuất bản')).toBeInTheDocument(); // refetch chưa về
    releaseWebsite();
  });

  it('rollback ở lịch sử → trạng thái đổi sang bản mới', async () => {
    let current = 'pub-2';
    serve({ ...website, status: 'Published', currentPublicationId: 'pub-2' });
    server.use(
      http.get('*/api/shops/:shopId/website', () =>
        HttpResponse.json({ ...website, status: 'Published', currentPublicationId: current }),
      ),
      http.get('*/api/shops/:shopId/website/publications', () =>
        HttpResponse.json(publications.map((p) => ({ ...p, isCurrent: p.id === current }))),
      ),
      http.post('*/api/shops/:shopId/website/publications/:id/rollback', ({ params }) => {
        current = params.id as string;
        return HttpResponse.json({ ...publications[1], isCurrent: true });
      }),
    );
    renderApp('/shops/shop-1/website');
    expect(await screen.findByText('Đang chạy bản v2')).toBeInTheDocument();
    fireEvent.click(await screen.findByRole('button', { name: 'Quay về bản này' }));
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận quay về' }));
    expect(await screen.findByText('Đang chạy bản v1')).toBeInTheDocument();
  });

  it('W3: không cấu hình origin site (env rỗng) → Published vẫn không có link Mở site', async () => {
    vi.stubEnv('VITE_SHOP_SITE_ORIGIN_TEMPLATE', '');
    serve({ ...website, status: 'Published', currentPublicationId: 'pub-2' });
    renderApp('/shops/shop-1/website');
    expect(await screen.findByText('Đang chạy bản v2')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Mở site' })).not.toBeInTheDocument();
  });

  it('R6 chéo: đang rollback → nút Xuất bản ở khối trạng thái disabled', async () => {
    serve({ ...website, status: 'Published', currentPublicationId: 'pub-2' });
    let release: () => void = () => undefined;
    server.use(
      http.post('*/api/shops/:shopId/website/publications/:id/rollback', async () => {
        await new Promise<void>((resolve) => { release = resolve; });
        return HttpResponse.json(publications[1]);
      }),
    );
    renderApp('/shops/shop-1/website');
    expect(await screen.findByText('Đang chạy bản v2')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Xuất bản' })).toBeEnabled();
    fireEvent.click(await screen.findByRole('button', { name: 'Quay về bản này' }));
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận quay về' }));
    await waitFor(() => { expect(screen.getByRole('button', { name: 'Xuất bản' })).toBeDisabled(); });
    release();
    await waitFor(() => { expect(screen.getByRole('button', { name: 'Xuất bản' })).toBeEnabled(); });
  });
});
