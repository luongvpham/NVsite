import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { CropFrame } from './crop-frame';

function loadImage(width: number, height: number) {
  const img = screen.getByAltText('ảnh');
  Object.defineProperty(img, 'naturalWidth', { value: width, configurable: true });
  Object.defineProperty(img, 'naturalHeight', { value: height, configurable: true });
  fireEvent.load(img);
}

function mockArea(width: number, height: number) {
  const area = screen.getByTestId('crop-area');
  area.getBoundingClientRect = () =>
    ({ left: 0, top: 0, width, height, right: width, bottom: height, x: 0, y: 0, toJSON: () => ({}) });
  return area;
}

describe('CropFrame (F11)', () => {
  it('preset cover: khung đúng tỉ lệ preset (ảnh 1:1, preset 16:9 → 100% × 56.25%)', () => {
    render(<CropFrame src="x.jpg" alt="ảnh" preset="1600x900,cover" focal={{ x: 0.5, y: 0.5 }} onFocalChange={vi.fn()} />);
    expect(screen.queryByTestId('crop-frame')).not.toBeInTheDocument(); // chưa biết kích thước ảnh
    loadImage(1000, 1000);
    const frame = screen.getByTestId('crop-frame');
    expect(frame.style.width).toBe('100%');
    expect(parseFloat(frame.style.height)).toBeCloseTo(56.25);
  });

  it('preset inside: không khung, không vùng kéo', () => {
    render(<CropFrame src="x.jpg" alt="ảnh" preset="1200x1200,inside" focal={{ x: 0.5, y: 0.5 }} onFocalChange={vi.fn()} />);
    loadImage(1000, 500);
    expect(screen.queryByTestId('crop-frame')).not.toBeInTheDocument();
    expect(screen.queryByTestId('crop-area')).not.toBeInTheDocument();
  });

  it('kéo khung → focal = tâm khung (kẹp trong ảnh)', () => {
    const onFocalChange = vi.fn();
    // ảnh 2:1, preset vuông → khung rộng 50% × cao 100%
    render(<CropFrame src="x.jpg" alt="ảnh" preset="800x800,cover" focal={{ x: 0.5, y: 0.5 }} onFocalChange={onFocalChange} />);
    loadImage(2000, 1000);
    const area = mockArea(200, 100);

    fireEvent.pointerDown(area, { clientX: 120, clientY: 50, pointerId: 1 });
    expect(onFocalChange).toHaveBeenLastCalledWith({ x: 0.6, y: 0.5 });

    fireEvent.pointerMove(area, { clientX: 190, clientY: 10, pointerId: 1 });
    expect(onFocalChange).toHaveBeenLastCalledWith({ x: 0.75, y: 0.5 }); // kẹp: khung rộng 0.5 → tâm tối đa 0.75

    fireEvent.pointerUp(area, { pointerId: 1 });
    onFocalChange.mockClear();
    fireEvent.pointerMove(area, { clientX: 20, clientY: 50, pointerId: 1 });
    expect(onFocalChange).not.toHaveBeenCalled(); // thả rồi thì không kéo nữa
  });

  it('focal cũ nằm sát mép → kéo vào trong ngay khi biết kích thước ảnh (giá trị gửi = khung hiện)', () => {
    const onFocalChange = vi.fn();
    render(<CropFrame src="x.jpg" alt="ảnh" preset="800x800,cover" focal={{ x: 0.98, y: 0.5 }} onFocalChange={onFocalChange} />);
    loadImage(2000, 1000);
    expect(onFocalChange).toHaveBeenCalledWith({ x: 0.75, y: 0.5 });
  });
});
