import { act, fireEvent, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { setAccessToken } from '@vsite/api-sdk';
import { useSessionStore } from '../stores/session-store';
import { renderApp } from '../test/render-app';
import { renderWithQuery } from '../test/render-with-query';
import { server } from '../test/msw-server';
import { JobsDashboardButton } from './jobs-dashboard-button';

// JOBS-001 F1 — nút "Job nền" cho PlatformAdmin (#114). Claim `role` chỉ để hiện/ẩn; quyền thật do BE kiểm (403).

const b64url = (value: string) => {
  const bytes = new TextEncoder().encode(value);
  return btoa(String.fromCharCode(...bytes)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
};
const jwt = (payload: unknown) => `${b64url('{"alg":"none"}')}.${b64url(JSON.stringify(payload))}.sig`;
const adminToken = () => jwt({ role: 'PlatformAdmin' });
const BUTTON = { name: 'Job nền' };
const ENDPOINT = '/api/platform/jobs/dashboard-session';

let openSpy: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  openSpy = vi.spyOn(window, 'open').mockReturnValue(null);
});
afterEach(() => {
  vi.restoreAllMocks();
  useSessionStore.getState().clearSession();
  setAccessToken(undefined);
});

describe('hiện/ẩn theo claim role', () => {
  it('có role PlatformAdmin → hiện', () => {
    setAccessToken(adminToken());
    renderWithQuery(<JobsDashboardButton />);
    expect(screen.getByRole('button', BUTTON)).toBeInTheDocument();
  });

  it('role là mảng có PlatformAdmin → hiện', () => {
    setAccessToken(jwt({ role: ['PlatformUser', 'PlatformAdmin'] }));
    renderWithQuery(<JobsDashboardButton />);
    expect(screen.getByRole('button', BUTTON)).toBeInTheDocument();
  });

  it('không role → ẩn', () => {
    setAccessToken(jwt({ sub: 'u' }));
    renderWithQuery(<JobsDashboardButton />);
    expect(screen.queryByRole('button', BUTTON)).not.toBeInTheDocument();
  });

  it('role khác → ẩn', () => {
    setAccessToken(jwt({ role: 'PlatformUser' }));
    renderWithQuery(<JobsDashboardButton />);
    expect(screen.queryByRole('button', BUTTON)).not.toBeInTheDocument();
  });

  it('token hỏng / không token → ẩn', () => {
    setAccessToken('rac.roi.token');
    const { unmount } = renderWithQuery(<JobsDashboardButton />);
    expect(screen.queryByRole('button', BUTTON)).not.toBeInTheDocument();
    unmount();
    setAccessToken(undefined);
    renderWithQuery(<JobsDashboardButton />);
    expect(screen.queryByRole('button', BUTTON)).not.toBeInTheDocument();
  });

  it('token đổi sau refresh: không role → có role ⇒ hiện, rồi mất role ⇒ ẩn', () => {
    setAccessToken(jwt({}));
    renderWithQuery(<JobsDashboardButton />);
    expect(screen.queryByRole('button', BUTTON)).not.toBeInTheDocument();

    act(() => {
      setAccessToken(adminToken());
    });
    expect(screen.getByRole('button', BUTTON)).toBeInTheDocument();

    act(() => {
      setAccessToken(jwt({}));
    });
    expect(screen.queryByRole('button', BUTTON)).not.toBeInTheDocument();
  });
});

