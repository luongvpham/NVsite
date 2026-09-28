import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import type { MediaAssetDto, PagedResultOfMediaAssetDto } from '@vsite/api-sdk';
import { renderWithQuery } from '../../test/render-with-query';
import { server } from '../../test/msw-server';
import { MediaLibraryPicker } from './media-library-picker';

function asset(id: string): MediaAssetDto {
  return {
    id,
    storageKey: `shop-1/${id}.jpg`,
    mimeType: 'image/jpeg',
    width: 800,
    height: 800,
    sizeBytes: 5000,
    altText: null,
    focalPointX: 0.5,
    focalPointY: 0.5,
    originalFileName: `${id}.jpg`,
    folder: null,
    isInLibrary: true,
    preset: null,
    sourceAssetId: null,
    createdAt: new Date().toISOString(),
  };
}

function libraryPage(items: MediaAssetDto[], total = items.length): PagedResultOfMediaAssetDto {
  return { items, total, page: 1, pageSize: 24 };
}

describe('MediaLibraryPicker (F3)', () => {
  it('hiện trạng thái loading rồi render grid ảnh', async () => {
    server.use(
      http.get('*/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([asset('a1'), asset('a2')]))),
      http.get('*/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 2048 })),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    expect(screen.getByText('Đang tải thư viện...')).toBeInTheDocument();
    await screen.findByText(/Đã dùng/);
    expect(screen.getAllByRole('img')).toHaveLength(2);
  });

  it('hiện trạng thái rỗng khi thư viện chưa có ảnh', async () => {
    server.use(
      http.get('*/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([]))),
      http.get('*/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    expect(await screen.findByText('Thư viện chưa có ảnh nào. Tải ảnh lên để bắt đầu.')).toBeInTheDocument();
  });

  it('hiện trạng thái lỗi khi tải thư viện thất bại', async () => {
    server.use(
      http.get('*/shops/:shopId/media/library', () =>
        HttpResponse.json({ error_code: 'SHOP_ACCESS_DENIED', title: 'denied' }, { status: 403 }),
      ),
      http.get('*/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    expect(await screen.findByText('Bạn không có quyền truy cập shop này.')).toBeInTheDocument();
  });

  it('chọn ảnh → xác nhận focal → gọi clone → onSelect nhận MediaAssetDto', async () => {
    const source = asset('a1');
    const cloned = { ...asset('clone-1') };
    server.use(
      http.get('*/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([source]))),
      http.get('*/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
      http.post('*/shops/:shopId/media/library/:assetId/clones', () => HttpResponse.json(cloned)),
    );
    const onSelect = vi.fn();
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" open onClose={vi.fn()} onSelect={onSelect} />,
    );

    const thumbnails = await screen.findAllByRole('img');
    const firstThumbnailButton = thumbnails[0]?.closest('button');
    expect(firstThumbnailButton).toBeInstanceOf(HTMLButtonElement);
    fireEvent.click(firstThumbnailButton as HTMLButtonElement);

    expect(await screen.findByText('Xác nhận chọn ảnh')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Dùng ảnh này' }));

    await waitFor(() => {
      expect(onSelect).toHaveBeenCalledWith(cloned);
    });
  });

  it('xoá: gọi references trước, có tham chiếu thì cảnh báo nhưng không chặn xoá', async () => {
    const source = asset('a1');
    server.use(
      http.get('*/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([source]))),
      http.get('*/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
      http.get('*/shops/:shopId/media/library/:assetId/references', () =>
        HttpResponse.json({ references: [{ kind: 'ShopLogo' }] }),
      ),
      http.delete('*/shops/:shopId/media/library/:assetId', () => new HttpResponse(null, { status: 204 })),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    fireEvent.click(await screen.findByRole('button', { name: 'Xoá' }));

    expect(await screen.findByText(/đang được dùng ở 1 nơi/)).toBeInTheDocument();
    const confirmDialog = screen.getByRole('alertdialog');
    fireEvent.click(within(confirmDialog).getByRole('button', { name: 'Xoá' }));

    await waitFor(() => {
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    });
  });
});
