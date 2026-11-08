import { fireEvent, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { useSessionStore } from '../stores/session-store';
import {
  canvasNodeIds,
  nodeEl,
  openFixture,
  topLevelIds,
  validEmptyHome,
  validHome,
  validSystemContact,
} from '../test/editor-helpers';

afterEach(() => {
  useSessionStore.getState().clearSession();
});

const palette = () => screen.getByRole('region', { name: 'Thêm khối' });
const addButton = (name: string) => within(palette()).getByRole('button', { name: `Thêm ${name}` });

/** Id node xuất hiện trên canvas sau `act` mà trước đó chưa có. */
function newNodeIds(before: string[]): string[] {
  return canvasNodeIds().filter((id) => !before.includes(id));
}

describe('Palette + thêm khối (F7)', () => {
  it('liệt kê khối theo nhãn manifest, theo từng kiểu hiển thị', async () => {
    openFixture(validEmptyHome);
    await screen.findByText(/Trang trống/);

    expect(within(palette()).getByText('Banner đầu trang')).toBeInTheDocument();
    expect(addButton('Banner đầu trang — Ảnh nền toàn khối')).toBeEnabled();
    expect(addButton('Banner đầu trang — Chia đôi: chữ trái, ảnh phải')).toBeEnabled();
    expect(addButton('Đoạn văn bản — Một cột')).toBeInTheDocument();
  });

  it('trang rỗng: thêm Hero → xuất hiện trên canvas, được chọn, Inspector mở', async () => {
    openFixture(validEmptyHome);
    await screen.findByText(/Trang trống/);

    fireEvent.click(addButton('Banner đầu trang — Ảnh nền toàn khối'));

    const [id] = topLevelIds();
    expect(id).toMatch(/^c_[A-Za-z0-9_-]{8}$/);
    expect(nodeEl(id as string)).toHaveAttribute('data-selected', 'true');
    expect(screen.queryByText(/Trang trống/)).not.toBeInTheDocument();
    expect(
      within(screen.getByRole('complementary', { name: 'Thuộc tính khối' })).getByRole('heading', { name: /Hero/ }),
    ).toBeInTheDocument();
  });

  it('chưa chọn gì → thêm vào cuối trang', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    const before = canvasNodeIds();
    const topBefore = topLevelIds();

    fireEvent.click(addButton('Đoạn văn bản — Một cột'));

    const [added] = newNodeIds(before);
    expect(topLevelIds()).toEqual([...topBefore, added]);
  });

  it('chọn Section → thêm vào cuối Section', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_sect1'));
    const before = canvasNodeIds();

    fireEvent.click(addButton('Đoạn văn bản — Một cột'));

    const [added] = newNodeIds(before);
    expect(nodeEl('c_sect1').contains(nodeEl(added as string))).toBe(true);
  });

  it('chọn khối không chứa con (Hero) → thêm ngay sau nó', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_hero1'));
    const before = canvasNodeIds();
    const topBefore = topLevelIds();

    fireEvent.click(addButton('Lưới dịch vụ — Thẻ có ảnh'));

    const [added] = newNodeIds(before);
    const heroAt = topBefore.indexOf('c_hero1');
    expect(topLevelIds()[heroAt + 1]).toBe(added);
  });

  it('Hero thứ hai bị chặn, kèm lý do (maxPerPage = 1)', async () => {
    openFixture(validHome); // đã có Hero
    await screen.findByText('Chào mừng');

    expect(addButton('Banner đầu trang — Ảnh nền toàn khối')).toBeDisabled();
    expect(addButton('Banner đầu trang — Chia đôi: chữ trái, ảnh phải')).toBeDisabled();
    expect(within(palette()).getAllByText(/tối đa một lần/).length).toBeGreaterThan(0);
    expect(addButton('Đoạn văn bản — Một cột')).toBeEnabled();
  });

  it('thêm khối rồi Hoàn tác → khối biến mất, bỏ chọn', async () => {
    openFixture(validEmptyHome);
    await screen.findByText(/Trang trống/);
    fireEvent.click(addButton('Đoạn văn bản — Một cột'));
    expect(topLevelIds()).toHaveLength(1);

    fireEvent.click(screen.getByRole('button', { name: 'Hoàn tác' }));

    expect(topLevelIds()).toHaveLength(0);
    expect(screen.getByText(/Trang trống/)).toBeInTheDocument();
    expect(screen.getByText(/Chọn một khối trên trang/)).toBeInTheDocument();
  });

  it('trang System: không có palette', async () => {
    openFixture(validSystemContact);
    await screen.findByText('Liên hệ với chúng tôi');

    expect(screen.queryByRole('region', { name: 'Thêm khối' })).not.toBeInTheDocument();
  });
});

