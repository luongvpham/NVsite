import { fireEvent, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import type { MediaAssetDto, ShopSummaryDto } from '@vsite/api-sdk';
import { renderWithQuery } from '../test/render-with-query';
import { server } from '../test/msw-server';
import { ImagePickerControl } from './dev-registry';

const asset: MediaAssetDto = {
  id: 'a1',
  storageKey: 'shops/shop-1/a1.webp',
  mimeType: 'image/webp',
  width: 800,
  height: 800,
  sizeBytes: 5000,
  altText: null,
  focalPointX: 0.5,
  focalPointY: 0.5,
  originalFileName: 'a1.jpg',
  folder: null,
  isInLibrary: true,
  preset: null,
  sourceAssetId: null,
  createdAt: new Date().toISOString(),
};

function shop(roleCode: string): ShopSummaryDto {
  return { id: 'shop-1', name: 'Shop', slug: 'shop', kind: 'Hosted', status: 'Active', roleCode, logoUrl: null } as ShopSummaryDto;
}

function renderControl(roleCode: string) {
  server.use(
    http.get('*/api/shops', () => HttpResponse.json([shop(roleCode)])),
    http.get('*/api/shops/:shopId/media/library', () =>
      HttpResponse.json({ items: [asset], total: 1, page: 1, pageSize: 24 }),
    ),
    http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
  );
  renderWithQuery(
    <ImagePickerControl
      shopId="shop-1"
      preset="800x800,cover"
      value={{}}
      onChange={vi.fn()}
      mediaMap={{}}
      onAssetResolved={vi.fn()}
    />,
  );
  fireEvent.click(screen.getByRole('button', { name: 'Chọn từ thư viện' }));
}

describe('dev-registry ImagePickerControl — isOwner wiring (W3)', () => {
  it('Owner (roleCode từ GET /shops) thấy nút Xoá trong picker', async () => {
    renderControl('Owner');

    expect(await screen.findByRole('button', { name: 'Xoá' }, { timeout: 5000 })).toBeInTheDocument();
  }, 15_000);

  it('không phải Owner thì không có nút Xoá', async () => {
    renderControl('Staff');

    await screen.findByText(/Đã dùng/, undefined, { timeout: 5000 });
    expect(screen.queryByRole('button', { name: 'Xoá' })).not.toBeInTheDocument();
  }, 15_000);
});
