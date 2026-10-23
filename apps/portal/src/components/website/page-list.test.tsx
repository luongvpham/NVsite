import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { getListPagesQueryKey, type PageDto } from '@vsite/api-sdk';
import { server } from '../../test/msw-server';
import { renderWithQuery } from '../../test/render-with-query';
import { PageList } from './page-list';

function page(overrides: Partial<PageDto>): PageDto {
  return {
    id: 'p-x',
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
    updatedAt: '2026-10-23T00:00:00Z',
    ...overrides,
  };
}

const pages: PageDto[] = [
  page({ id: 'p-home', slug: '', title: 'Trang chủ', isHomePage: true, deletable: false, sortOrder: 0 }),
  page({ id: 'p-contact', kind: 'System', systemType: 'Contact', slug: 'lien-he', title: 'Liên hệ', deletable: false, sortOrder: 1 }),
  page({ id: 'p-1', slug: 'dich-vu', title: 'Dịch vụ', sortOrder: 2 }),
];

function renderList() {
  server.use(http.get('*/api/shops/:shopId/website/pages', () => HttpResponse.json(pages)));
  const utils = renderWithQuery(<PageList shopId="shop-1" />);
  const invalidate = vi.spyOn(utils.queryClient, 'invalidateQueries');
  return { ...utils, invalidate };
}

function row(title: string): HTMLElement {
  return screen.getByText(title).closest('li') as HTMLElement;
}

describe('PageList — xoá trang', () => {
  it('chỉ trang deletable có nút Xoá (trang chủ và System thì không)', async () => {
    renderList();
    await screen.findByText('Dịch vụ');

    expect(within(row('Dịch vụ')).getByRole('button', { name: 'Xoá' })).toBeInTheDocument();
    expect(within(row('Trang chủ')).queryByRole('button', { name: 'Xoá' })).not.toBeInTheDocument();
    expect(within(row('Liên hệ')).queryByRole('button', { name: 'Xoá' })).not.toBeInTheDocument();
  });

  it('xác nhận → DELETE đúng trang → làm mới danh sách', async () => {
    let deletedId: string | readonly string[] | undefined;
    server.use(
      http.delete('*/api/shops/:shopId/website/pages/:pageId', ({ params }) => {
        deletedId = params.pageId;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const { invalidate } = renderList();
    await screen.findByText('Dịch vụ');

    fireEvent.click(within(row('Dịch vụ')).getByRole('button', { name: 'Xoá' }));
    const confirm = await screen.findByRole('alertdialog');
    expect(confirm).toHaveTextContent('Dịch vụ');
    fireEvent.click(within(confirm).getByRole('button', { name: 'Xoá trang' }));

    await waitFor(() => {
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    });
    expect(deletedId).toBe('p-1');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: getListPagesQueryKey('shop-1') });
  });

  it('Huỷ → không gọi DELETE', async () => {
    let called = false;
    server.use(
      http.delete('*/api/shops/:shopId/website/pages/:pageId', () => {
        called = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderList();
    await screen.findByText('Dịch vụ');

    fireEvent.click(within(row('Dịch vụ')).getByRole('button', { name: 'Xoá' }));
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Huỷ' }));

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(called).toBe(false);
  });

  it('409 PAGE_NOT_DELETABLE → báo lỗi trong hộp xác nhận, không đóng', async () => {
    server.use(
      http.delete('*/api/shops/:shopId/website/pages/:pageId', () =>
        HttpResponse.json({ status: 409, title: 'x', error_code: 'PAGE_NOT_DELETABLE' }, { status: 409 }),
      ),
    );
    renderList();
    await screen.findByText('Dịch vụ');

    fireEvent.click(within(row('Dịch vụ')).getByRole('button', { name: 'Xoá' }));
    const confirm = await screen.findByRole('alertdialog');
    fireEvent.click(within(confirm).getByRole('button', { name: 'Xoá trang' }));

    expect(await within(confirm).findByRole('alert')).toHaveTextContent('Trang này không xoá được');
  });
});

describe('PageList — mở form', () => {
  it('"Tạo trang" mở dialog tạo trống; "Sửa" mở dialog điền sẵn dữ liệu trang đó', async () => {
    renderList();
    await screen.findByText('Dịch vụ');

    fireEvent.click(screen.getByRole('button', { name: 'Tạo trang' }));
    const createDialog = await screen.findByRole('dialog', { name: 'Tạo trang mới' });
    expect(within(createDialog).getByLabelText('Tên trang')).toHaveValue('');
    fireEvent.click(within(createDialog).getByRole('button', { name: 'Đóng' }));
    expect(screen.queryByRole('dialog', { name: 'Tạo trang mới' })).not.toBeInTheDocument();

    fireEvent.click(within(row('Dịch vụ')).getByRole('button', { name: 'Sửa' }));
    const editDialog = await screen.findByRole('dialog', { name: 'Sửa trang' });
    expect(within(editDialog).getByLabelText('Tên trang')).toHaveValue('Dịch vụ');
    expect(within(editDialog).getByLabelText('Đường dẫn')).toHaveValue('dich-vu');
  });

  it('mọi trang đều sửa được, kể cả trang chủ và System', async () => {
    renderList();
    await screen.findByText('Dịch vụ');
    for (const title of ['Trang chủ', 'Liên hệ', 'Dịch vụ']) {
      expect(within(row(title)).getByRole('button', { name: 'Sửa' })).toBeInTheDocument();
    }
  });
});