const actions = () => screen.getByRole('group', { name: 'Thao tác với khối' });
const action = (name: string) => within(actions()).getByRole('button', { name });

describe('Di chuyển bằng nút Lên/Xuống (F7, a11y — cùng op `move` với kéo-thả)', () => {
  it('Xuống đổi chỗ với khối kế tiếp, Lên đưa về', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_hero1'));

    fireEvent.click(action('Xuống'));
    expect(topLevelIds()).toEqual(['c_sect1', 'c_hero1', 'c_grid1', 'c_gall1']);
    // vẫn đang chọn đúng khối vừa chuyển
    expect(nodeEl('c_hero1')).toHaveAttribute('data-selected', 'true');

    fireEvent.click(action('Lên'));
    expect(topLevelIds()).toEqual(['c_hero1', 'c_sect1', 'c_grid1', 'c_gall1']);
  });

  it('khối đầu không Lên được, khối cuối không Xuống được', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');

    fireEvent.click(nodeEl('c_hero1'));
    expect(action('Lên')).toBeDisabled();
    expect(action('Xuống')).toBeEnabled();

    fireEvent.click(nodeEl('c_gall1'));
    expect(action('Xuống')).toBeDisabled();
    expect(action('Lên')).toBeEnabled();
  });

  it('khối con duy nhất của Section: cả hai nút đều tắt (không rời Section bằng nút này)', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_rich1'));

    expect(action('Lên')).toBeDisabled();
    expect(action('Xuống')).toBeDisabled();
  });

  it('di chuyển rồi Hoàn tác → về thứ tự cũ', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_gall1'));
    fireEvent.click(action('Lên'));
    expect(topLevelIds()).toEqual(['c_hero1', 'c_sect1', 'c_gall1', 'c_grid1']);

    fireEvent.click(screen.getByRole('button', { name: 'Hoàn tác' }));

    expect(topLevelIds()).toEqual(['c_hero1', 'c_sect1', 'c_grid1', 'c_gall1']);
  });

  it('trang System: không có nút thao tác cấu trúc', async () => {
    openFixture(validSystemContact);
    await screen.findByText('Liên hệ với chúng tôi');
    fireEvent.click(nodeEl('c_cont1'));

    expect(screen.queryByRole('group', { name: 'Thao tác với khối' })).not.toBeInTheDocument();
  });
});

describe('Xoá khối (F7)', () => {
  it('nút Xoá khối gỡ node, bỏ chọn, Inspector về gợi ý', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_grid1'));

    fireEvent.click(action('Xoá khối'));

    expect(topLevelIds()).toEqual(['c_hero1', 'c_sect1', 'c_gall1']);
    expect(screen.getByText(/Chọn một khối trên trang/)).toBeInTheDocument();
  });

  it('xoá Section kéo theo cả con của nó', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_sect1'));

    fireEvent.click(action('Xoá khối'));

    expect(canvasNodeIds()).not.toContain('c_rich1');
    expect(canvasNodeIds()).not.toContain('c_sect1');
  });

  it('phím Delete xoá khối đang chọn; không có khối chọn thì không làm gì', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');

    fireEvent.keyDown(document.body, { key: 'Delete' });
    expect(topLevelIds()).toHaveLength(4);

    fireEvent.click(nodeEl('c_grid1'));
    fireEvent.keyDown(document.body, { key: 'Delete' });
    expect(topLevelIds()).toEqual(['c_hero1', 'c_sect1', 'c_gall1']);
  });

  it('Delete bị bỏ qua khi đang focus trong ô nhập (đang xoá chữ, không phải xoá khối)', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_hero1'));
    const input = screen.getByDisplayValue('Chào mừng');
    input.focus();

    fireEvent.keyDown(input, { key: 'Delete' });

    expect(topLevelIds()).toContain('c_hero1');
  });

  it('xoá rồi Hoàn tác → khối trở lại đúng chỗ', async () => {
    openFixture(validHome);
    await screen.findByText('Chào mừng');
    fireEvent.click(nodeEl('c_sect1'));
    fireEvent.click(action('Xoá khối'));

    fireEvent.click(screen.getByRole('button', { name: 'Hoàn tác' }));

    expect(topLevelIds()).toEqual(['c_hero1', 'c_sect1', 'c_grid1', 'c_gall1']);
    expect(nodeEl('c_sect1').contains(nodeEl('c_rich1'))).toBe(true);
  });

  it('trang System: Delete không xoá được khối', async () => {
    openFixture(validSystemContact);
    await screen.findByText('Liên hệ với chúng tôi');
    fireEvent.click(nodeEl('c_cont1'));

    fireEvent.keyDown(document.body, { key: 'Delete' });

    expect(nodeEl('c_cont1')).toBeInTheDocument();
  });
});
