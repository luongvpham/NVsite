import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';
import { useSessionStore } from '../stores/session-store';
import { server } from '../test/msw-server';
import { defaultTheme, openFixture, themeBody, validHome } from '../test/editor-helpers';

const THEME_URL = '*/api/shops/:shopId/website/theme';

afterEach(() => {
  useSessionStore.getState().clearSession();
});

interface PutBody {
  presetKey: string | null;
  tokens: { colors: Record<string, string>; shape: Record<string, string> };
}

function mockPut(respond: () => Response = () => HttpResponse.json(themeBody())) {
  const bodies: PutBody[] = [];
  server.use(
    http.put(THEME_URL, async ({ request }) => {
      bodies.push((await request.json()) as PutBody);
      return respond();
    }),
  );
  return bodies;
}

const problem = (status: number, error_code: string, params?: unknown) => () =>
  HttpResponse.json({ status, error_code, title: error_code, params }, { status });

type Responder = () => Response | Promise<Response>;

async function openThemeTab(theme?: Responder) {
  openFixture(validHome, undefined, theme);
  await screen.findByText('Chào mừng');
  fireEvent.click(screen.getByRole('tab', { name: /Giao diện/ }));
}

const canvasVar = (name: string) => screen.getByTestId('editor-canvas').style.getPropertyValue(name);
const colorText = (label: string) => screen.getByLabelText(label, { selector: 'input[type="text"]' });
const saveButton = () => screen.getByRole('button', { name: 'Lưu theme' });
// Toolbar cũng có nút "Hoàn tác" (undo cây) — nút của panel nằm trong form.
const revertButton = () =>
  within(screen.getByRole('form', { name: 'Giao diện website' })).getByRole('button', { name: 'Hoàn tác' });

describe('Theme panel — nạp', () => {
  it('đang tải → "Đang tải giao diện"; xong → control lấy giá trị từ theme đã lưu', async () => {
    await openThemeTab(async () => {
      await delay(150);
      return HttpResponse.json(themeBody({ ...defaultTheme, colors: { ...defaultTheme.colors, primary: '#112233' } }));
    });
    expect(await screen.findByText('Đang tải giao diện...')).toBeInTheDocument();
    expect(await screen.findByLabelText('Màu chính', { selector: 'input[type="text"]' })).toHaveValue('#112233');
    // Canvas đã vẽ theme đã lưu ngay từ lúc mở phiên (không đợi mở tab).
    expect(canvasVar('--vs-color-primary')).toBe('#112233');
    expect(saveButton()).toBeDisabled();
  });

  it('404 WEBSITE_NOT_FOUND → báo, không có form và không có nút thử lại', async () => {
    await openThemeTab(problem(404, 'WEBSITE_NOT_FOUND'));
    expect(await screen.findByText('Shop này chưa có website.')).toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Giao diện website' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Thử lại' })).not.toBeInTheDocument();
  });

  it('lỗi khác → báo + Thử lại; thử lại thành công thì có form', async () => {
    let calls = 0;
    await openThemeTab(() => {
      calls += 1;
      return calls === 1 ? problem(500, 'INTERNAL_ERROR')() : HttpResponse.json(themeBody());
    });
    fireEvent.click(await screen.findByRole('button', { name: 'Thử lại' }));
    expect(await screen.findByRole('form', { name: 'Giao diện website' })).toBeInTheDocument();
  });

  it('tokens không qua Zod → "không đọc được", không cho sửa', async () => {
    await openThemeTab(() => HttpResponse.json(themeBody({ colors: 'sai' })));
    expect(await screen.findByText(/Cấu hình giao diện không đọc được/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Lưu theme' })).not.toBeInTheDocument();
  });

  it('control chọn lấy đúng tập giá trị của config', async () => {
    await openThemeTab();
    const radius = await screen.findByLabelText('Bo góc');
    expect(within(radius).getAllByRole('option').map((o) => (o as HTMLOptionElement).value)).toEqual([
      'none',
      'sm',
      'md',
      'lg',
      'full',
    ]);
    const fonts = screen.getByLabelText('Font tiêu đề');
    expect(within(fonts).getAllByRole('option')).toHaveLength(6);
  });
});

