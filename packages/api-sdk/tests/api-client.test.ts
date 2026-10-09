import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest';
import { listShops } from '../src/generated/shop/shop/shop';
import {
  API_PREFIX,
  axiosInstance,
  createApiClient,
  setAccessToken,
  setRefreshToken,
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

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => {
  server.resetHandlers();
  seen.length = 0;
  setAccessToken(undefined);
  setRefreshToken(undefined);
});
afterAll(() => server.close());

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
  it('401 → gọi /api/auth/refresh-token ở đúng baseURL rồi retry với token mới', async () => {
    const base = axiosInstance.defaults.baseURL!;
    let refreshBody: unknown;
    server.use(
      http.get(`${base}${API_PREFIX}/shops`, ({ request }) =>
        request.headers.get('authorization') === 'Bearer fresh'
          ? HttpResponse.json([])
          : HttpResponse.json({ error_code: 'UNAUTHORIZED' }, { status: 401 }),
      ),
      http.post(`${base}${API_PREFIX}/auth/refresh-token`, async ({ request }) => {
        refreshBody = await request.json();
        return HttpResponse.json({ accessToken: 'fresh', refreshToken: 'r2' });
      }),
    );
    setAccessToken('stale');
    setRefreshToken('r1');

    await expect(listShops()).resolves.toEqual([]);
    expect(refreshBody).toEqual({ refreshToken: 'r1' });
  });
});
