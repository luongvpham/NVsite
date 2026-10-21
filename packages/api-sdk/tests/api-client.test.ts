import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import { listShops } from '../src/generated/shop/shop/shop';
import {
  API_PREFIX,
  axiosInstance,
  createApiClient,
  restoreSession,
  setAccessToken,
  setOnSessionExpired,
  waitForPendingRefresh,
} from '../src/mutator/axios-instance';

// REFACTOR-API-001 — chạy trong Node (môi trường SSR thật), MSW chặn request HTTP của axios.
const BE = 'http://be.test';
const seen: { url: string; authorization: string | null; host: string | null }[] = [];

const server = setupServer(
  http.get(`${BE}${API_PREFIX}/shops`, ({ request }) => {
    seen.push({
      url: request.url,
      authorization: request.headers.get('authorization'),
      host: request.headers.get('host'),
    });
    return HttpResponse.json([]);
  }),
  http.get(`${BE}${API_PREFIX}/shops/:shopId`, () =>
    HttpResponse.json({ status: 403, title: 'Forbidden', error_code: 'SHOP_ACCESS_DENIED' }, { status: 403 }),
  ),
);

beforeAll(() => { server.listen({ onUnhandledRequest: 'error' }); });
afterEach(() => {
  server.resetHandlers();
  seen.length = 0;
  setAccessToken(undefined);
});
afterAll(() => { server.close(); });

describe('createApiClient (SSR)', () => {
  it('mỗi client gửi token + Host riêng của nó, không lẫn nhau và không lẫn với instance mặc định', async () => {
    setAccessToken('portal-token'); // state cấp module của instance mặc định — client SSR phải bỏ qua
    // Host gốc của request trình duyệt — BE resolve tenant theo Request.Host (#7).
    const alice = createApiClient({ baseURL: BE, headers: { Host: 'spa-a.vsite.vn' }, getAccessToken: () => 'alice' });
    const bob = createApiClient({ baseURL: BE, headers: { Host: 'spa-b.vsite.vn' }, getAccessToken: () => 'bob' });

    await Promise.all([listShops({ client: alice }), listShops({ client: bob })]);

    expect(seen).toHaveLength(2);
    expect(seen).toEqual(
      expect.arrayContaining([
        { url: `${BE}/api/shops`, authorization: 'Bearer alice', host: 'spa-a.vsite.vn' },
        { url: `${BE}/api/shops`, authorization: 'Bearer bob', host: 'spa-b.vsite.vn' },
      ]),
    );
  });

  it('không có token thì không gửi Authorization', async () => {
    await listShops({ client: createApiClient({ baseURL: BE }) });

    expect(seen[0]?.authorization).toBeNull();
  });

  it('reject bằng body ProblemDetails (có error_code), như instance mặc định (#19)', async () => {
    const { getShop } = await import('../src/generated/shop/shop/shop');

    await expect(getShop('00000000-0000-0000-0000-000000000001', { client: createApiClient({ baseURL: BE }) })).rejects.toMatchObject({
      error_code: 'SHOP_ACCESS_DENIED',
    });
  });
});

describe('instance mặc định (Portal)', () => {
  // #101 — refresh token nằm ở cookie httpOnly: FE không gửi body, chỉ gửi kèm cookie (withCredentials).
  it('401 → gọi /api/auth/refresh-token (không body, kèm cookie) ở đúng baseURL rồi retry với token mới', async () => {
    const base = axiosInstance.defaults.baseURL;
    expect(base).toBeDefined();
    let refreshBody: string | undefined;
    server.use(
      http.get(`${base}${API_PREFIX}/shops`, ({ request }) =>
        request.headers.get('authorization') === 'Bearer fresh'
          ? HttpResponse.json([])
          : HttpResponse.json({ error_code: 'UNAUTHORIZED' }, { status: 401 }),
      ),
      http.post(`${base}${API_PREFIX}/auth/refresh-token`, async ({ request }) => {
        refreshBody = await request.text();
        return HttpResponse.json({ accessToken: 'fresh', accessTokenExpiresAt: '2030-01-01T00:00:00Z' });
      }),
    );
    setAccessToken('stale');

    await expect(listShops()).resolves.toEqual([]);
    expect(refreshBody).toBe('');
  });

  it('instance Portal bật withCredentials; client SSR (createApiClient) thì không', () => {
    expect(axiosInstance.defaults.withCredentials).toBe(true);
    expect(createApiClient({ baseURL: BE }).defaults.withCredentials).not.toBe(true);
  });

  it('refresh 401 → onSessionExpired được gọi và request gốc reject', async () => {
    const base = axiosInstance.defaults.baseURL;
    server.use(
      http.get(`${base}${API_PREFIX}/shops`, () => HttpResponse.json({ error_code: 'UNAUTHORIZED' }, { status: 401 })),
      http.post(`${base}${API_PREFIX}/auth/refresh-token`, () =>
        HttpResponse.json({ error_code: 'UNAUTHORIZED' }, { status: 401 }),
      ),
    );
    const expired = vi.fn();
    setOnSessionExpired(expired);
    setAccessToken('stale');

    await expect(listShops()).rejects.toMatchObject({ error_code: 'UNAUTHORIZED' });
    expect(expired).toHaveBeenCalledTimes(1);
    setOnSessionExpired(undefined);
  });
});

