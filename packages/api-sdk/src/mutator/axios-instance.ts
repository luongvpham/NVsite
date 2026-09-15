import axios, { isAxiosError, type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios';

/**
 * Instance dùng chung cho mọi request sinh bởi Orval.
 * Access token giữ trong memory (Quyết định #3) — set qua setAccessToken(), không đọc localStorage.
 */
// Bước 1 chưa có Caddy/api.vsite.vn — mặc định trỏ về Api host local (backend/src/Api/Properties/launchSettings.json).
const baseURL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5270';

export const axiosInstance = axios.create({ baseURL });

let accessToken: string | undefined;
let refreshToken: string | undefined;

export function setAccessToken(token: string | undefined): void {
  accessToken = token;
}

export function setRefreshToken(token: string | undefined): void {
  refreshToken = token;
}

/**
 * Gọi khi refresh-token rotation thất bại (refresh token hết hạn/không hợp lệ) — nghĩa là phiên
 * đăng nhập đã hết hạn thật (Quyết định #3: "401 từ /auth/refresh-token = phiên hết hạn thật —
 * clear state, về login, không retry"). App (Zustand session store) đăng ký callback này để tự
 * clear state + điều hướng về trang login; package này không biết về router/store của app.
 */
let onSessionExpired: (() => void) | undefined;

export function setOnSessionExpired(callback: (() => void) | undefined): void {
  onSessionExpired = callback;
}

interface RetryableConfig extends InternalAxiosRequestConfig {
  _retriedAfterRefresh?: boolean;
}

axiosInstance.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.set('Authorization', `Bearer ${accessToken}`);
  }
  return config;
});

// Endpoint tự thân không được kích hoạt vòng lặp refresh (401 ở đây nghĩa là credentials/refresh
// token sai, không phải access token hết hạn).
const REFRESH_EXEMPT_URLS = ['/auth/login', '/auth/refresh-token', '/auth/register'];

let refreshPromise: Promise<string | undefined> | null = null;

async function refreshAccessToken(): Promise<string | undefined> {
  if (!refreshToken) return undefined;

  try {
    const response = await axios.post<{
      accessToken: string;
      refreshToken: string;
    }>(`${baseURL}/auth/refresh-token`, { refreshToken });
    accessToken = response.data.accessToken;
    refreshToken = response.data.refreshToken;
    return accessToken;
  } catch {
    accessToken = undefined;
    refreshToken = undefined;
    return undefined;
  }
}

axiosInstance.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
    if (isAxiosError(error) && error.response?.status === 401 && error.config) {
      const config = error.config as RetryableConfig;
      const url = config.url ?? '';
      const isExempt = REFRESH_EXEMPT_URLS.some((exempt) => url.startsWith(exempt));

      if (!isExempt && !config._retriedAfterRefresh) {
        config._retriedAfterRefresh = true;

        refreshPromise ??= refreshAccessToken().finally(() => {
          refreshPromise = null;
        });
        const newAccessToken = await refreshPromise;

        if (newAccessToken) {
          config.headers.set('Authorization', `Bearer ${newAccessToken}`);
          return axiosInstance(config);
        }

        onSessionExpired?.();
      }
    }

    // Reject với body ProblemDetails (có error_code) thay vì AxiosError thô,
    // để getErrorCode() ở FE đọc được thẳng (Quyết định #19).
    if (isAxiosError(error) && error.response?.data) {
      return Promise.reject(error.response.data);
    }
    return Promise.reject(error);
  },
);

export const customInstance = <T>(config: AxiosRequestConfig): Promise<T> =>
  axiosInstance.request<T>(config).then((response) => response.data);
