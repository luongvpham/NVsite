import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import {
  $getRoot,
  $getSelection,
  $isRangeSelection,
  $selectAll,
  KEY_ENTER_COMMAND,
  type LexicalEditor,
} from 'lexical';
import { describe, expect, it, vi } from 'vitest';
import RichTextEditor from './RichTextEditor';

type EditableEl = HTMLElement & { __lexicalEditor?: LexicalEditor };

/** Async: đợi editor commit xong trạng thái khởi tạo — update trước đó bị gắn `history-merge` và OnChangePlugin bỏ qua (người dùng thật không gặp). */
async function setup(props: Partial<React.ComponentProps<typeof RichTextEditor>> = {}) {
  const onChange = vi.fn<(html: string) => void>();
  const utils = render(
    <RichTextEditor value="" onChange={onChange} profile="basic" label="Nội dung" {...props} />,
  );
  await act(async () => { await new Promise((r) => setTimeout(r, 0)); });
  const editable = screen.getByTestId<EditableEl>('richtext-editable');
  const editor = editable.__lexicalEditor;
  if (!editor) throw new Error('Không lấy được editor Lexical từ phần tử contenteditable');
  return { onChange, editor, editable, ...utils };
}

/** Chọn toàn bộ nội dung bằng API của Lexical (jsdom không có selection thật của người dùng). */
const selectAll = (editor: LexicalEditor) =>
  act(async () => {
    editor.update(() => { $selectAll(); }, { discrete: true });
    await Promise.resolve();
  });

const lastHtml = (fn: ReturnType<typeof vi.fn<(html: string) => void>>) => fn.mock.calls.at(-1)?.[0];
const toolbar = () => screen.getByRole('toolbar', { name: 'Định dạng văn bản' });

describe('RichTextEditor — toolbar theo profile', () => {
  it('basic: đủ nút định dạng + link + chọn khối (chỉ h2–h4, trích dẫn, 2 loại danh sách, đoạn)', async () => {
    await setup({ profile: 'basic' });
    for (const name of ['In đậm', 'In nghiêng', 'Gạch chân', 'Gạch ngang', 'Liên kết']) {
      expect(within(toolbar()).getByRole('button', { name })).toBeInTheDocument();
    }
    const options = within(within(toolbar()).getByRole('combobox', { name: 'Kiểu khối' })).getAllByRole('option');
    expect(options.map((o) => o.textContent)).toEqual([
      'Đoạn văn',
      'Tiêu đề 2',
      'Tiêu đề 3',
      'Tiêu đề 4',
      'Trích dẫn',
      'Danh sách chấm',
      'Danh sách số',
    ]);
  });

  it('inline: có định dạng chữ + link, KHÔNG có chọn khối (không heading/list)', async () => {
    await setup({ profile: 'inline' });
    expect(within(toolbar()).getByRole('button', { name: 'In đậm' })).toBeInTheDocument();
    expect(within(toolbar()).getByRole('button', { name: 'Liên kết' })).toBeInTheDocument();
    expect(within(toolbar()).queryByRole('combobox')).not.toBeInTheDocument();
  });

  it('profile lạ → ném lỗi (không âm thầm cho mọi thẻ)', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    expect(() => render(<RichTextEditor value="" onChange={() => undefined} profile="nope" label="x" />)).toThrow(/profile/);
    spy.mockRestore();
  });
});

