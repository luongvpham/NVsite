import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { treeEnvelopeSchema } from '@vsite/builder-components';
import { createEditorStore, PageKind, type EditorStore } from '@vsite/builder-core';
import { server } from '../../test/msw-server';
import { renderWithQuery } from '../../test/render-with-query';
import { SitePreview } from './site-preview';

type Target = { kind: string; [k: string]: unknown };

function heroTree(title: string, target?: Target) {
  return {
    schemaVersion: 1,
    id: 'root',
    type: 'Page',
    children: [
      {
        id: 'c_hero1',
        type: 'Hero',
        variant: 'Hero01',
        props: { title, ...(target ? { cta: { label: 'Đi', target } } : {}) },
      },
    ],
  };
}

const pageRow = (over: Record<string, unknown>) => ({
  id: 'x',
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
  updatedAt: '2026-11-11T00:00:00Z',
  ...over,
});

const pages = [
  pageRow({ id: 'p-home', slug: '', title: 'Trang chủ', isHomePage: true }),
  pageRow({ id: 'p-about', slug: 'gioi-thieu', title: 'Giới thiệu' }),
  pageRow({ id: 'p-off', slug: 'tat', title: 'Tắt', isEnabled: false }),
  pageRow({ id: 'p-404', kind: 'System', systemType: 'NotFound', slug: null, title: '404' }),
];

const drafts: Record<string, unknown> = {
  'p-about': heroTree('Nội dung trang giới thiệu'),
  'p-off': heroTree('Nội dung trang tắt'),
  'p-404': heroTree('Không tìm thấy trang này'),
};

