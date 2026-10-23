import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import type { MediaAssetDto, PagedResultOfMediaAssetDto } from '@vsite/api-sdk';
import { mediaUrl } from '@vsite/builder-components';
import { HEIC_UNSUPPORTED_GUIDANCE } from '../../lib/error-messages';
import { renderWithQuery } from '../../test/render-with-query';
import { server } from '../../test/msw-server';
import { MediaLibraryPicker } from './media-library-picker';

function makeFile(name: string, sizeBytes: number, type: string): File {
  const file = new File([new Uint8Array(1)], name, { type });
  Object.defineProperty(file, 'size', { value: sizeBytes });
  return file;
}

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
      http.get('*/api/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([asset('a1'), asset('a2')]))),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 2048 })),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    expect(screen.getByText('Đang tải thư viện...')).toBeInTheDocument();
    await screen.findByText(/Đã dùng/);
    const thumbnails = screen.getAllByRole('img');
    expect(thumbnails).toHaveLength(2);
    // src phải đi qua mediaUrl() (packages/builder-components) — không tự nối chuỗi /media/ lần hai.
    expect(thumbnails[0]).toHaveAttribute('src', mediaUrl('shop-1/a1.jpg'));
  });

  it('ẩn nút Xoá khi isOwner không được truyền (mặc định false, fail-safe)', async () => {
    server.use(
      http.get('*/api/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([asset('a1')]))),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    await screen.findAllByRole('img');
    expect(screen.queryByRole('button', { name: 'Xoá' })).not.toBeInTheDocument();
  });

  it('ẩn nút Tải ảnh lên thư viện khi không phải Owner (#100 — mọi route media chỉ Owner)', async () => {
    server.use(
      http.get('*/api/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([asset('a1')]))),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    await screen.findAllByRole('img');
    expect(screen.queryByText('Tải ảnh lên thư viện')).not.toBeInTheDocument();
  });

  it('báo lỗi client khi upload thẳng vào thư viện với file vượt quá 10MB, không gọi API', async () => {
    server.use(
      http.get('*/api/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([]))),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" isOwner open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    const bigFile = makeFile('big.jpg', 11 * 1024 * 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Tải ảnh lên thư viện', { selector: 'input' }), {
      target: { files: [bigFile] },
    });

    expect(await screen.findByText(/vượt quá 10 MB/)).toBeInTheDocument();
  });

  it('chọn file HEIC ở upload thư viện: client chặn và hiện hướng dẫn HEIC (S4)', async () => {
    server.use(
      http.get('*/api/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([]))),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" isOwner open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    fireEvent.change(screen.getByLabelText('Tải ảnh lên thư viện', { selector: 'input' }), {
      target: { files: [makeFile('a.heif', 1024, 'image/heif')] },
    });

    expect(await screen.findByText(/Định dạng không hỗ trợ/)).toBeInTheDocument();
    expect(screen.getByText(HEIC_UNSUPPORTED_GUIDANCE)).toBeInTheDocument();
  });

  it('hiện trạng thái rỗng khi thư viện chưa có ảnh', async () => {
    server.use(
      http.get('*/api/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([]))),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    expect(await screen.findByText('Thư viện chưa có ảnh nào. Tải ảnh lên để bắt đầu.')).toBeInTheDocument();
  });

  it('hiện trạng thái lỗi khi tải thư viện thất bại', async () => {
    server.use(
      http.get('*/api/shops/:shopId/media/library', () =>
        HttpResponse.json({ error_code: 'SHOP_ACCESS_DENIED', title: 'denied' }, { status: 403 }),
      ),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
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
      http.get('*/api/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([source]))),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
      http.post('*/api/shops/:shopId/media/library/:assetId/clones', () => HttpResponse.json(cloned)),
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

  it('mode="library": chọn ảnh trả thẳng bản Library, không bước xác nhận, không gọi clone (ogImageId của Page)', async () => {
    const source = asset('a1');
    let cloneCalled = false;
    server.use(
      http.get('*/api/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([source]))),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
      http.post('*/api/shops/:shopId/media/library/:assetId/clones', () => {
        cloneCalled = true;
        return HttpResponse.json(asset('clone-1'));
      }),
    );
    const onSelect = vi.fn();
    renderWithQuery(<MediaLibraryPicker shopId="shop-1" mode="library" open onClose={vi.fn()} onSelect={onSelect} />);

    const thumbnails = await screen.findAllByRole('img');
    fireEvent.click(thumbnails[0]?.closest('button') as HTMLButtonElement);

    expect(onSelect).toHaveBeenCalledWith(source);
    expect(screen.queryByText('Xác nhận chọn ảnh')).not.toBeInTheDocument();
    expect(cloneCalled).toBe(false);
  });

  it('xoá: liệt kê từng nơi đang dùng — Logo shop, Trang {pageTitle} (Flow 7)', async () => {
    server.use(
      http.get('*/api/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([asset('a1')]))),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
      http.get('*/api/shops/:shopId/media/library/:assetId/references', () =>
        HttpResponse.json({
          references: [
            { kind: 'ShopLogo' },
            { kind: 'PageDraft', pageId: 'p-1', pageTitle: 'Dịch vụ' },
            { kind: 'PageDraft', pageId: 'p-2', pageTitle: 'Giới thiệu' },
          ],
        }),
      ),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" isOwner open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    fireEvent.click(await screen.findByRole('button', { name: 'Xoá' }));

    const confirm = await screen.findByRole('alertdialog');
    expect(within(confirm).getByText(/đang được dùng ở 3 nơi/)).toBeInTheDocument();
    const items = within(within(confirm).getByRole('list')).getAllByRole('listitem');
    expect(items.map((li) => li.textContent)).toEqual(['Logo shop', 'Trang Dịch vụ', 'Trang Giới thiệu']);
  });

  it('xoá: reference PageDraft thiếu pageTitle → vẫn hiện "Một trang", không in "null"', async () => {
    server.use(
      http.get('*/api/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([asset('a1')]))),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
      http.get('*/api/shops/:shopId/media/library/:assetId/references', () =>
        HttpResponse.json({ references: [{ kind: 'PageDraft', pageId: 'p-1', pageTitle: null }] }),
      ),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" isOwner open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    fireEvent.click(await screen.findByRole('button', { name: 'Xoá' }));

    const confirm = await screen.findByRole('alertdialog');
    expect(within(confirm).getByRole('listitem')).toHaveTextContent(/^Một trang$/);
  });

  it('xoá: gọi references trước, có tham chiếu thì cảnh báo nhưng không chặn xoá', async () => {
    const source = asset('a1');
    server.use(
      http.get('*/api/shops/:shopId/media/library', () => HttpResponse.json(libraryPage([source]))),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
      http.get('*/api/shops/:shopId/media/library/:assetId/references', () =>
        HttpResponse.json({ references: [{ kind: 'ShopLogo' }] }),
      ),
      http.delete('*/api/shops/:shopId/media/library/:assetId', () => new HttpResponse(null, { status: 204 })),
    );
    renderWithQuery(
      <MediaLibraryPicker shopId="shop-1" preset="800x800,cover" isOwner open onClose={vi.fn()} onSelect={vi.fn()} />,
    );

    fireEvent.click(await screen.findByRole('button', { name: 'Xoá' }));

    expect(await screen.findByText(/đang được dùng ở 1 nơi/)).toBeInTheDocument();
    expect(
      screen.getByText(/Ảnh sẽ ẩn khỏi thư viện; các nơi đang dùng vẫn hiển thị bình thường\./),
    ).toBeInTheDocument();
    expect(screen.queryByText(/có thể hiển thị lỗi/)).not.toBeInTheDocument();
    const confirmDialog = screen.getByRole('alertdialog');
    fireEvent.click(within(confirmDialog).getByRole('button', { name: 'Xoá' }));

    await waitFor(() => {
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    });
  });
});
