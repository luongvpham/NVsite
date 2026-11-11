import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { getListPagesQueryKey, type MediaAssetDto, type PageDto } from '@vsite/api-sdk';
import { mediaUrl } from '@vsite/builder-components';
import { server } from '../../test/msw-server';
import { renderWithQuery } from '../../test/render-with-query';
import { PageFormDialog } from './page-form-dialog';

const OG_ID = '22222222-2222-4222-8222-222222222222';

function page(overrides: Partial<PageDto> = {}): PageDto {
  return {
    id: 'p-1',
    kind: 'Composable',
    systemType: null,
    slug: 'dich-vu',
    title: 'Dịch vụ',
    seoTitle: null,
    seoDescription: null,
    ogImageId: null,
    noIndex: false,
    isEnabled: true,
    sortOrder: 3,
    isHomePage: false,
    isDirty: false,
    deletable: true,
    updatedAt: '2026-10-23T00:00:00Z',
    ...overrides,
  };
}

function libraryAsset(id: string): MediaAssetDto {
  return {
    id,
    storageKey: `shop-1/${id}.jpg`,
    mimeType: 'image/jpeg',
    width: 1200,
    height: 630,
    sizeBytes: 5000,
    altText: null,
    focalPointX: 0.5,
    focalPointY: 0.5,
    originalFileName: `${id}.jpg`,
    folder: null,
    isInLibrary: true,
    preset: null,
    sourceAssetId: null,
    createdAt: '2026-10-23T00:00:00Z',
  };
}

function problem(status: number, errorCode: string) {
  return HttpResponse.json({ status, title: 'x', error_code: errorCode }, { status });
}

function renderDialog(p?: PageDto, onClose = vi.fn()) {
  const utils = renderWithQuery(<PageFormDialog shopId="shop-1" page={p} open onClose={onClose} />);
  const invalidate = vi.spyOn(utils.queryClient, 'invalidateQueries');
  return { ...utils, onClose, invalidate };
}