const website = {
  id: 'w1',
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

let writes: string[];

function setup(homeTree: unknown, over: { pagesStatus?: number } = {}) {
  const store: EditorStore = createEditorStore();
  store.getState().loadDraft(treeEnvelopeSchema.parse(homeTree), PageKind.Composable);
  server.use(
    http.get('*/api/shops/:shopId/website', () => HttpResponse.json(website)),
    http.get('*/api/shops/:shopId/website/pages', () =>
      over.pagesStatus
        ? HttpResponse.json({ status: over.pagesStatus, title: 'x' }, { status: over.pagesStatus })
        : HttpResponse.json(pages),
    ),
    http.get('*/api/shops/:shopId/website/pages/:pageId/draft', ({ params }) => {
      const tree = drafts[params.pageId as string];
      return tree
        ? HttpResponse.json({
            tree,
            version: 1,
            isDirty: false,
            updatedAt: '2026-11-11T00:00:00Z',
            page: { id: params.pageId, kind: 'Composable', systemType: null },
          })
        : HttpResponse.json({ status: 404, title: 'x', error_code: 'PAGE_NOT_FOUND' }, { status: 404 });
    }),
    http.get('*/api/shops/:shopId', () =>
      HttpResponse.json({
        id: 'shop-1',
        name: 'Spa ABC',
        slug: 'spa-abc',
        kind: 'Hosted',
        externalUrl: null,
        status: 'Active',
        logoId: 'l1',
        logoUrl: '/media/s/logo.png',
      }),
    ),
    http.get('*/api/shops/:shopId/media/assets', () => HttpResponse.json([])),
  );
  const onClose = vi.fn();
  const utils = renderWithQuery(<SitePreview shopId="shop-1" pageId="p-home" store={store} onClose={onClose} />);
  return { store, onClose, ...utils };
}

beforeEach(() => {
  writes = [];
  server.events.on('request:start', ({ request }) => {
    if (request.method !== 'GET') writes.push(`${request.method} ${request.url}`);
  });
});
afterEach(() => {
  server.events.removeAllListeners();
  vi.restoreAllMocks();
});

describe('SitePreview — xem trước toàn trang (WEBSITE-003 T4)', () => {
  it('hiện khung Header (tên + logo) và nội dung trang đang sửa; không có request ghi nào', async () => {
    setup(heroTree('Chào khách'));
    const dialog = await screen.findByRole('dialog', { name: 'Xem trước trang' });
    expect(await within(dialog).findByText('Chào khách')).toBeInTheDocument();
    expect(dialog.querySelector('header')).toHaveTextContent('Spa ABC');
    expect(dialog.querySelector('header img')).toHaveAttribute('src', '/media/s/logo.png');
    expect(dialog.querySelector('footer')).toHaveTextContent('Spa ABC');
    expect(writes).toEqual([]);
  });

  it('đang tải → "Đang tải trang" trong khung', () => {
    setup(heroTree('x'));
    expect(screen.getByText('Đang tải trang')).toBeInTheDocument();
  });

  it('đóng bằng nút và bằng Escape', async () => {
    const { onClose } = setup(heroTree('Nội dung'));
    await screen.findByText('Nội dung');
    fireEvent.click(screen.getByRole('button', { name: 'Đóng xem trước' }));
    expect(onClose).toHaveBeenCalledTimes(1);
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it('thay đổi chưa autosave vẫn hiện (đọc từ store, không từ server) — R5', async () => {
    const { store } = setup(heroTree('Bản cũ'));
    store.getState().dispatch({ type: 'update', nodeId: 'c_hero1', path: ['title'], value: 'Bản đang sửa' });
    expect(await screen.findByText('Bản đang sửa')).toBeInTheDocument();
    expect(screen.queryByText('Bản cũ')).not.toBeInTheDocument();
    expect(writes).toEqual([]);
  });

  it('CTA tới trang khác → nạp draft trang đó; "← Trang trước" quay lại', async () => {
    setup(heroTree('Trang chủ nội dung', { kind: 'page', pageId: 'p-about' }));
    fireEvent.click(await screen.findByRole('link', { name: 'Đi' }));
    expect(await screen.findByText('Nội dung trang giới thiệu')).toBeInTheDocument();
    expect(screen.queryByText('Trang chủ nội dung')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: '← Trang trước' }));
    expect(await screen.findByText('Trang chủ nội dung')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '← Trang trước' })).not.toBeInTheDocument();
  });

  it('click link không điều hướng thật (R4)', async () => {
    setup(heroTree('x', { kind: 'page', pageId: 'p-about' }));
    const link = await screen.findByRole('link', { name: 'Đi' });
    const notPrevented = fireEvent.click(link);
    expect(notPrevented).toBe(false); // preventDefault đã được gọi
  });

  it('trang đích đang tắt → NotFound + dải "đang tắt"', async () => {
    setup(heroTree('x', { kind: 'page', pageId: 'p-off' }));
    fireEvent.click(await screen.findByRole('link', { name: 'Đi' }));
    expect(await screen.findByText('Không tìm thấy trang này')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Trang này đang tắt — khách sẽ thấy trang 404');
    expect(screen.queryByText('Nội dung trang tắt')).not.toBeInTheDocument();
  });

  it('systemPage chưa có trang → NotFound + dải báo', async () => {
    setup(heroTree('x', { kind: 'systemPage', systemType: 'Booking' }));
    fireEvent.click(await screen.findByRole('link', { name: 'Đi' }));
    expect(await screen.findByText('Không tìm thấy trang này')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('chưa được tạo');
  });

  it('link tới trang đã xoá → dải báo, ở lại trang hiện tại', async () => {
    setup(heroTree('Ở lại đây', { kind: 'page', pageId: 'ghost' }));
    fireEvent.click(await screen.findByRole('link', { name: 'Đi' }));
    expect(await screen.findByRole('status')).toHaveTextContent('Liên kết trỏ tới trang đã xoá');
    expect(screen.getByText('Ở lại đây')).toBeInTheDocument();
  });

  it('external mở tab mới (noopener) và không đổi trang hiện tại', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    setup(heroTree('Giữ nguyên', { kind: 'external', url: 'https://example.com/a' }));
    fireEvent.click(await screen.findByRole('link', { name: 'Đi' }));
    expect(open).toHaveBeenCalledWith('https://example.com/a', '_blank', 'noopener,noreferrer');
    expect(screen.getByText('Giữ nguyên')).toBeInTheDocument();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('lỗi listPages → báo lỗi + Thử lại', async () => {
    setup(heroTree('x'), { pagesStatus: 500 });
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    server.use(http.get('*/api/shops/:shopId/website/pages', () => HttpResponse.json(pages)));
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    await waitFor(() => {
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });
    expect(await screen.findByRole('dialog')).toHaveTextContent('Spa ABC');
  });

  it('trang đích lỗi tải → báo lỗi trong thân trang, khung vẫn còn', async () => {
    setup(heroTree('x', { kind: 'page', pageId: 'p-about' }));
    server.use(
      http.get('*/api/shops/:shopId/website/pages/:pageId/draft', () =>
        HttpResponse.json({ status: 404, title: 'x', error_code: 'PAGE_NOT_FOUND' }, { status: 404 }),
      ),
    );
    fireEvent.click(await screen.findByRole('link', { name: 'Đi' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Không tìm thấy trang');
    expect(screen.getByRole('dialog').querySelector('header')).toHaveTextContent('Spa ABC');
  });
});
