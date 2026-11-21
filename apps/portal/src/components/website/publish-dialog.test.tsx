import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRootRoute, createRoute, createRouter } from '@tanstack/react-router';
import { http, HttpResponse } from 'msw';
import { useState, type ComponentType } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { server } from '../../test/msw-server';
import {
  getGetNavigationQueryKey,
  getGetWebsiteQueryKey,
  getListPagesQueryKey,
  getListPublicationsQueryKey,
} from '@vsite/api-sdk';
import { PublishDialog, type PublishDialogProps } from './publish-dialog';

/** `Root`: thay dialog mặc định (open cố định) bằng component riêng — vd. harness tự đóng/mở. */
function renderDialog(props: Partial<PublishDialogProps> = {}, Root?: ComponentType) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: 3, retryDelay: 0 } },
  });
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
  const onOpenChange = vi.fn();
  const onSettled = vi.fn();
  const rootRoute = createRootRoute({
    component: () =>
      Root ? (
        <Root />
      ) : (
        <PublishDialog
          shopId="shop-1"
          open
          onOpenChange={onOpenChange}
          onSettled={onSettled}
          siteUrl="http://spa.vsite.local:3000"
          {...props}
        />
      ),
  });
  const editorRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/shops/$shopId/website/pages/$pageId',
    component: () => null,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([editorRoute]),
    history: createMemoryHistory({ initialEntries: ['/'] }),
  });
  const utils = render(
    <QueryClientProvider client={queryClient}>
      {/* Router test riêng, không phải routeTree thật của app — ép kiểu để khỏi vướng Register. */}
      <RouterProvider router={router as never} />
    </QueryClientProvider>,
  );
  return { onOpenChange, onSettled, invalidate, unmount: utils.unmount };
}

const publication = {
  id: 'pub-1',
  version: 7,
  note: null,
  publishedAt: '2026-11-19T00:00:00Z',
  publishedByUserId: 'u-1',
  isCurrent: true,
};

const blocked = {
  status: 422,
  title: 'x',
  error_code: 'PUBLISH_BLOCKED',
  params: {
    truncated: true,
    errors: [
      { pageId: 'p-1', pageTitle: 'Giới thiệu', path: '/seoTitle', code: 'SEO_TITLE_MISSING', message: 'Thiếu tiêu đề SEO' },
      { pageId: null, pageTitle: null, path: '/favicon', code: 'FAVICON_MISSING', message: 'Thiếu favicon' },
      { pageId: 'p-2', pageTitle: 'Dịch vụ', path: '/tree/a', code: 'REQUIRED_PROP_MISSING', message: 'Thiếu tên' },
      { pageId: 'p-1', pageTitle: 'Giới thiệu', path: '/tree/b', code: 'REQUIRED_PROP_MISSING', message: 'Thiếu ảnh' },
    ],
  },
};

const POST_URL = '*/api/shops/:shopId/website/publications';

async function openDialog() {
  return screen.findByRole('dialog');
}

