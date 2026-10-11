import { fireEvent, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import type { MediaAssetDto } from '@vsite/api-sdk';
import { HEIC_UNSUPPORTED_GUIDANCE } from '../../lib/error-messages';
import { renderWithQuery } from '../../test/render-with-query';
import { server } from '../../test/msw-server';
import { UploadSlotDialog } from './upload-slot-dialog';

function makeFile(name: string, sizeBytes: number, type: string): File {
  const file = new File([new Uint8Array(1)], name, { type });
  Object.defineProperty(file, 'size', { value: sizeBytes });
  return file;
}

const heroAsset: MediaAssetDto = {
  id: 'asset-1',
  storageKey: 'shop-1/asset-1.jpg',
  mimeType: 'image/jpeg',
  width: 1600,
  height: 900,
  sizeBytes: 12345,
  altText: null,
  focalPointX: 0.5,
  focalPointY: 0.5,
  originalFileName: 'hero.jpg',
  folder: null,
  isInLibrary: false,
  preset: '1600x900,cover',
  sourceAssetId: null,
  createdAt: new Date().toISOString(),
};

describe('UploadSlotDialog (F2)', () => {
  it('không render gì khi open=false', () => {
    renderWithQuery(
      <UploadSlotDialog shopId="shop-1" preset="1600x900,cover" open={false} onClose={vi.fn()} onUploaded={vi.fn()} />,
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('báo lỗi client khi chọn file vượt quá 10MB, không gọi API', async () => {
    renderWithQuery(
      <UploadSlotDialog shopId="shop-1" preset="1600x900,cover" open onClose={vi.fn()} onUploaded={vi.fn()} />,
    );
    const bigFile = makeFile('big.jpg', 11 * 1024 * 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Chọn ảnh'), { target: { files: [bigFile] } });

    expect(await screen.findByText(/vượt quá 10 MB/)).toBeInTheDocument();
  });

  it('ẩn focal point picker khi preset có fit=inside', () => {
    renderWithQuery(
      <UploadSlotDialog shopId="shop-1" preset="1200x1200,inside" open onClose={vi.fn()} onUploaded={vi.fn()} />,
    );
    const file = makeFile('a.jpg', 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Chọn ảnh'), { target: { files: [file] } });
    expect(screen.getByText(/giữ nguyên tỉ lệ ảnh/)).toBeInTheDocument();
  });

  it('hiện focal point picker khi preset có fit=cover', () => {
    renderWithQuery(
      <UploadSlotDialog shopId="shop-1" preset="1600x900,cover" open onClose={vi.fn()} onUploaded={vi.fn()} />,
    );
    const file = makeFile('a.jpg', 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Chọn ảnh'), { target: { files: [file] } });
    expect(screen.getByText(/điểm lấy nét/)).toBeInTheDocument();
  });

  it('upload thành công gọi onUploaded với asset (không phải libraryAsset) và đóng dialog', async () => {
    server.use(
      http.post('*/api/shops/:shopId/media/slot-uploads', () =>
        HttpResponse.json({ asset: heroAsset, libraryAsset: null }),
      ),
    );
    const onUploaded = vi.fn();
    const onClose = vi.fn();
    renderWithQuery(<UploadSlotDialog shopId="shop-1" preset="1600x900,cover" open onClose={onClose} onUploaded={onUploaded} />);

    const file = makeFile('a.jpg', 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Chọn ảnh'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Tải lên' }));

    await waitFor(() => {
      expect(onUploaded).toHaveBeenCalledWith(heroAsset);
    });
    expect(onClose).toHaveBeenCalled();
  });

  it('lỗi MEDIA_HEIC_UNSUPPORTED hiện hướng dẫn chuyển sang JPEG', async () => {
    server.use(
      http.post('*/api/shops/:shopId/media/slot-uploads', () =>
        HttpResponse.json({ error_code: 'MEDIA_HEIC_UNSUPPORTED', title: 'unsupported' }, { status: 422 }),
      ),
    );
    renderWithQuery(<UploadSlotDialog shopId="shop-1" preset="1600x900,cover" open onClose={vi.fn()} onUploaded={vi.fn()} />);

    const file = makeFile('a.jpg', 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Chọn ảnh'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Tải lên' }));

    expect(await screen.findByText(HEIC_UNSUPPORTED_GUIDANCE)).toBeInTheDocument();
  });

  it('chọn file HEIC: client chặn và hiện hướng dẫn HEIC (S4)', () => {
    renderWithQuery(<UploadSlotDialog shopId="shop-1" preset="1600x900,cover" open onClose={vi.fn()} onUploaded={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Chọn ảnh'), { target: { files: [makeFile('a.heic', 1024, 'image/heic')] } });

    expect(screen.getByText(/Định dạng không hỗ trợ/)).toBeInTheDocument();
    expect(screen.getByText(HEIC_UNSUPPORTED_GUIDANCE)).toBeInTheDocument();
  });

  it('file sai định dạng khác (không phải HEIC) không hiện hướng dẫn HEIC', () => {
    renderWithQuery(<UploadSlotDialog shopId="shop-1" preset="1600x900,cover" open onClose={vi.fn()} onUploaded={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Chọn ảnh'), { target: { files: [makeFile('a.gif', 1024, 'image/gif')] } });

    expect(screen.getByText(/Định dạng không hỗ trợ/)).toBeInTheDocument();
    expect(screen.queryByText(HEIC_UNSUPPORTED_GUIDANCE)).not.toBeInTheDocument();
  });

  it('lỗi MEDIA_FILE_TOO_LARGE từ server hiện message riêng', async () => {
    server.use(
      http.post('*/api/shops/:shopId/media/slot-uploads', () =>
        HttpResponse.json({ error_code: 'MEDIA_FILE_TOO_LARGE', title: 'too large' }, { status: 413 }),
      ),
    );
    renderWithQuery(<UploadSlotDialog shopId="shop-1" preset="1600x900,cover" open onClose={vi.fn()} onUploaded={vi.fn()} />);

    const file = makeFile('a.jpg', 1024, 'image/jpeg');
    fireEvent.change(screen.getByLabelText('Chọn ảnh'), { target: { files: [file] } });
    fireEvent.click(screen.getByRole('button', { name: 'Tải lên' }));

    expect(await screen.findByText('Ảnh vượt quá 10 MB, vui lòng chọn ảnh nhỏ hơn.')).toBeInTheDocument();
  });
});
