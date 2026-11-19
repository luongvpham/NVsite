import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { getGetWebsiteQueryKey, getListPagesQueryKey, getListPublicationsQueryKey, type PublicationDto } from '@vsite/api-sdk';
import { server } from '../../test/msw-server';
import { renderWithQuery } from '../../test/render-with-query';
import { PublicationHistory } from './publication-history';

function pub(overrides: Partial<PublicationDto>): PublicationDto {
  return {
    id: 'pub-x',
    version: 1,
    publishedAt: '2026-10-21T17:30:00Z',
    publishedByUserId: 'user-secret-id',
    note: null,
    isCurrent: false,
    ...overrides,
  };
}

const list: PublicationDto[] = [
  pub({ id: 'pub-3', version: 3, isCurrent: true, note: 'Bản mới nhất' }),
  pub({ id: 'pub-2', version: 2 }),
  pub({ id: 'pub-1', version: 1 }),
];

function row(version: number): HTMLElement {
  return screen.getByText(`v${version}`).closest('li') as HTMLElement;
}

function serveList(data: PublicationDto[] = list) {
  const counter = { gets: 0 };
  server.use(
    http.get('*/api/shops/:shopId/website/publications', () => {
      counter.gets += 1;
      return HttpResponse.json(data);
    }),
  );
  return counter;
}

function problem(status: number, code: string) {
  return HttpResponse.json({ status, title: 'x', error_code: code }, { status });
}