describe('bấm nút', () => {
  it('204 → window.open("/hangfire", "_blank"), POST không body, không hiện lỗi', async () => {
    setAccessToken(adminToken());
    let calls = 0;
    server.use(
      http.post(ENDPOINT, () => {
        calls += 1;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithQuery(<JobsDashboardButton />);

    fireEvent.click(screen.getByRole('button', BUTTON));

    await waitFor(() => {
      expect(openSpy).toHaveBeenCalledWith('/hangfire', '_blank');
    });
    expect(calls).toBe(1);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByRole('button', BUTTON)).toBeEnabled();
  });

  it('disabled trong lúc đang gọi', async () => {
    setAccessToken(adminToken());
    let release!: () => void;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.post(ENDPOINT, async () => {
        await gate;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithQuery(<JobsDashboardButton />);

    fireEvent.click(screen.getByRole('button', BUTTON));
    await waitFor(() => {
      expect(screen.getByRole('button', BUTTON)).toBeDisabled();
    });
    release();
    await waitFor(() => {
      expect(screen.getByRole('button', BUTTON)).toBeEnabled();
    });
  });

  it('403 PLATFORM_ADMIN_REQUIRED → thông báo không có quyền + nút biến mất, không mở tab', async () => {
    setAccessToken(adminToken());
    server.use(
      http.post(ENDPOINT, () =>
        HttpResponse.json({ status: 403, error_code: 'PLATFORM_ADMIN_REQUIRED', title: 'Forbidden' }, { status: 403 }),
      ),
    );
    renderWithQuery(<JobsDashboardButton />);

    fireEvent.click(screen.getByRole('button', BUTTON));

    expect(await screen.findByRole('alert')).toHaveTextContent('Bạn không có quyền xem job nền');
    expect(screen.queryByRole('button', BUTTON)).not.toBeInTheDocument();
    expect(openSpy).not.toHaveBeenCalled();
  });

  it('mọi 403 (body chỉ có status, không error_code) → coi là không có quyền', async () => {
    setAccessToken(adminToken());
    server.use(http.post(ENDPOINT, () => HttpResponse.json({ title: 'Forbidden', status: 403 }, { status: 403 })));
    renderWithQuery(<JobsDashboardButton />);

    fireEvent.click(screen.getByRole('button', BUTTON));

    expect(await screen.findByRole('alert')).toHaveTextContent('Bạn không có quyền xem job nền');
    expect(screen.queryByRole('button', BUTTON)).not.toBeInTheDocument();
  });

  it('403 body rỗng → cũng không có quyền', async () => {
    setAccessToken(adminToken());
    server.use(http.post(ENDPOINT, () => new HttpResponse(null, { status: 403 })));
    renderWithQuery(<JobsDashboardButton />);

    fireEvent.click(screen.getByRole('button', BUTTON));

    expect(await screen.findByRole('alert')).toHaveTextContent('Bạn không có quyền xem job nền');
    expect(screen.queryByRole('button', BUTTON)).not.toBeInTheDocument();
  });

  it('500 → thông báo lỗi chung, nút còn, bấm lại gọi lại được', async () => {
    setAccessToken(adminToken());
    let calls = 0;
    server.use(
      http.post(ENDPOINT, () => {
        calls += 1;
        return calls === 1
          ? HttpResponse.json({ status: 500, error_code: 'INTERNAL_ERROR' }, { status: 500 })
          : new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithQuery(<JobsDashboardButton />);

    fireEvent.click(screen.getByRole('button', BUTTON));
    const alert = await screen.findByRole('alert');
    expect(alert).not.toHaveTextContent('không có quyền');
    expect(screen.getByRole('button', BUTTON)).toBeEnabled();

    fireEvent.click(screen.getByRole('button', BUTTON));
    await waitFor(() => {
      expect(openSpy).toHaveBeenCalledWith('/hangfire', '_blank');
    });
    expect(calls).toBe(2);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('lỗi mạng → thông báo lỗi chung, nút còn, bấm lại được', async () => {
    setAccessToken(adminToken());
    let calls = 0;
    server.use(
      http.post(ENDPOINT, () => {
        calls += 1;
        return calls === 1 ? HttpResponse.error() : new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithQuery(<JobsDashboardButton />);

    fireEvent.click(screen.getByRole('button', BUTTON));
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('button', BUTTON)).toBeEnabled();

    fireEvent.click(screen.getByRole('button', BUTTON));
    await waitFor(() => {
      expect(openSpy).toHaveBeenCalledTimes(1);
    });
  });
});

describe('layout _authenticated', () => {
  it('header có nút "Job nền" khi token có role PlatformAdmin, và không có khi thiếu', async () => {
    useSessionStore.getState().setSession({ accessToken: adminToken() });
    server.use(http.get('/api/shops', () => HttpResponse.json([])));
    const { unmount } = renderApp('/shops');
    expect(await screen.findByRole('button', BUTTON)).toBeInTheDocument();
    unmount();

    useSessionStore.getState().setSession({ accessToken: jwt({}) });
    renderApp('/shops');
    await screen.findByRole('button', { name: 'Đăng xuất' });
    expect(screen.queryByRole('button', BUTTON)).not.toBeInTheDocument();
  });
});