describe('RichTextEditor — nạp + phát thay đổi', () => {
  it('nạp HTML đã lưu: hiện đúng nội dung/định dạng, KHÔNG phát onChange lúc mở', async () => {
    const { onChange, editable } = await setup({
      value: '<h2>Giới thiệu</h2><p>Xin <strong>chào</strong></p><ul><li>một</li></ul>',
    });
    expect(within(editable).getByRole('heading', { level: 2, name: 'Giới thiệu' })).toBeInTheDocument();
    expect(editable.querySelector('strong')).toHaveTextContent('chào');
    expect(editable.querySelector('ul li')).toHaveTextContent('một');
    await act(async () => { await new Promise((r) => setTimeout(r, 30)); });
    expect(onChange).not.toHaveBeenCalled();
  });

  it('nội dung độc đã lưu không vào được editor (sanitize trước khi nạp)', async () => {
    const { editable } = await setup({ value: '<p>an toàn</p><script>alert(1)</script><p onclick="x()">b</p>' });
    expect(editable.innerHTML).not.toMatch(/script|onclick|alert/i);
    expect(editable).toHaveTextContent('an toàn');
    expect(editable).toHaveTextContent('b');
  });

  it('bấm In đậm khi chọn hết → onChange nhận HTML đã sanitize có <strong>', async () => {
    const { onChange, editor } = await setup({ value: '<p>Xin chào</p>' });
    await selectAll(editor);
    fireEvent.click(within(toolbar()).getByRole('button', { name: 'In đậm' }));
    await waitFor(() => { expect(onChange).toHaveBeenCalled(); });
    expect(lastHtml(onChange)).toBe('<p><strong>Xin chào</strong></p>');
    expect(within(toolbar()).getByRole('button', { name: 'In đậm' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('chọn "Tiêu đề 2" → <h2>; chọn "Danh sách chấm" → <ul>', async () => {
    const { onChange, editor } = await setup({ value: '<p>Mục</p>' });
    await selectAll(editor);
    fireEvent.change(within(toolbar()).getByRole('combobox', { name: 'Kiểu khối' }), { target: { value: 'h2' } });
    await waitFor(() => { expect(lastHtml(onChange)).toBe('<h2>Mục</h2>'); });

    await selectAll(editor);
    fireEvent.change(within(toolbar()).getByRole('combobox', { name: 'Kiểu khối' }), { target: { value: 'ul' } });
    await waitFor(() => { expect(lastHtml(onChange)).toBe('<ul><li>Mục</li></ul>'); });
  });

  it('xoá hết chữ → onChange("") chứ không phải "<p><br /></p>"', async () => {
    const { onChange, editor } = await setup({ value: '<p>Xin chào</p>' });
    await act(async () => {
      editor.update(() => { $getRoot().clear(); }, { discrete: true });
      await Promise.resolve();
    });
    await waitFor(() => { expect(onChange).toHaveBeenCalledWith(''); });
  });

  it('gõ chữ chứa thẻ → thoát thành chữ thường, không thành HTML', async () => {
    const { onChange, editor } = await setup({ value: '<p>a</p>' });
    await act(async () => {
      editor.update(
        () => {
          $selectAll();
          const sel = $getSelection();
          if ($isRangeSelection(sel)) sel.insertText('<script>alert(1)</script>');
        },
        { discrete: true },
      );
      await Promise.resolve();
    });
    await waitFor(() => { expect(onChange).toHaveBeenCalled(); });
    expect(lastHtml(onChange)).not.toMatch(/<script/i);
    expect(lastHtml(onChange)).toContain('&lt;script&gt;');
  });
});

describe('RichTextEditor — profile inline', () => {
  it('Enter chèn xuống dòng (<br>), không tách đoạn; <br> thừa cuối dòng không tạo giá trị mới', async () => {
    const { onChange, editor, editable } = await setup({ profile: 'inline', value: 'Dòng một' });
    const typeAtEnd = (text: string) =>
      act(async () => {
        editor.update(
          () => {
            $getRoot().selectEnd();
            const sel = $getSelection();
            if ($isRangeSelection(sel) && text) sel.insertText(text);
          },
          { discrete: true },
        );
        await Promise.resolve();
      });

    await act(async () => {
      editor.update(() => { $getRoot().selectEnd(); }, { discrete: true });
      editor.dispatchCommand(KEY_ENTER_COMMAND, new KeyboardEvent('keydown', { key: 'Enter' }));
      await Promise.resolve();
    });
    await act(async () => { await new Promise((r) => setTimeout(r, 30)); });
    expect(onChange).not.toHaveBeenCalled(); // Enter ở cuối: nội dung thật không đổi
    // Cùng một đoạn (xuống dòng bằng <br>), không tách thành hai đoạn trong editor.
    expect(editable.querySelectorAll('p')).toHaveLength(1);
    expect(editable.querySelectorAll('p br').length).toBeGreaterThan(0);

    await typeAtEnd('x');
    await waitFor(() => { expect(onChange).toHaveBeenCalled(); });
    expect(lastHtml(onChange)).toBe('Dòng một<br />x');
  });
});

describe('RichTextEditor — link', () => {
  async function openLinkForm(editor: LexicalEditor) {
    await selectAll(editor);
    fireEvent.click(within(toolbar()).getByRole('button', { name: 'Liên kết' }));
    return screen.getByRole('textbox', { name: 'Địa chỉ liên kết' });
  }

  it('URL hợp lệ → <a href> trong HTML phát ra', async () => {
    const { onChange, editor } = await setup({ value: '<p>xem đây</p>' });
    const input = await openLinkForm(editor);
    fireEvent.change(input, { target: { value: 'https://example.com/a' } });
    fireEvent.click(screen.getByRole('button', { name: 'Áp dụng' }));
    await waitFor(() => { expect(onChange).toHaveBeenCalled(); });
    expect(lastHtml(onChange)).toBe('<p><a href="https://example.com/a" rel="noreferrer">xem đây</a></p>');
  });

  it.each(['javascript:alert(1)', 'JaVaScRiPt:alert(1)', 'data:text/html;base64,AAAA', 'vbscript:x', '//evil.com', 'ftp://x.com'])(
    'URL không an toàn %s → báo lỗi, KHÔNG phát onChange',
    async (bad) => {
      const { onChange, editor } = await setup({ value: '<p>xem đây</p>' });
      const input = await openLinkForm(editor);
      fireEvent.change(input, { target: { value: bad } });
      fireEvent.click(screen.getByRole('button', { name: 'Áp dụng' }));
      expect(await screen.findByRole('alert')).toHaveTextContent(/Liên kết không hợp lệ/);
      expect(input).toHaveAttribute('aria-invalid', 'true');
      await act(async () => { await new Promise((r) => setTimeout(r, 30)); });
      expect(onChange).not.toHaveBeenCalled();
    },
  );

  it('gỡ liên kết trên chữ đang có link', async () => {
    const { onChange, editor } = await setup({ value: '<p><a href="https://x.vn">xem đây</a></p>' });
    await selectAll(editor);
    fireEvent.click(within(toolbar()).getByRole('button', { name: 'Liên kết' }));
    fireEvent.click(screen.getByRole('button', { name: 'Gỡ liên kết' }));
    await waitFor(() => { expect(onChange).toHaveBeenCalled(); });
    expect(lastHtml(onChange)).toBe('<p>xem đây</p>');
  });
});

describe('RichTextEditor — đồng bộ với giá trị bên ngoài', () => {
  it('value đổi từ ngoài (undo của cây) → editor nạp lại, không phát lại onChange', async () => {
    const { onChange, editable, rerender } = await setup({ value: '<p>bản một</p>' });
    expect(editable).toHaveTextContent('bản một');
    rerender(<RichTextEditor value="<p>bản hai</p>" onChange={onChange} profile="basic" label="Nội dung" />);
    await waitFor(() => { expect(editable).toHaveTextContent('bản hai'); });
    await act(async () => { await new Promise((r) => setTimeout(r, 30)); });
    expect(onChange).not.toHaveBeenCalled();
  });

  it('value trùng chính cái vừa phát (vòng lặp qua store) → không nạp lại, giữ con trỏ', async () => {
    const { onChange, editor, rerender } = await setup({ value: '<p>Xin chào</p>' });
    await selectAll(editor);
    fireEvent.click(within(toolbar()).getByRole('button', { name: 'In đậm' }));
    await waitFor(() => { expect(onChange).toHaveBeenCalledTimes(1); });
    const emitted = lastHtml(onChange) ?? '';
    const spy = vi.spyOn(editor, 'update');
    rerender(<RichTextEditor value={emitted} onChange={onChange} profile="basic" label="Nội dung" />);
    await act(async () => { await Promise.resolve(); });
    expect(spy).not.toHaveBeenCalled();
    expect(onChange).toHaveBeenCalledTimes(1);
  });
});

describe('RichTextEditor — giới hạn độ dài', () => {
  it('hiện bộ đếm; quá giới hạn thì báo đỏ', async () => {
    const { editor } = await setup({ value: '<p>abc</p>', maxLength: 10 });
    expect(screen.getByTestId('richtext-count')).toHaveTextContent('/10 ký tự');
    expect(screen.getByTestId('richtext-count')).not.toHaveClass('text-destructive');
    await act(async () => {
      editor.update(
        () => {
          $selectAll();
          const sel = $getSelection();
          if ($isRangeSelection(sel)) sel.insertText('x'.repeat(40));
        },
        { discrete: true },
      );
      await Promise.resolve();
    });
    await waitFor(() => { expect(screen.getByTestId('richtext-count')).toHaveClass('text-destructive'); });
  });
});