describe('restoreSession (F5 / khởi động Portal)', () => {
  it('thành công → trả access token và các request sau mang token đó', async () => {
    const base = axiosInstance.defaults.baseURL;
    server.use(
      http.post(`${base}${API_PREFIX}/auth/refresh-token`, () =>
        HttpResponse.json({ accessToken: 'restored', accessTokenExpiresAt: '2030-01-01T00:00:00Z' }),
      ),
      http.get(`${base}${API_PREFIX}/shops`, ({ request }) =>
        request.headers.get('authorization') === 'Bearer restored' ? HttpResponse.json([]) : HttpResponse.json({}, { status: 401 }),
      ),
    );

    await expect(restoreSession()).resolves.toBe('restored');
    await expect(listShops()).resolves.toEqual([]);
  });

  it('401 (không có cookie / hết hạn) → undefined, không ném, không gọi onSessionExpired', async () => {
    const base = axiosInstance.defaults.baseURL;
    server.use(
      http.post(`${base}${API_PREFIX}/auth/refresh-token`, () =>
        HttpResponse.json({ error_code: 'UNAUTHORIZED' }, { status: 401 }),
      ),
    );
    const expired = vi.fn();
    setOnSessionExpired(expired);

    await expect(restoreSession()).resolves.toBeUndefined();
    expect(expired).not.toHaveBeenCalled();
    setOnSessionExpired(undefined);
  });

  it('gọi đồng thời nhiều lần trong một tab chỉ bắn MỘT request refresh', async () => {
    const base = axiosInstance.defaults.baseURL;
    let calls = 0;
    server.use(
      http.post(`${base}${API_PREFIX}/auth/refresh-token`, async () => {
        calls += 1;
        await new Promise((resolve) => setTimeout(resolve, 20));
        return HttpResponse.json({ accessToken: 'once', accessTokenExpiresAt: '2030-01-01T00:00:00Z' });
      }),
    );

    const results = await Promise.all([restoreSession(), restoreSession(), restoreSession()]);

    expect(results).toEqual(['once', 'once', 'once']);
    expect(calls).toBe(1);
  });
});

describe('refresh đang bay khi đăng xuất (I1)', () => {
  const delayedRefresh = (base: string | undefined, accessToken: string) =>
    http.post(`${base}${API_PREFIX}/auth/refresh-token`, async () => {
      await new Promise((resolve) => setTimeout(resolve, 30));
      return HttpResponse.json({ accessToken, accessTokenExpiresAt: '2030-01-01T00:00:00Z' });
    });

  it('phiên bị clear trong lúc refresh đang bay → kết quả refresh bị bỏ, access token không sống lại', async () => {
    const base = axiosInstance.defaults.baseURL;
    server.use(delayedRefresh(base, 'zombie'));
    setAccessToken('old');

    const pending = restoreSession();
    setAccessToken(undefined); // đăng xuất
    await expect(pending).resolves.toBeUndefined();

    let authorization: string | null = 'unset';
    server.use(
      http.get(`${base}${API_PREFIX}/shops`, ({ request }) => {
        authorization = request.headers.get('authorization');
        return HttpResponse.json([]);
      }),
    );
    await listShops();
    expect(authorization).toBeNull();
  });

  it('waitForPendingRefresh chờ refresh đang chạy xong; không có refresh nào thì resolve ngay', async () => {
    const base = axiosInstance.defaults.baseURL;
    await expect(waitForPendingRefresh()).resolves.toBeUndefined();

    let finished = false;
    server.use(delayedRefresh(base, 'late'));
    const pending = restoreSession().then(() => { finished = true; });
    await waitForPendingRefresh();

    expect(finished).toBe(true);
    await pending;
  });
});