function fill(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

describe('PageFormDialog — tạo trang', () => {
  it('gửi đúng body (SEO rỗng → null), đóng dialog và làm mới danh sách', async () => {
    let body: unknown;
    server.use(
      http.post('*/api/shops/:shopId/website/pages', async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(page({ id: 'p-new' }), { status: 201 });
      }),
    );
    const { onClose, invalidate } = renderDialog();

    fill('Tên trang', '  Giới thiệu ');
    fill('Đường dẫn', 'gioi-thieu');
    fireEvent.click(screen.getByRole('button', { name: 'Tạo trang' }));

    await waitFor(() => {
      expect(onClose).toHaveBeenCalled();
    });
    expect(body).toEqual({
      slug: 'gioi-thieu',
      title: 'Giới thiệu',
      seoTitle: null,
      seoDescription: null,
      ogImageId: null,
      noIndex: false,
      isEnabled: true,
    });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: getListPagesQueryKey('shop-1') });
  });

  it('slug reserved bị chặn ở client (không gọi API) và hiện lỗi ở ô slug', async () => {
    let posted = false;
    server.use(
      http.post('*/api/shops/:shopId/website/pages', () => {
        posted = true;
        return HttpResponse.json(page(), { status: 201 });
      }),
    );
    renderDialog();

    fill('Tên trang', 'Liên hệ 2');
    fill('Đường dẫn', 'lien-he');
    fireEvent.click(screen.getByRole('button', { name: 'Tạo trang' }));

    const slugField = screen.getByLabelText('Đường dẫn');
    await waitFor(() => {
      expect(slugField).toHaveAttribute('aria-invalid', 'true');
    });
    expect(screen.getByText(/hệ thống dành riêng/)).toBeInTheDocument();
    expect(posted).toBe(false);
  });

  it.each([
    [409, 'PAGE_SLUG_TAKEN', /đã có trang khác dùng/],
    [422, 'PAGE_SLUG_RESERVED', /dành riêng/],
    [422, 'PAGE_SLUG_INVALID', /không hợp lệ/],
  ])('%i %s từ BE → lỗi hiện ở ô slug, dialog vẫn mở', async (status, code, message) => {
    server.use(http.post('*/api/shops/:shopId/website/pages', () => problem(status, code)));
    const { onClose } = renderDialog();

    fill('Tên trang', 'Dịch vụ');
    fill('Đường dẫn', 'dich-vu');
    fireEvent.click(screen.getByRole('button', { name: 'Tạo trang' }));

    await waitFor(() => {
      expect(screen.getByLabelText('Đường dẫn')).toHaveAttribute('aria-invalid', 'true');
    });
    expect(screen.getByText(message)).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('422 MEDIA_INVALID_IMAGE_REFERENCE → báo ở mục ảnh chia sẻ', async () => {
    server.use(http.post('*/api/shops/:shopId/website/pages', () => problem(422, 'MEDIA_INVALID_IMAGE_REFERENCE')));
    renderDialog();

    fill('Tên trang', 'Dịch vụ');
    fill('Đường dẫn', 'dich-vu');
    fireEvent.click(screen.getByRole('button', { name: 'Tạo trang' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/chọn lại ảnh/);
  });

  it('lỗi không nhận diện → alert chung, dialog vẫn mở', async () => {
    server.use(http.post('*/api/shops/:shopId/website/pages', () => problem(500, 'SOMETHING_ELSE')));
    const { onClose } = renderDialog();

    fill('Tên trang', 'Dịch vụ');
    fill('Đường dẫn', 'dich-vu');
    fireEvent.click(screen.getByRole('button', { name: 'Tạo trang' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Có lỗi xảy ra');
    expect(onClose).not.toHaveBeenCalled();
  });
});

describe('PageFormDialog — ảnh chia sẻ (OG)', () => {
  it('chọn ảnh từ thư viện (bản Library, KHÔNG clone) → hiện ảnh và gửi ogImageId', async () => {
    const asset = libraryAsset(OG_ID);
    let cloneCalled = false;
    let body: { ogImageId?: string | null } = {};
    server.use(
      http.get('*/api/shops/:shopId/media/library', () =>
        HttpResponse.json({ items: [asset], total: 1, page: 1, pageSize: 24 }),
      ),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
      http.get('*/api/shops/:shopId/media/assets', () => HttpResponse.json([asset])),
      http.post('*/api/shops/:shopId/media/library/:assetId/clones', () => {
        cloneCalled = true;
        return HttpResponse.json(asset);
      }),
      http.post('*/api/shops/:shopId/website/pages', async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json(page(), { status: 201 });
      }),
    );
    const { onClose } = renderDialog();

    fill('Tên trang', 'Dịch vụ');
    fill('Đường dẫn', 'dich-vu');
    fireEvent.click(screen.getByRole('button', { name: 'Chọn ảnh chia sẻ' }));

    const picker = await screen.findByRole('dialog', { name: 'Thư viện ảnh' });
    const thumb = await within(picker).findByRole('img');
    fireEvent.click(thumb.closest('button') as HTMLButtonElement);

    // Ảnh đã chọn hiện ở form, picker đóng.
    await waitFor(() => {
      expect(screen.queryByRole('dialog', { name: 'Thư viện ảnh' })).not.toBeInTheDocument();
    });
    expect(await screen.findByAltText('Ảnh chia sẻ')).toHaveAttribute('src', mediaUrl(asset.storageKey));

    fireEvent.click(screen.getByRole('button', { name: 'Tạo trang' }));
    await waitFor(() => {
      expect(onClose).toHaveBeenCalled();
    });
    expect(body.ogImageId).toBe(OG_ID);
    expect(cloneCalled).toBe(false);
  });

  it('có ảnh sẵn → nút "Bỏ ảnh" gửi ogImageId = null', async () => {
    const asset = libraryAsset(OG_ID);
    let body: { ogImageId?: string | null } = { ogImageId: 'chưa gửi' };
    server.use(
      http.get('*/api/shops/:shopId/media/assets', () => HttpResponse.json([asset])),
      http.put('*/api/shops/:shopId/website/pages/:pageId', async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json(page());
      }),
    );
    const { onClose } = renderDialog(page({ ogImageId: OG_ID }));

    expect(await screen.findByAltText('Ảnh chia sẻ')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Bỏ ảnh' }));
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() => {
      expect(onClose).toHaveBeenCalled();
    });
    expect(body.ogImageId).toBeNull();
  });

  it('Escape khi picker đang mở chỉ đóng picker, không đóng form', async () => {
    server.use(
      http.get('*/api/shops/:shopId/media/library', () =>
        HttpResponse.json({ items: [], total: 0, page: 1, pageSize: 24 }),
      ),
      http.get('*/api/shops/:shopId/media/usage', () => HttpResponse.json({ usedBytes: 0 })),
    );
    const { onClose } = renderDialog();

    fireEvent.click(screen.getByRole('button', { name: 'Chọn ảnh chia sẻ' }));
    await screen.findByRole('dialog', { name: 'Thư viện ảnh' });
    fireEvent.keyDown(document, { key: 'Escape' });

    await waitFor(() => {
      expect(screen.queryByRole('dialog', { name: 'Thư viện ảnh' })).not.toBeInTheDocument();
    });
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByRole('dialog', { name: 'Tạo trang mới' })).toBeInTheDocument();
  });
});

describe('PageFormDialog — sửa trang', () => {
  it('điền sẵn dữ liệu; trang thường mở khoá slug và gửi slug', async () => {
    let body: { slug?: string | null } = {};
    server.use(
      http.put('*/api/shops/:shopId/website/pages/:pageId', async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json(page());
      }),
    );
    const { onClose } = renderDialog(page({ seoTitle: 'SEO', seoDescription: 'Mô tả' }));

    expect(screen.getByLabelText('Tên trang')).toHaveValue('Dịch vụ');
    expect(screen.getByLabelText('Đường dẫn')).toHaveValue('dich-vu');
    expect(screen.getByLabelText('Đường dẫn')).toBeEnabled();
    expect(screen.getByLabelText('Tiêu đề SEO')).toHaveValue('SEO');

    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
    await waitFor(() => {
      expect(onClose).toHaveBeenCalled();
    });
    expect(body.slug).toBe('dich-vu');
  });

  it('trang chủ: slug khoá và gửi slug = null (không đổi)', async () => {
    let body: { slug?: string | null } = { slug: 'chưa gửi' };
    server.use(
      http.put('*/api/shops/:shopId/website/pages/:pageId', async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json(page({ isHomePage: true, slug: '' }));
      }),
    );
    const { onClose } = renderDialog(page({ isHomePage: true, slug: '', title: 'Trang chủ', deletable: false }));

    expect(screen.getByLabelText('Đường dẫn')).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
    await waitFor(() => {
      expect(onClose).toHaveBeenCalled();
    });
    expect(body.slug).toBeNull();
  });

  it('trang System (Contact): slug khoá, vẫn sửa được tiêu đề/SEO/bật tắt', () => {
    renderDialog(page({ kind: 'System', systemType: 'Contact', slug: 'lien-he', title: 'Liên hệ', deletable: false }));

    expect(screen.getByLabelText('Đường dẫn')).toBeDisabled();
    expect(screen.getByLabelText('Tên trang')).toBeEnabled();
    expect(screen.getByLabelText('Tiêu đề SEO')).toBeEnabled();
    expect(screen.getByLabelText('Hiển thị trang')).toBeEnabled();
  });

  it('NotFound: không có slug, noIndex bật cứng và khoá', async () => {
    let body: { slug?: string | null; noIndex?: boolean } = {};
    server.use(
      http.put('*/api/shops/:shopId/website/pages/:pageId', async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json(page());
      }),
    );
    const { onClose } = renderDialog(
      page({ kind: 'System', systemType: 'NotFound', slug: null, title: 'Không tìm thấy', noIndex: false, deletable: false }),
    );

    expect(screen.getByLabelText('Đường dẫn')).toBeDisabled();
    const noIndex = screen.getByLabelText('Ẩn khỏi công cụ tìm kiếm');
    expect(noIndex).toBeChecked();
    expect(noIndex).toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
    await waitFor(() => {
      expect(onClose).toHaveBeenCalled();
    });
    expect(body.slug).toBeNull();
    expect(body.noIndex).toBe(true);
  });

  it('422 PAGE_NOINDEX_REQUIRED hiện ở ô noindex', async () => {
    server.use(http.put('*/api/shops/:shopId/website/pages/:pageId', () => problem(422, 'PAGE_NOINDEX_REQUIRED')));
    renderDialog(page());

    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Không tìm thấy');
  });

  it('409 CONCURRENCY_CONFLICT → báo + "Tải lại" làm mới danh sách và đóng dialog', async () => {
    server.use(http.put('*/api/shops/:shopId/website/pages/:pageId', () => problem(409, 'CONCURRENCY_CONFLICT')));
    const { onClose, invalidate } = renderDialog(page());

    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('vừa được thay đổi');
    expect(onClose).not.toHaveBeenCalled();
    expect(invalidate).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Tải lại' }));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: getListPagesQueryKey('shop-1') });
    expect(onClose).toHaveBeenCalled();
  });

  it('422 PAGE_HOME_SLUG_FIXED / PAGE_SYSTEM_SLUG_FIXED → alert chung (không có ô slug để gắn)', async () => {
    server.use(http.put('*/api/shops/:shopId/website/pages/:pageId', () => problem(422, 'PAGE_SYSTEM_SLUG_FIXED')));
    renderDialog(page());

    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Không đổi được đường dẫn của trang hệ thống');
  });
});
