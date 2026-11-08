import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';
import { useSessionStore } from '../stores/session-store';
import {
  draftBody,
  nodeEl,
  openEditor,
  openFixture,
  validEmptyHome,
  validHome,
  validSystemContact,
} from '../test/editor-helpers';

afterEach(() => {
  useSessionStore.getState().clearSession();
});

describe('Editor — nạp draft (Flow 4)', () => {
  it('đang tải → hiện "Đang tải trang", rồi canvas với nội dung của cây', async () => {
    openEditor(async () => {
      await delay(150);
      return HttpResponse.json(draftBody(validHome));
    });
    expect(await screen.findByText('Đang tải trang...')).toBeInTheDocument();
    expect(await screen.findByText('Chào mừng')).toBeInTheDocument();
    expect(screen.getByTestId('editor-canvas')).toBeInTheDocument();
    expect(screen.queryByText('Đang tải trang...')).not.toBeInTheDocument();
  });

  it('404 PAGE_NOT_FOUND → báo lỗi, không có canvas', async () => {
    openEditor(() =>
      HttpResponse.json({ status: 404, title: 'x', error_code: 'PAGE_NOT_FOUND' }, { status: 404 }),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent('Không tìm thấy trang');
    expect(screen.queryByTestId('editor-canvas')).not.toBeInTheDocument();
  });

  it('cây hỏng theo Zod → "không đọc được", không cho sửa', async () => {
    openFixture({ pageKind: 'Composable', tree: { schemaVersion: 1, id: 'root', type: 'Page', children: 'sai' } });
    expect(await screen.findByRole('alert')).toHaveTextContent('Dữ liệu trang không đọc được');
    expect(screen.queryByTestId('editor-canvas')).not.toBeInTheDocument();
  });

  it('cây rỗng → canvas hiện gợi ý thêm khối', async () => {
    openFixture(validEmptyHome);
    expect(await screen.findByText(/Trang trống — thêm khối từ bảng bên trái/)).toBeInTheDocument();
  });

  it('non-Owner mở thẳng URL → không có request draft', async () => {
    let draftCalls = 0;
    openEditor(() => {
      draftCalls += 1;
      return HttpResponse.json(draftBody(validHome));
    }, 'Manager');
    expect(await screen.findByText(/Chỉ chủ shop/)).toBeInTheDocument();
    expect(draftCalls).toBe(0);
  });
});

describe('Editor — chọn node + Inspector', () => {
  it('chưa chọn → gợi ý; bấm node → Inspector đúng field, canvas có viền chọn', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    expect(screen.getByText(/Chọn một khối trên trang/)).toBeInTheDocument();

    fireEvent.click(nodeEl('c_hero1'));

    const panel = screen.getByRole('complementary', { name: 'Thuộc tính khối' });
    expect(within(panel).getByRole('heading', { name: /Hero/ })).toBeInTheDocument();
    expect(within(panel).getByDisplayValue('Chào mừng')).toBeInTheDocument();
    expect(nodeEl('c_hero1')).toHaveAttribute('data-selected', 'true');
  });

  it('bấm node con chỉ chọn con (không nổi bọt lên Section cha)', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');

    fireEvent.click(nodeEl('c_rich1'));

    expect(nodeEl('c_rich1')).toHaveAttribute('data-selected', 'true');
    expect(nodeEl('c_sect1')).not.toHaveAttribute('data-selected');
  });

  it('bấm vùng trống của canvas → bỏ chọn', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_hero1'));

    fireEvent.click(screen.getByTestId('editor-canvas'));

    expect(screen.getByText(/Chọn một khối trên trang/)).toBeInTheDocument();
  });

  it('sửa title ở Inspector → canvas đổi ngay', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_hero1'));

    fireEvent.change(screen.getByDisplayValue('Chào mừng'), { target: { value: 'Tiêu đề mới' } });

    expect(within(nodeEl('c_hero1')).getByText('Tiêu đề mới')).toBeInTheDocument();
    expect(screen.queryByText('Chào mừng')).not.toBeInTheDocument();
  });

  it('sửa field lồng trong group (cta.label) đi qua op update, không có alert', async () => {
    // Đường op bị Engine từ chối (link javascript:, binding Review…) đã phủ ở builder-core; Inspector hiện chưa có ô nhập URL.
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_hero1'));

    fireEvent.change(screen.getByDisplayValue('Đặt lịch'), { target: { value: 'Mới' } });

    expect(within(nodeEl('c_hero1')).getByText('Mới')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});

describe('Editor — undo/redo (#42)', () => {
  async function editTitle() {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_hero1'));
    fireEvent.change(screen.getByDisplayValue('Chào mừng'), { target: { value: 'Bản sửa' } });
    expect(screen.getByText('Bản sửa')).toBeInTheDocument();
  }

  it('nút Hoàn tác/Làm lại: tắt khi chưa có lịch sử, chạy đúng sau khi sửa', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    expect(screen.getByRole('button', { name: 'Hoàn tác' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Làm lại' })).toBeDisabled();

    fireEvent.click(nodeEl('c_hero1'));
    fireEvent.change(screen.getByDisplayValue('Chào mừng'), { target: { value: 'Bản sửa' } });

    fireEvent.click(screen.getByRole('button', { name: 'Hoàn tác' }));
    expect(screen.getByText('Chào mừng')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Làm lại' })).toBeEnabled();

    fireEvent.click(screen.getByRole('button', { name: 'Làm lại' }));
    expect(screen.getByText('Bản sửa')).toBeInTheDocument();
  });

  it('Ctrl+Z hoàn tác, Ctrl+Shift+Z và Ctrl+Y làm lại', async () => {
    await editTitle();

    fireEvent.keyDown(document.body, { key: 'z', ctrlKey: true });
    expect(screen.getByText('Chào mừng')).toBeInTheDocument();

    fireEvent.keyDown(document.body, { key: 'z', ctrlKey: true, shiftKey: true });
    expect(screen.getByText('Bản sửa')).toBeInTheDocument();

    fireEvent.keyDown(document.body, { key: 'z', ctrlKey: true });
    fireEvent.keyDown(document.body, { key: 'y', ctrlKey: true });
    expect(screen.getByText('Bản sửa')).toBeInTheDocument();
  });

  it('phím tắt bị bỏ qua khi đang focus trong ô nhập', async () => {
    await editTitle();
    const input = screen.getByDisplayValue('Bản sửa');
    input.focus();

    fireEvent.keyDown(input, { key: 'z', ctrlKey: true });

    expect(screen.getByText('Bản sửa')).toBeInTheDocument();
  });

  it('undo giữ nguyên node đang chọn khi node còn trong cây', async () => {
    // Trường hợp node biến mất sau undo (add/remove) thuộc F7 và đã phủ ở store của builder-core.
    await editTitle();
    fireEvent.click(screen.getByRole('button', { name: 'Hoàn tác' }));
    await waitFor(() => {
      expect(nodeEl('c_hero1')).toHaveAttribute('data-selected', 'true');
    });
  });
});

describe('Editor — trang System (R3)', () => {
  const contact = validSystemContact;

  it('Inspector chỉ bật `content`, prop khác bị khoá', async () => {
    openFixture(contact);
    await screen.findByText('Liên hệ với chúng tôi');
    fireEvent.click(nodeEl('c_cont1'));

    const panel = screen.getByRole('complementary', { name: 'Thuộc tính khối' });
    expect(within(panel).getByText(/chỉ sửa được phần nội dung/)).toBeInTheDocument();
    expect(within(panel).getByDisplayValue('<p>Liên hệ với chúng tôi</p>')).toBeEnabled();
    // `align` (Căn lề) không thuộc editableInSystemPageProps → khoá.
    const alignField = within(panel).getByText('Căn lề').closest('fieldset');
    expect(alignField).toBeDisabled();
  });

  it('sửa content trên System page được', async () => {
    openFixture(contact);
    await screen.findByText('Liên hệ với chúng tôi');
    fireEvent.click(nodeEl('c_cont1'));

    fireEvent.change(screen.getByDisplayValue('<p>Liên hệ với chúng tôi</p>'), {
      target: { value: '<p>Địa chỉ mới</p>' },
    });

    expect(await screen.findByText('Địa chỉ mới')).toBeInTheDocument();
  });
});
