import { fireEvent, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import type { ShopDto, ShopLogoDto } from '@vsite/api-sdk';
import { renderWithQuery } from '../../test/render-with-query';
import { server } from '../../test/msw-server';
import { ShopLogoSection } from './shop-logo-section';

function makeFile(name: string, sizeBytes: number, type: string): File {
  const file = new File([new Uint8Array(1)], name, { type });
  Object.defineProperty(file, 'size', { value: sizeBytes });
  return file;
}

const baseShop: ShopDto = {
  id: 'shop-1',
  name: 'Spa ABC',
  slug: 'spa-abc',
  kind: 'Hosted',
  externalUrl: null,
  status: 'Active',
  logoId: null,
  logoUrl: null,
};

function useShop(shop: ShopDto) {
  server.use(http.get('*/shops/:shopId', () => HttpResponse.json(shop)));
}

describe('ShopLogoSection (F4/F6)', () => {
  it('loading: hiện "Đang tải logo..." trước khi shop về', async () => {
    useShop(baseShop);
    renderWithQuery(<ShopLogoSection shopId="shop-1" isOwner={false} />);
    expect(screen.getByText('Đang tải logo...')).toBeInTheDocument();
    expect(await screen.findByText('Chưa có logo')).toBeInTheDocument();
  });

  it('logoUrl có giá trị: <img> dùng đúng nguyên trạng, không bọc thêm /media/', async () => {
    useShop({ ...baseShop, logoId: 'd1', logoUrl: '/media/shop-1/logo-320x96.jpg' });
    renderWithQuery(<ShopLogoSection shopId="shop-1" isOwner={false} />);
    const img = await screen.findByAltText('Logo shop');
    expect(img).toHaveAttribute('src', '/media/shop-1/logo-320x96.jpg');
    expect(img.getAttribute('src')).not.toContain('/media//media/');
    expect(screen.queryByText('Chưa có logo')).not.toBeInTheDocument();
  });

  it('logoUrl null: hiện trạng thái rỗng "Chưa có logo", không có ảnh', async () => {
    useShop(baseShop);
    renderWithQuery(<ShopLogoSection shopId="shop-1" isOwner={false} />);
    expect(await screen.findByText('Chưa có logo')).toBeInTheDocument();
    expect(screen.queryByAltText('Logo shop')).not.toBeInTheDocument();
  });

  it('error: shop query lỗi hiện thông báo', async () => {
    server.use(
      http.get('*/shops/:shopId', () =>
        HttpResponse.json({ error_code: 'SHOP_ACCESS_DENIED', title: 'denied' }, { status: 403 }),
      ),
    );
    renderWithQuery(<ShopLogoSection shopId="shop-1" isOwner={false} />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Bạn không có quyền truy cập shop này.');
  });

  it('không hiện nút tải logo khi không phải Owner', async () => {
    useShop(baseShop);
    renderWithQuery(<ShopLogoSection shopId="shop-1" isOwner={false} />);
    await screen.findByText('Chưa có logo');
    expect(screen.queryByText('Tải logo mới')).not.toBeInTheDocument();
  });

  it('hiện nút tải logo khi là Owner', async () => {
    useShop(baseShop);
    renderWithQuery(<ShopLogoSection shopId="shop-1" isOwner />);
    await screen.findByText('Chưa có logo');
    expect(screen.getByText('Tải logo mới')).toBeInTheDocument();
  });

  it('upload thành công: refetch shop và hiện logoUrl mới từ server', async () => {
    let current: ShopDto = baseShop;
    let getCount = 0;
    const uploadResponse: ShopLogoDto = {
      libraryAsset: {
        id: 'lib-1',
        storageKey: 'shop-1/logo-original.jpg',
        mimeType: 'image/jpeg',
        width: 800,
        height: 400,
        sizeBytes: 1024,
        altText: null,
        focalPointX: 0.5,
        focalPointY: 0.5,
        originalFileName: 'logo.jpg',
        folder: null,
        isInLibrary: true,
        preset: null,
        sourceAssetId: null,
        createdAt: new Date().toISOString(),
      },
      derivatives: [],
    };
    server.use(
      http.get('*/shops/:shopId', () => {
        getCount += 1;
        return HttpResponse.json(current);
      }),
      http.put('*/shops/:shopId/logo', () => {
        current = { ...baseShop, logoId: 'd1', logoUrl: '/media/shop-1/logo-new-320x96.jpg' };
        return HttpResponse.json(uploadResponse);
      }),
    );
    renderWithQuery(<ShopLogoSection shopId="shop-1" isOwner />);
    await screen.findByText('Chưa có logo');

    const file = makeFile('logo.jpg', 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Tải logo mới', { selector: 'input' }), { target: { files: [file] } });

    const img = await screen.findByAltText('Logo shop');
    expect(img).toHaveAttribute('src', '/media/shop-1/logo-new-320x96.jpg');
    expect(getCount).toBeGreaterThanOrEqual(2);
  });

  it('upload lỗi HEIC hiện hướng dẫn', async () => {
    useShop(baseShop);
    server.use(
      http.put('*/shops/:shopId/logo', () =>
        HttpResponse.json({ error_code: 'MEDIA_HEIC_UNSUPPORTED', title: 'heic' }, { status: 422 }),
      ),
    );
    renderWithQuery(<ShopLogoSection shopId="shop-1" isOwner />);
    await screen.findByText('Chưa có logo');
    const file = makeFile('logo.jpg', 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Tải logo mới', { selector: 'input' }), { target: { files: [file] } });
    expect(await screen.findByRole('alert')).toHaveTextContent(/Trên iPhone/);
  });

  it('báo lỗi client khi chọn file vượt quá 10MB, không gọi API', async () => {
    useShop(baseShop);
    renderWithQuery(<ShopLogoSection shopId="shop-1" isOwner />);
    await screen.findByText('Chưa có logo');
    const bigFile = makeFile('big.jpg', 11 * 1024 * 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Tải logo mới', { selector: 'input' }), { target: { files: [bigFile] } });
    expect(screen.getByText(/vượt quá 10 MB/)).toBeInTheDocument();
  });
});
