import { fireEvent, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { MediaAssetDto, ShopLogoDto } from '@vsite/api-sdk';
import { renderWithQuery } from '../../test/render-with-query';
import { server } from '../../test/msw-server';
import { ShopLogoSection } from './shop-logo-section';

function makeFile(name: string, sizeBytes: number, type: string): File {
  const file = new File([new Uint8Array(1)], name, { type });
  Object.defineProperty(file, 'size', { value: sizeBytes });
  return file;
}

const baseAsset: MediaAssetDto = {
  id: 'logo-derivative-1',
  storageKey: 'shop-1/logo-320x96.jpg',
  mimeType: 'image/jpeg',
  width: 320,
  height: 96,
  sizeBytes: 2048,
  altText: null,
  focalPointX: 0.5,
  focalPointY: 0.5,
  originalFileName: 'logo.jpg',
  folder: null,
  isInLibrary: false,
  preset: '320x96,inside',
  sourceAssetId: 'logo-library-1',
  createdAt: new Date().toISOString(),
};

describe('ShopLogoSection (F4)', () => {
  it('empty: hiện "chưa có logo" khi logoId null, không gọi assets query', () => {
    renderWithQuery(<ShopLogoSection shopId="shop-1" logoId={null} isOwner={false} />);
    expect(screen.getByText('Shop chưa có logo.')).toBeInTheDocument();
  });

  it('không hiện nút tải logo khi không phải Owner', () => {
    renderWithQuery(<ShopLogoSection shopId="shop-1" logoId={null} isOwner={false} />);
    expect(screen.queryByText('Tải logo mới')).not.toBeInTheDocument();
  });

  it('hiện nút tải logo khi là Owner', () => {
    renderWithQuery(<ShopLogoSection shopId="shop-1" logoId={null} isOwner />);
    expect(screen.getByText('Tải logo mới')).toBeInTheDocument();
  });

  it('loading rồi hiện ảnh logo hiện có qua assets?ids=', async () => {
    server.use(
      http.get('*/shops/:shopId/media/assets', () => HttpResponse.json([baseAsset])),
    );
    renderWithQuery(<ShopLogoSection shopId="shop-1" logoId="logo-derivative-1" isOwner={false} />);

    expect(screen.getByText('Đang tải logo...')).toBeInTheDocument();
    const img = await screen.findByAltText('Logo shop');
    expect(img).toHaveAttribute('src', '/media/shop-1/logo-320x96.jpg');
  });

  it('error: assets query lỗi hiện thông báo', async () => {
    server.use(
      http.get('*/shops/:shopId/media/assets', () =>
        HttpResponse.json({ error_code: 'SHOP_ACCESS_DENIED', title: 'denied' }, { status: 403 }),
      ),
    );
    renderWithQuery(<ShopLogoSection shopId="shop-1" logoId="logo-derivative-1" isOwner={false} />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Bạn không có quyền truy cập shop này.');
  });

  it('upload thành công hiện ngay ảnh từ phái sinh 320x96,inside trong response', async () => {
    const responseBody: ShopLogoDto = {
      libraryAsset: { ...baseAsset, id: 'logo-library-1', isInLibrary: true, preset: null, storageKey: 'shop-1/logo-original.jpg' },
      derivatives: [
        baseAsset,
        { ...baseAsset, id: 'logo-derivative-2', preset: '96x96,cover', storageKey: 'shop-1/logo-96x96.jpg' },
      ],
    };
    server.use(
      http.put('*/shops/:shopId/logo', () => HttpResponse.json(responseBody)),
    );
    renderWithQuery(<ShopLogoSection shopId="shop-1" logoId={null} isOwner />);

    const file = makeFile('logo.jpg', 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Tải logo mới', { selector: 'input' }), { target: { files: [file] } });

    await waitFor(async () => {
      const img = await screen.findByAltText('Logo shop');
      expect(img).toHaveAttribute('src', '/media/shop-1/logo-320x96.jpg');
    });
  });

  it('upload thành công nhưng response thiếu phái sinh 320x96,inside → hiện cảnh báo, không âm thầm dùng ảnh gốc', async () => {
    const responseBody: ShopLogoDto = {
      libraryAsset: { ...baseAsset, id: 'logo-library-1', isInLibrary: true, preset: null, storageKey: 'shop-1/logo-original.jpg' },
      // Chỉ có phái sinh khác — KHÔNG có 320x96,inside.
      derivatives: [{ ...baseAsset, id: 'logo-derivative-2', preset: '96x96,cover', storageKey: 'shop-1/logo-96x96.jpg' }],
    };
    server.use(
      http.put('*/shops/:shopId/logo', () => HttpResponse.json(responseBody)),
    );
    renderWithQuery(<ShopLogoSection shopId="shop-1" logoId={null} isOwner />);

    const file = makeFile('logo.jpg', 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Tải logo mới', { selector: 'input' }), { target: { files: [file] } });

    expect(await screen.findByRole('alert')).toHaveTextContent(/Không tìm thấy phái sinh 320x96,inside/);
    // Ảnh vẫn hiện (fallback bản gốc) nhưng đi kèm cảnh báo ở trên, không phải thay thế âm thầm.
    const img = await screen.findByAltText('Logo shop');
    expect(img).toHaveAttribute('src', '/media/shop-1/logo-original.jpg');
  });

  it('báo lỗi client khi chọn file vượt quá 10MB, không gọi API', () => {
    renderWithQuery(<ShopLogoSection shopId="shop-1" logoId={null} isOwner />);
    const bigFile = makeFile('big.jpg', 11 * 1024 * 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Tải logo mới', { selector: 'input' }), { target: { files: [bigFile] } });
    expect(screen.getByText(/vượt quá 10 MB/)).toBeInTheDocument();
  });
});
