import axios, { AxiosError, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios';
import { afterEach, expect, it, vi } from 'vitest';

// REFACTOR-API-001 — hồi quy cho bug refresh của Portal: Portal đặt VITE_API_BASE_URL = "/", code cũ
// nối chuỗi `${baseURL}/auth/refresh-token` → "//auth/refresh-token" = URL protocol-relative tới HOST
// "auth" (refresh chưa bao giờ chạy đúng). Test nạp lại module với baseURL "/" và khẳng định lời gọi
// refresh nhận path tương đối + baseURL riêng, để axios tự ghép.

afterEach(() => {
  vi.unstubAllEnvs();
  vi.restoreAllMocks();
  vi.resetModules();
});

it('baseURL "/": refresh gọi `/api/auth/refresh-token` với { baseURL: "/", withCredentials }, không nối chuỗi thành "//…"', async () => {
  vi.stubEnv('VITE_API_BASE_URL', '/');
  vi.resetModules();
  const sdk = await import('../src/mutator/axios-instance');
  expect(sdk.axiosInstance.defaults.baseURL).toBe('/');

  const post = vi.spyOn(axios, 'post').mockResolvedValue({ data: { accessToken: 'fresh' } });

  // Adapter giả: lần đầu 401, lần retry (có token mới) 200 — không cần mạng.
  sdk.axiosInstance.defaults.adapter = (config: InternalAxiosRequestConfig): Promise<AxiosResponse> => {
    const ok = config.headers.get('Authorization') === 'Bearer fresh';
    const response = { data: ok ? [] : { error_code: 'UNAUTHORIZED' }, status: ok ? 200 : 401, statusText: '', headers: {}, config };
    return ok
      ? Promise.resolve(response as AxiosResponse)
      : Promise.reject(new AxiosError('401', 'ERR_BAD_REQUEST', config, undefined, response as AxiosResponse));
  };
  sdk.setAccessToken('stale');

  await expect(sdk.customInstance({ url: '/api/shops', method: 'GET' })).resolves.toEqual([]);

  expect(post).toHaveBeenCalledTimes(1);
  const [url, body, config] = post.mock.calls[0] ?? [];
  expect(url).toBe('/api/auth/refresh-token');
  expect(url?.startsWith('//')).toBe(false);
  // #101 — không body (refresh token ở cookie httpOnly), kèm cookie qua withCredentials.
  expect(body).toBeUndefined();
  expect(config).toEqual({ baseURL: '/', withCredentials: true });
});
