import { fireEvent, render, screen, within } from '@testing-library/react';
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

  it('isFieldDisabled khoá đúng field được chỉ định, field khác vẫn sửa được', () => {
    render(
      <Inspector
        fields={heroPropertyPanel}
        values={{ title: 'khoá', subtitle: 'mở' }}
        onChange={vi.fn()}
        isFieldDisabled={(field) => field.name === 'title'}
      />,
    );

    expect(screen.getByDisplayValue('khoá')).toBeDisabled();
    expect(screen.getByDisplayValue('mở')).toBeEnabled();
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
});