describe('PublishDialog', () => {
  it('201: gửi note đã trim, hiện "Đã xuất bản bản v7" + link Mở site, làm mới 3 query, gọi onSettled', async () => {
    let body: unknown;
    server.use(
      http.post(POST_URL, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(publication, { status: 201 });
      }),
    );
    const { onSettled, invalidate } = renderDialog();
    await openDialog();

    fireEvent.change(screen.getByLabelText('Ghi chú (tuỳ chọn)'), { target: { value: '  đợt 2 ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Xuất bản' }));

    expect(await screen.findByText('Đã xuất bản bản v7')).toBeInTheDocument();
    expect(body).toEqual({ note: 'đợt 2' });
    expect(screen.getByRole('link', { name: 'Mở site' })).toHaveAttribute('href', 'http://spa.vsite.local:3000');
    expect(onSettled).toHaveBeenCalledTimes(1);
    const keys = invalidate.mock.calls.map(([filters]) => filters?.queryKey);
    expect(keys).toEqual([
      getGetWebsiteQueryKey('shop-1'),
      getListPagesQueryKey('shop-1'),
      getListPublicationsQueryKey('shop-1'),
      getGetNavigationQueryKey('shop-1'),
    ]);
  });

  it('không có siteUrl ⇒ không có link Mở site', async () => {
    server.use(http.post(POST_URL, () => HttpResponse.json(publication, { status: 201 })));
    renderDialog({ siteUrl: null });
    await openDialog();
    fireEvent.click(screen.getByRole('button', { name: 'Xuất bản' }));
    await screen.findByText('Đã xuất bản bản v7');
    expect(screen.queryByRole('link', { name: 'Mở site' })).not.toBeInTheDocument();
  });

  it('note toàn khoảng trắng ⇒ gửi note: null', async () => {
    let body: unknown;
    server.use(
      http.post(POST_URL, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(publication, { status: 201 });
      }),
    );
    renderDialog();
    await openDialog();
    fireEvent.change(screen.getByLabelText('Ghi chú (tuỳ chọn)'), { target: { value: '    ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Xuất bản' }));
    await screen.findByText('Đã xuất bản bản v7');
    expect(body).toEqual({ note: null });
  });

  it('note > 200 ký tự ⇒ báo lỗi, nút disabled, không gửi request', async () => {
    let calls = 0;
    server.use(
      http.post(POST_URL, () => {
        calls += 1;
        return HttpResponse.json(publication, { status: 201 });
      }),
    );
    renderDialog();
    await openDialog();
    fireEvent.change(screen.getByLabelText('Ghi chú (tuỳ chọn)'), { target: { value: 'a'.repeat(201) } });
    expect(screen.getByRole('button', { name: 'Xuất bản' })).toBeDisabled();
    expect(screen.getByText(/tối đa 200/i)).toBeInTheDocument();
    expect(calls).toBe(0);
  });

  it('R4: 422 PUBLISH_BLOCKED — lỗi website đứng đầu (không có "Mở trang"), nhóm theo trang, "còn lỗi khác"', async () => {
    server.use(http.post(POST_URL, () => HttpResponse.json(blocked, { status: 422 })));
    const { onSettled } = renderDialog();
    await openDialog();
    fireEvent.click(screen.getByRole('button', { name: 'Xuất bản' }));

    const list = await screen.findByRole('list', { name: 'Lỗi chặn xuất bản' });
    const groups = within(list).getAllByTestId('publish-error-group') as [HTMLElement, HTMLElement, HTMLElement];
    expect(groups).toHaveLength(3);
    expect(within(groups[0]).getByText('Thiếu favicon')).toBeInTheDocument();
    expect(within(groups[0]).queryByRole('link')).not.toBeInTheDocument();
    expect(within(groups[1]).getByText('Giới thiệu')).toBeInTheDocument();
    expect(within(groups[1]).getByText('Thiếu tiêu đề SEO')).toBeInTheDocument();
    expect(within(groups[1]).getByText('Thiếu ảnh')).toBeInTheDocument();
    expect(within(groups[1]).getByRole('link', { name: 'Mở trang' })).toHaveAttribute(
      'href',
      '/shops/shop-1/website/pages/p-1',
    );
    expect(within(groups[2]).getByText('Thiếu tên')).toBeInTheDocument();
    expect(screen.getByText('Còn lỗi khác chưa hiển thị — sửa các lỗi trên rồi xuất bản lại.')).toBeInTheDocument();
    expect(onSettled).toHaveBeenCalledTimes(1);
    expect(screen.queryByText(/Đã xuất bản bản/)).not.toBeInTheDocument();
  });

  it('R5: lỗi thuộc trang đang mở ⇒ hiện "trang này", không có link "Mở trang"', async () => {
    server.use(http.post(POST_URL, () => HttpResponse.json(blocked, { status: 422 })));
    renderDialog({ currentPageId: 'p-1' });
    await openDialog();
    fireEvent.click(screen.getByRole('button', { name: 'Xuất bản' }));

    const list = await screen.findByRole('list', { name: 'Lỗi chặn xuất bản' });
    const groups = within(list).getAllByTestId('publish-error-group') as [HTMLElement, HTMLElement, HTMLElement];
    expect(within(groups[1]).getByText('trang này')).toBeInTheDocument();
    expect(within(groups[1]).queryByRole('link', { name: 'Mở trang' })).not.toBeInTheDocument();
    expect(within(groups[2]).getByRole('link', { name: 'Mở trang' })).toBeInTheDocument();
  });

  it('R6 + 409: nút disabled khi đang chạy, đúng 1 request, không tự thử lại, hiện thông điệp PUBLISH_CONFLICT', async () => {
    let calls = 0;
    let release: () => void = () => {};
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.post(POST_URL, async () => {
        calls += 1;
        await gate;
        return HttpResponse.json({ status: 409, title: 'x', error_code: 'PUBLISH_CONFLICT' }, { status: 409 });
      }),
    );
    const { onSettled } = renderDialog();
    await openDialog();
    fireEvent.click(screen.getByRole('button', { name: 'Xuất bản' }));
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Đang xuất bản…' })).toBeDisabled();
    });
    fireEvent.click(screen.getByRole('button', { name: 'Đang xuất bản…' }));
    release();

    expect(await screen.findByRole('alert')).toHaveTextContent('Có thao tác khác vừa chạy');
    expect(calls).toBe(1);
    expect(onSettled).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('button', { name: 'Xuất bản' })).toBeEnabled();
  });

  it('lỗi khác (500) ⇒ hiện alert', async () => {
    server.use(
      http.post(POST_URL, () =>
        HttpResponse.json({ status: 500, title: 'x', error_code: 'INTERNAL_ERROR' }, { status: 500 }),
      ),
    );
    renderDialog();
    await openDialog();
    fireEvent.click(screen.getByRole('button', { name: 'Xuất bản' }));
    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });

  it('đang xuất bản: Huỷ disabled, Escape/✕ không gọi onOpenChange; onSettled vẫn chạy khi unmount giữa chừng', async () => {
    let release: () => void = () => {};
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.post(POST_URL, async () => {
        await gate;
        return HttpResponse.json(publication, { status: 201 });
      }),
    );
    const { onOpenChange, onSettled, unmount } = renderDialog();
    await openDialog();
    fireEvent.click(screen.getByRole('button', { name: 'Xuất bản' }));
    await screen.findByRole('button', { name: 'Đang xuất bản…' });

    expect(screen.getByRole('button', { name: 'Huỷ' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Đóng' })).not.toBeInTheDocument();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onOpenChange).not.toHaveBeenCalled();

    unmount();
    release();
    await waitFor(() => {
      expect(onSettled).toHaveBeenCalledTimes(1);
    });
  });

  it('màn thành công: Escape và ✕ gọi onOpenChange(false); nút "Xong" cũng đóng', async () => {
    server.use(http.post(POST_URL, () => HttpResponse.json(publication, { status: 201 })));
    const { onOpenChange } = renderDialog();
    await openDialog();
    fireEvent.click(screen.getByRole('button', { name: 'Xuất bản' }));
    await screen.findByText('Đã xuất bản bản v7');

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onOpenChange).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: 'Đóng' }));
    expect(onOpenChange).toHaveBeenCalledTimes(2);
    fireEvent.click(screen.getByRole('button', { name: 'Xong' }));
    expect(onOpenChange).toHaveBeenCalledTimes(3);
    expect(onOpenChange).toHaveBeenLastCalledWith(false);
  });

  it('F5-S3: đóng rồi mở lại ⇒ phiên mới — ghi chú rỗng, không còn "Đã xuất bản bản vN", form hiện lại', async () => {
    server.use(http.post(POST_URL, () => HttpResponse.json(publication, { status: 201 })));
    function Harness() {
      const [open, setOpen] = useState(false);
      return (
        <>
          <button type="button" onClick={() => { setOpen(true); }}>
            Mở dialog
          </button>
          <PublishDialog shopId="shop-1" open={open} onOpenChange={setOpen} siteUrl="http://spa.vsite.local:3000" />
        </>
      );
    }
    renderDialog({}, Harness);

    // Phiên 1: gõ ghi chú rồi đóng (Huỷ) — mở lại thì ô ghi chú rỗng.
    fireEvent.click(await screen.findByRole('button', { name: 'Mở dialog' }));
    await openDialog();
    fireEvent.change(screen.getByLabelText('Ghi chú (tuỳ chọn)'), { target: { value: 'nháp cũ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Huỷ' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Mở dialog' }));
    expect(await openDialog()).toBeInTheDocument();
    expect(screen.getByLabelText('Ghi chú (tuỳ chọn)')).toHaveValue('');

    // Phiên 2: xuất bản thành công rồi đóng (Xong) — mở lại thì về form, không còn kết quả cũ.
    fireEvent.change(screen.getByLabelText('Ghi chú (tuỳ chọn)'), { target: { value: 'đợt 3' } });
    fireEvent.click(screen.getByRole('button', { name: 'Xuất bản' }));
    await screen.findByText('Đã xuất bản bản v7');
    fireEvent.click(screen.getByRole('button', { name: 'Xong' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Mở dialog' }));
    await openDialog();
    expect(screen.queryByText(/Đã xuất bản bản v/)).not.toBeInTheDocument();
    expect(screen.getByLabelText('Ghi chú (tuỳ chọn)')).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Xuất bản' })).toBeEnabled();
  });

  it('nút Huỷ gọi onOpenChange(false)', async () => {
    const { onOpenChange } = renderDialog();
    await openDialog();
    fireEvent.click(screen.getByRole('button', { name: 'Huỷ' }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  describe('menu chưa publish (WEBSITE-007 F5)', () => {
    const NAV_URL = '*/api/shops/:shopId/website/navigation';
    const nav = (isDirty: boolean) => ({ version: 3, isDirty, items: [] });

    it('isDirty true ⇒ hiện "Menu có thay đổi chưa publish"', async () => {
      server.use(http.get(NAV_URL, () => HttpResponse.json(nav(true))));
      renderDialog();
      await openDialog();
      expect(await screen.findByText('Menu có thay đổi chưa publish')).toBeInTheDocument();
    });

    it('isDirty false ⇒ không hiện', async () => {
      let gets = 0;
      server.use(
        http.get(NAV_URL, () => {
          gets += 1;
          return HttpResponse.json(nav(false));
        }),
      );
      renderDialog();
      await openDialog();
      await waitFor(() => { expect(gets).toBe(1); });
      expect(screen.queryByText('Menu có thay đổi chưa publish')).not.toBeInTheDocument();
    });

    it('publish thành công ⇒ GET menu lần nữa, dấu chưa publish biến mất', async () => {
      let gets = 0;
      server.use(
        http.get(NAV_URL, () => {
          gets += 1;
          return HttpResponse.json(nav(gets === 1));
        }),
        http.post(POST_URL, () => HttpResponse.json(publication, { status: 201 })),
      );
      renderDialog();
      await openDialog();
      await screen.findByText('Menu có thay đổi chưa publish');
      fireEvent.click(screen.getByRole('button', { name: 'Xuất bản' }));
      await screen.findByText('Đã xuất bản bản v7');
      await waitFor(() => { expect(gets).toBe(2); });
      await waitFor(() => { expect(screen.queryByText('Menu có thay đổi chưa publish')).not.toBeInTheDocument(); });
    });
  });
});
