import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { heroPropertyPanel, sectionPropertyPanel } from '../../generated/property-panel';
import { Inspector } from './Inspector';

describe('Inspector (§10 — property-panel.ts đủ metadata để sinh form dùng được)', () => {
  it('renders a control for every Hero field, grouped by field.group', () => {
    render(<Inspector fields={heroPropertyPanel} values={{ title: 'Chào mừng' }} onChange={vi.fn()} />);

    expect(screen.getByDisplayValue('Chào mừng')).toBeInTheDocument();
    expect(screen.getByText('Tiêu đề')).toBeInTheDocument();
    expect(screen.getByText('Nút hành động')).toBeInTheDocument(); // group fieldset (cta)
  });

  it('color-token liệt kê KEY màu của theme per-shop (config/theme-tokens.json), không còn key của theme tĩnh', () => {
    const onChange = vi.fn();
    render(<Inspector fields={sectionPropertyPanel} values={{ background: 'primary' }} onChange={onChange} />);

    const select = screen.getByDisplayValue('primary');
    const options = within(select).getAllByRole('option').map((option) => option.textContent);
    expect(options).toEqual(['primary', 'secondary', 'background', 'surface', 'text', 'textMuted', 'border']);
    expect(options).not.toContain('foreground');
    expect(options).not.toContain('muted');

    fireEvent.change(select, { target: { value: 'surface' } });
    expect(onChange).toHaveBeenCalledWith(['background'], 'surface');
  });

  it('isFieldDisabled khoá đúng field được chỉ định, field khác vẫn sửa được', async () => {
    render(
      <Inspector
        fields={heroPropertyPanel}
        values={{ title: 'khoá', subtitle: 'mở' }}
        onChange={vi.fn()}
        isFieldDisabled={(field) => field.name === 'title'}
      />,
    );

    expect(screen.getByDisplayValue('khoá')).toBeDisabled();
    // `subtitle` là control Lexical (tải lười): contenteditable còn sửa được.
    expect(await screen.findByRole('textbox', { name: 'Mô tả ngắn' })).toHaveAttribute('contenteditable', 'true');
  });

  it('field Lexical bị khoá → contenteditable tắt (fieldset disabled không khoá được nó)', async () => {
    render(
      <Inspector
        fields={heroPropertyPanel}
        values={{ title: 't', subtitle: 'đang khoá' }}
        onChange={vi.fn()}
        isFieldDisabled={(field) => field.name === 'subtitle'}
      />,
    );
    const editable = await screen.findByRole('textbox', { name: 'Mô tả ngắn' });
    await waitFor(() => { expect(editable).toHaveAttribute('contenteditable', 'false'); });
    expect(editable).toHaveAttribute('aria-disabled', 'true');
    expect(screen.getByDisplayValue('t')).toBeEnabled();
  });

  it('field Lexical hiện editor thật theo profile của field (subtitle = inline: không có chọn khối)', async () => {
    render(<Inspector fields={heroPropertyPanel} values={{ subtitle: 'xin chào' }} onChange={vi.fn()} />);
    expect(await screen.findByRole('textbox', { name: 'Mô tả ngắn' })).toHaveTextContent('xin chào');
    expect(screen.getByRole('button', { name: 'In đậm' })).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'Kiểu khối' })).not.toBeInTheDocument();
  });

  it('calls onChange with the correct path when editing the title field', () => {
    const onChange = vi.fn();
    render(<Inspector fields={heroPropertyPanel} values={{ title: 'x' }} onChange={onChange} />);

    fireEvent.change(screen.getByDisplayValue('x'), { target: { value: 'Tiêu đề mới' } });

    expect(onChange).toHaveBeenCalledWith(['title'], 'Tiêu đề mới');
  });

  it('editing a field inside a group (cta.label) reports the merged group value at the group path', () => {
    const onChange = vi.fn();
    render(<Inspector fields={heroPropertyPanel} values={{ cta: { label: 'Đặt lịch' } }} onChange={onChange} />);

    fireEvent.change(screen.getByDisplayValue('Đặt lịch'), { target: { value: 'Liên hệ ngay' } });

    // fieldset (group) tự merge nội bộ bằng setAtPath rồi báo lên giá trị TOÀN BỘ group tại path
    // của chính nó — path không lồng sâu hơn, tránh Inspector cha phải biết cấu trúc con.
    expect(onChange).toHaveBeenCalledWith(['cta'], { label: 'Liên hệ ngay' });
  });
  it('invalidPaths tô đỏ đúng field (cả khi đường dẫn đi sâu hơn field), field khác không bị tô', async () => {
    render(
      <Inspector
        fields={heroPropertyPanel}
        values={{ title: 'a', subtitle: 'b', cta: { label: 'c' } }}
        onChange={vi.fn()}
        invalidPaths={[['title'], ['cta', 'label']]}
      />,
    );
    expect(screen.getByDisplayValue('a').closest('fieldset[disabled], fieldset')?.getAttribute('data-invalid')).toBe('true');
    // `subtitle` (Lexical) không nằm trong invalidPaths → wrapper của nó không bị tô.
    const subtitle = await screen.findByRole('textbox', { name: 'Mô tả ngắn' });
    expect(subtitle.closest('fieldset')?.getAttribute('data-invalid')).toBeNull();
    // `cta.label` nằm trong field `cta` → field `cta` bị tô.
    expect(document.querySelectorAll('[data-invalid="true"]').length).toBe(2);
  });

  it('không có invalidPaths → không field nào bị tô', () => {
    render(<Inspector fields={heroPropertyPanel} values={{ title: 'a' }} onChange={vi.fn()} />);
    expect(document.querySelectorAll('[data-invalid]').length).toBe(0);
  });
});
