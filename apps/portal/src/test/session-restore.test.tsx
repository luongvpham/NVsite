import { fireEvent, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';
import type { ShopSummaryDto } from '@vsite/api-sdk';
import { setAccessToken } from '@vsite/api-sdk';
import { useSessionStore } from '../stores/session-store';
import { renderApp } from './render-app';
import { server } from './msw-server';

// IDENTITY-002 / #101 — refresh token là cookie httpOnly, FE không giữ. F5 làm store rỗng; route guard
// phải hỏi BE (refresh bằng cookie) TRƯỚC khi quyết định redirect /login (R7).

afterEach(() => {
  useSessionStore.getState().clearSession();
  setAccessToken(undefined);
});

const shop: ShopSummaryDto = {
  id: 'shop-1',
  name: 'Spa ABC',
  slug: 'spa-abc',
  kind: 'Hosted',
  status: 'Active',
  roleCode: 'Owner',
  logoUrl: null,
};

function mockRefresh(status: 200 | 401) {
  server.use(
    http.post('/api/auth/refresh-token', () =>
      status === 200
        ? HttpResponse.json({ accessToken: 'restored', accessTokenExpiresAt: new Date(Date.now() + 3_600_000).toISOString() })
        : HttpResponse.json({ error_code: 'UNAUTHORIZED', title: 'Unauthorized' }, { status: 401 }),
    ),
  );
}

describe('Khôi phục phiên (F5)', () => {
  it('store rỗng + cookie còn hạn → vào thẳng route con, không qua /login, và request mang token mới', async () => {
    mockRefresh(200);
    let authorization: string | null = null;
    server.use(
      http.get('/api/shops', ({ request }) => {
        authorization = request.headers.get('authorization');
        return HttpResponse.json([shop]);
      }),
    );

    const { router } = renderApp('/shops');

    expect(await screen.findByText('Spa ABC')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/shops');
    expect(authorization).toBe('Bearer restored');
    expect(useSessionStore.getState().isAuthenticated).toBe(true);
  });

  it('store rỗng + refresh 401 (không cookie / hết hạn) → về /login', async () => {
    mockRefresh(401);

    const { router } = renderApp('/shops');

    await screen.findByRole('heading', { name: 'Đăng nhập' });
    expect(router.state.location.pathname).toBe('/login');
    expect(useSessionStore.getState().isAuthenticated).toBe(false);
  });

  it('đã đăng nhập trong phiên này → guard không gọi refresh', async () => {
    let refreshCalls = 0;
    server.use(
      http.post('/api/auth/refresh-token', () => {
        refreshCalls += 1;
        return HttpResponse.json({}, { status: 401 });
      }),
      http.get('/api/shops', () => HttpResponse.json([shop])),
    );
    useSessionStore.getState().setSession({ accessToken: 't' });

    renderApp('/shops');

    expect(await screen.findByText('Spa ABC')).toBeInTheDocument();
    expect(refreshCalls).toBe(0);
  });
});

describe('Đăng xuất', () => {
  async function signedInAtShops() {
    useSessionStore.getState().setSession({ accessToken: 't' });
    server.use(http.get('/api/shops', () => HttpResponse.json([shop])));
    renderApp('/shops');
    await screen.findByText('Spa ABC');
  }

  it('clear state + về /login NGAY, rồi gọi POST /api/auth/logout đúng một lần', async () => {
    let logoutCalls = 0;
    server.use(
      http.post('/api/auth/logout', () => {
        logoutCalls += 1;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await signedInAtShops();

    fireEvent.click(screen.getByRole('button', { name: 'Đăng xuất' }));

    await screen.findByRole('heading', { name: 'Đăng nhập' });
    expect(useSessionStore.getState().isAuthenticated).toBe(false);
    await waitFor(() => {
      expect(logoutCalls).toBe(1);
    });
  });

  it('logout lỗi (mạng/500) vẫn về /login', async () => {
    server.use(http.post('/api/auth/logout', () => HttpResponse.error()));
    await signedInAtShops();

    fireEvent.click(screen.getByRole('button', { name: 'Đăng xuất' }));

    await screen.findByRole('heading', { name: 'Đăng nhập' });
    expect(useSessionStore.getState().isAuthenticated).toBe(false);
  });

  it('logout treo (server không trả lời) không chặn đăng xuất phía FE (I3)', async () => {
    server.use(http.post('/api/auth/logout', () => new Promise<never>(() => undefined)));
    await signedInAtShops();

    fireEvent.click(screen.getByRole('button', { name: 'Đăng xuất' }));

    await screen.findByRole('heading', { name: 'Đăng nhập' });
    expect(useSessionStore.getState().isAuthenticated).toBe(false);
  });
});