describe('Theme panel — sửa + xem trước', () => {
  it('đổi màu bằng ô hex hợp lệ → biến CSS của canvas đổi ngay, chưa gọi PUT', async () => {
    const bodies = mockPut();
    await openThemeTab();
    const input = await screen.findByLabelText('Màu chính', { selector: 'input[type="text"]' });
    fireEvent.change(input, { target: { value: '#ff0000' } });

    expect(canvasVar('--vs-color-primary')).toBe('#ff0000');
    expect(saveButton()).toBeEnabled();
    expect(bodies).toHaveLength(0);
  });

  it('đổi bằng ô chọn màu cũng đổi canvas', async () => {
    await openThemeTab();
    await screen.findByRole('form', { name: 'Giao diện website' });
    fireEvent.change(screen.getByLabelText('Nền trang (bảng màu)'), { target: { value: '#001122' } });
    expect(canvasVar('--vs-color-background')).toBe('#001122');
  });

  it('đổi select (bo góc) → --vs-radius đổi', async () => {
    await openThemeTab();
    fireEvent.change(await screen.findByLabelText('Bo góc'), { target: { value: 'full' } });
    expect(canvasVar('--vs-radius')).toBe('9999px');
  });

  it('màu sai khuôn bị chặn ở client: báo lỗi, canvas giữ màu cũ, không bật Lưu', async () => {
    await openThemeTab();
    const input = await screen.findByLabelText('Màu chính', { selector: 'input[type="text"]' });
    const before = canvasVar('--vs-color-primary');

    for (const bad of ['#fff', 'red', '#12345g', 'url(x)']) {
      fireEvent.change(input, { target: { value: bad } });
      expect(canvasVar('--vs-color-primary')).toBe(before);
    }
    expect(screen.getByText(/Màu phải có dạng #RRGGBB/)).toBeInTheDocument();
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(saveButton()).toBeDisabled();

    // Rời ô (blur) → trả về màu hợp lệ gần nhất.
    fireEvent.blur(input);
    expect(input).toHaveValue(before);
  });

  it('"Hoàn tác" → về bản đã lưu (ô + canvas), hết chưa lưu', async () => {
    await openThemeTab();
    const input = await screen.findByLabelText('Màu chính', { selector: 'input[type="text"]' });
    const original = canvasVar('--vs-color-primary');
    fireEvent.change(input, { target: { value: '#ff0000' } });
    fireEvent.change(screen.getByLabelText('Bo góc'), { target: { value: 'none' } });

    fireEvent.click(revertButton());

    expect(canvasVar('--vs-color-primary')).toBe(original);
    expect(colorText('Màu chính')).toHaveValue(original);
    expect(screen.getByLabelText('Bo góc')).toHaveValue('md');
    expect(saveButton()).toBeDisabled();
  });

  it('sửa theme không làm bước undo của cây (Hoàn tác trên toolbar vẫn tắt)', async () => {
    await openThemeTab();
    fireEvent.change(await screen.findByLabelText('Bo góc'), { target: { value: 'full' } });
    expect(screen.getByRole('button', { name: 'Hoàn tác', description: /Ctrl\+Z/ })).toBeDisabled();
  });
});

describe('Theme panel — lưu', () => {
  it('"Lưu theme" gửi {presetKey giữ nguyên, tokens đã sửa}; thành công → hết chưa lưu', async () => {
    const bodies = mockPut();
    await openThemeTab();
    fireEvent.change(await screen.findByLabelText('Màu chính', { selector: 'input[type="text"]' }), {
      target: { value: '#ff0000' },
    });
    fireEvent.change(screen.getByLabelText('Bo góc'), { target: { value: 'lg' } });

    fireEvent.click(saveButton());

    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]?.presetKey).toBe('classic');
    expect(bodies[0]?.tokens.colors.primary).toBe('#ff0000');
    expect(bodies[0]?.tokens.shape.radius).toBe('lg');
    // Phần không sửa đi nguyên vẹn.
    expect(bodies[0]?.tokens.colors.secondary).toBe(defaultTheme.colors.secondary);
    await waitFor(() => {
      expect(saveButton()).toBeDisabled();
    });
    expect(screen.getByText('Giao diện đã lưu.')).toBeInTheDocument();
    // Hoàn tác sau khi lưu về bản MỚI lưu, không về bản ban đầu.
    expect(canvasVar('--vs-color-primary')).toBe('#ff0000');
  });

  it('presetKey null vẫn gửi null', async () => {
    const bodies = mockPut();
    await openThemeTab(() => HttpResponse.json(themeBody(defaultTheme, null)));
    fireEvent.change(await screen.findByLabelText('Bo góc'), { target: { value: 'sm' } });
    fireEvent.click(saveButton());
    await waitFor(() => {
      expect(bodies).toHaveLength(1);
    });
    expect(bodies[0]?.presetKey).toBeNull();
  });

  it('đang lưu → nút khoá, không gửi hai PUT', async () => {
    let puts = 0;
    server.use(
      http.put(THEME_URL, async () => {
        puts += 1;
        await delay(150);
        return HttpResponse.json(themeBody());
      }),
    );
    await openThemeTab();
    fireEvent.change(await screen.findByLabelText('Bo góc'), { target: { value: 'sm' } });
    fireEvent.click(saveButton());
    expect(await screen.findByRole('button', { name: 'Đang lưu...' })).toBeDisabled();
    fireEvent.submit(screen.getByRole('form', { name: 'Giao diện website' }));
    await waitFor(() => {
      expect(saveButton()).toBeDisabled();
    });
    expect(puts).toBe(1);
  });

  it('422 THEME_INVALID → tô đúng ô theo path, thông báo của BE ở ô đó; path lạ → banner; sửa tiếp thì xoá lỗi', async () => {
    mockPut(
      problem(422, 'THEME_INVALID', {
        errors: [
          { path: '/colors/primary', code: 'THEME_TOKEN_INVALID', message: 'Màu không đúng khuôn.' },
          { path: '/shape/radius', code: 'THEME_TOKEN_INVALID', message: 'Bo góc không hợp lệ.' },
          { path: '/colors/ghost', code: 'THEME_TOKEN_INVALID', message: 'Khoá màu lạ.' },
        ],
        truncated: false,
      }),
    );
    await openThemeTab();
    fireEvent.change(await screen.findByLabelText('Bo góc'), { target: { value: 'sm' } });
    fireEvent.click(saveButton());

    expect(await screen.findByText('Giao diện có giá trị chưa hợp lệ.')).toBeInTheDocument();
    expect(colorText('Màu chính')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Màu không đúng khuôn.')).toBeInTheDocument();
    expect(screen.getByLabelText('Bo góc')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Bo góc không hợp lệ.')).toBeInTheDocument();
    expect(colorText('Nền trang')).not.toHaveAttribute('aria-invalid');
    expect(screen.getByText('Khoá màu lạ.')).toBeInTheDocument();
    // Vẫn còn bản chưa lưu: không mất công của chủ shop.
    expect(saveButton()).toBeEnabled();

    fireEvent.change(screen.getByLabelText('Bo góc'), { target: { value: 'md' } });
    expect(screen.queryByText('Màu không đúng khuôn.')).not.toBeInTheDocument();
  });

  it('413 THEME_BODY_TOO_LARGE → báo rõ, vẫn giữ bản đang sửa', async () => {
    mockPut(problem(413, 'THEME_BODY_TOO_LARGE'));
    await openThemeTab();
    fireEvent.change(await screen.findByLabelText('Bo góc'), { target: { value: 'sm' } });
    fireEvent.click(saveButton());
    expect(await screen.findByText('Cấu hình giao diện quá lớn để lưu.')).toBeInTheDocument();
    expect(saveButton()).toBeEnabled();
    expect(canvasVar('--vs-radius')).toBe('0.25rem');
  });

  it('422 không có params → chỉ thông báo chung, không crash', async () => {
    mockPut(problem(422, 'THEME_INVALID'));
    await openThemeTab();
    fireEvent.change(await screen.findByLabelText('Bo góc'), { target: { value: 'sm' } });
    fireEvent.click(saveButton());
    expect(await screen.findByText('Giao diện có giá trị chưa hợp lệ.')).toBeInTheDocument();
  });
});

describe('Theme panel — rời khi chưa lưu', () => {
  it('chưa lưu mà chuyển tab → nhắc; "Ở lại" giữ tab + giá trị, "Chuyển tab" đi tiếp (tab còn dấu •)', async () => {
    await openThemeTab();
    fireEvent.change(await screen.findByLabelText('Bo góc'), { target: { value: 'full' } });

    fireEvent.click(screen.getByRole('tab', { name: /Thuộc tính/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Giao diện chưa lưu' });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Ở lại để lưu' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Bo góc')).toHaveValue('full');

    fireEvent.click(screen.getByRole('tab', { name: /Thuộc tính/ }));
    fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Chuyển tab' }));
    expect(screen.getByText(/Chọn một khối trên trang/)).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /Giao diện •/ })).toBeInTheDocument();
    // Canvas vẫn là bản xem trước.
    expect(canvasVar('--vs-radius')).toBe('9999px');
  });

  it('theme chưa lưu mà rời trang (link trong app) → hộp "Còn thay đổi chưa lưu"; Ở lại thì vẫn ở editor', async () => {
    await openThemeTab();
    fireEvent.change(await screen.findByLabelText('Bo góc'), { target: { value: 'full' } });

    fireEvent.click(screen.getByRole('link', { name: /Danh sách trang/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Còn thay đổi chưa lưu' });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Ở lại' }));
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
    expect(screen.getByTestId('editor-canvas')).toBeInTheDocument();
  });

  it('theme đã lưu/không đổi → rời trang không bị chặn', async () => {
    await openThemeTab();
    await screen.findByRole('form', { name: 'Giao diện website' });
    fireEvent.click(screen.getByRole('link', { name: /Danh sách trang/ }));
    await waitFor(() => {
      expect(screen.queryByTestId('editor-canvas')).not.toBeInTheDocument();
    });
    expect(screen.queryByRole('dialog', { name: 'Còn thay đổi chưa lưu' })).not.toBeInTheDocument();
  });

  it('không có thay đổi → chuyển tab thẳng, không nhắc', async () => {
    await openThemeTab();
    await screen.findByRole('form', { name: 'Giao diện website' });
    fireEvent.click(screen.getByRole('tab', { name: /Thuộc tính/ }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByText(/Chọn một khối trên trang/)).toBeInTheDocument();
  });

  it('non-Owner không có request theme', async () => {
    let calls = 0;
    openFixture(validHome, 'Manager', () => {
      calls += 1;
      return HttpResponse.json(themeBody());
    });
    expect(await screen.findByText(/Chỉ chủ shop/)).toBeInTheDocument();
    expect(calls).toBe(0);
  });
});