describe('PublicationHistory — hiển thị', () => {
  it('đang tải → loading', () => {
    server.use(http.get('*/api/shops/:shopId/website/publications', () => new Promise(() => undefined)));
    renderWithQuery(<PublicationHistory shopId="shop-1" />);
    expect(screen.getByText('Đang tải lịch sử xuất bản...')).toBeInTheDocument();
  });

  it('lỗi → báo lỗi + Thử lại tải lại được', async () => {
    let fail = true;
    server.use(
      http.get('*/api/shops/:shopId/website/publications', () =>
        fail ? HttpResponse.json({ status: 500, title: 'x' }, { status: 500 }) : HttpResponse.json(list),
      ),
    );
    renderWithQuery(<PublicationHistory shopId="shop-1" />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Có lỗi xảy ra');
    fail = false;
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(await screen.findByText('v3')).toBeInTheDocument();
  });

  it('rỗng → "Chưa có bản nào"', async () => {
    serveList([]);
    renderWithQuery(<PublicationHistory shopId="shop-1" />);
    expect(await screen.findByText('Chưa có bản nào')).toBeInTheDocument();
  });

  it('hàng: vN, giờ Việt Nam, ghi chú, nhãn "Đang chạy"; không hiện id người xuất bản; ẩn nút ở bản isCurrent (R7)', async () => {
    serveList();
    renderWithQuery(<PublicationHistory shopId="shop-1" />);
    await screen.findByText('v3');
    // 17:30Z = 00:30 ngày hôm sau theo Asia/Ho_Chi_Minh (UTC+7).
    expect(row(2)).toHaveTextContent('00:30');
    expect(row(2)).toHaveTextContent('22/10/2026');
    expect(row(3)).toHaveTextContent('Bản mới nhất');
    expect(row(3)).toHaveTextContent('Đang chạy');
    expect(within(row(3)).queryByRole('button', { name: 'Quay về bản này' })).not.toBeInTheDocument();
    expect(within(row(2)).getByRole('button', { name: 'Quay về bản này' })).toBeInTheDocument();
    expect(within(row(2)).queryByText('Đang chạy')).not.toBeInTheDocument();
    expect(screen.queryByText(/user-secret-id/)).not.toBeInTheDocument();
  });
});

describe('PublicationHistory — rollback', () => {
  it('xác nhận → POST đúng bản, nạp lại danh sách + website + trang', async () => {
    const counter = serveList();
    let posted: string | null = null;
    server.use(
      http.post('*/api/shops/:shopId/website/publications/:publicationId/rollback', ({ params }) => {
        posted = params.publicationId as string;
        return HttpResponse.json(list[1]);
      }),
    );
    const { queryClient } = renderWithQuery(<PublicationHistory shopId="shop-1" />);
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
    await screen.findByText('v2');

    fireEvent.click(within(row(2)).getByRole('button', { name: 'Quay về bản này' }));
    expect(posted).toBeNull(); // chưa xác nhận thì chưa gọi
    expect(screen.getByRole('alertdialog')).toHaveTextContent('v2');
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận quay về' }));

    await waitFor(() => { expect(posted).toBe('pub-2'); });
    await waitFor(() => { expect(counter.gets).toBe(2); });
    const keys = invalidate.mock.calls.map((c) => JSON.stringify(c[0]?.queryKey));
    expect(keys).toContain(JSON.stringify(getGetWebsiteQueryKey('shop-1')));
    expect(keys).toContain(JSON.stringify(getListPagesQueryKey('shop-1')));
    expect(keys).toContain(JSON.stringify(getListPublicationsQueryKey('shop-1')));
    await waitFor(() => { expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument(); });
  });

  it('Huỷ → không gọi POST', async () => {
    serveList();
    let posts = 0;
    server.use(
      http.post('*/api/shops/:shopId/website/publications/:publicationId/rollback', () => {
        posts += 1;
        return HttpResponse.json(list[1]);
      }),
    );
    renderWithQuery(<PublicationHistory shopId="shop-1" />);
    await screen.findByText('v2');
    fireEvent.click(within(row(2)).getByRole('button', { name: 'Quay về bản này' }));
    fireEvent.click(screen.getByRole('button', { name: 'Huỷ' }));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(posts).toBe(0);
  });

  it.each([
    [404, 'PUBLICATION_NOT_FOUND', 'Không tìm thấy bản xuất bản này'],
    [409, 'ROLLBACK_CONFLICT', 'Có thao tác khác vừa chạy'],
  ])('%i %s → thông điệp, nạp lại danh sách, đúng 1 POST, bấm lại được', async (status, code, text) => {
    const counter = serveList();
    let posts = 0;
    server.use(
      http.post('*/api/shops/:shopId/website/publications/:publicationId/rollback', () => {
        posts += 1;
        return posts === 1 ? problem(status, code) : HttpResponse.json(list[1]);
      }),
    );
    renderWithQuery(<PublicationHistory shopId="shop-1" />);
    await screen.findByText('v2');
    fireEvent.click(within(row(2)).getByRole('button', { name: 'Quay về bản này' }));
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận quay về' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(text);
    await waitFor(() => { expect(counter.gets).toBe(2); });
    // Không tự thử lại: chờ thêm một nhịp rồi vẫn 1 request.
    await new Promise((r) => setTimeout(r, 100));
    expect(posts).toBe(1);

    // Người dùng bấm lại thủ công → thử lại được.
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận quay về' }));
    await waitFor(() => { expect(posts).toBe(2); });
  });

  it('R6: đang rollback → mọi nút Quay về/Xác nhận disabled', async () => {
    serveList();
    let release: () => void = () => undefined;
    server.use(
      http.post('*/api/shops/:shopId/website/publications/:publicationId/rollback', async () => {
        await new Promise<void>((resolve) => { release = resolve; });
        return HttpResponse.json(list[1]);
      }),
    );
    renderWithQuery(<PublicationHistory shopId="shop-1" />);
    await screen.findByText('v2');
    fireEvent.click(within(row(2)).getByRole('button', { name: 'Quay về bản này' }));
    fireEvent.click(screen.getByRole('button', { name: 'Xác nhận quay về' }));

    const confirm = await screen.findByRole('button', { name: 'Đang quay về...' });
    expect(confirm).toBeDisabled();
    expect(within(row(1)).getByRole('button', { name: 'Quay về bản này' })).toBeDisabled();
    expect(within(row(2)).getByRole('button', { name: 'Quay về bản này' })).toBeDisabled();
    release();
    await waitFor(() => { expect(within(row(1)).getByRole('button', { name: 'Quay về bản này' })).toBeEnabled(); });
  });
});
