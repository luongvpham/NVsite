import axios, {
  isAxiosError,
  type AxiosInstance,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios';

/** Prefix chung của mọi endpoint API (Quyết định #91). `/media/*` KHÔNG nằm dưới prefix này. */
export const API_PREFIX = '/api';

/**
 * Instance mặc định — dùng cho app CSR (Portal): một trình duyệt = một phiên, nên giữ token ở cấp
 * module là đúng. Access token giữ trong memory (Quyết định #3) — set qua setAccessToken(), không
 * đọc localStorage.
 *
 * ⚠️ KHÔNG dùng instance này khi render ở server (apps/web SSR): state cấp module dùng chung cho
 * MỌI request của process → token của người này lọt sang request của người khác. Ở server, tạo
 * client riêng cho từng request bằng {@link createApiClient} và truyền vào hàm sinh ra:
 * `listShops({ client })`.
 */
// Bước 1 chưa có Caddy — mặc định trỏ về Api host local (backend/src/Vsite.Api/Properties/launchSettings.json).
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
 * đăng nhập đã hết hạn thật (Quyết định #3: "401 từ refresh-token = phiên hết hạn thật — clear
 * state, về login, không retry"). App (Zustand session store) đăng ký callback này để tự clear
 * state + điều hướng về trang login; package này không biết về router/store của app.
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
const REFRESH_EXEMPT_URLS = [`${API_PREFIX}/auth/login`, `${API_PREFIX}/auth/refresh-token`, `${API_PREFIX}/auth/register`];

let refreshPromise: Promise<string | undefined> | null = null;

async function refreshAccessToken(): Promise<string | undefined> {
  if (!refreshToken) return undefined;

  try {
    // Axios bare (không qua axiosInstance → không dính interceptor 401 ở dưới). Truyền `baseURL`
    // để axios tự ghép đúng dấu `/` — KHÔNG nối chuỗi `${baseURL}/…`: với baseURL "/" (Portal) nó ra
    // "//api/…", trình duyệt hiểu thành URL protocol-relative tới host "api" (REFACTOR-API-001).
    const response = await axios.post<{
      accessToken: string;
      refreshToken: string;
    }>(`${API_PREFIX}/auth/refresh-token`, { refreshToken }, { baseURL });
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

    return rejectWithProblemDetails(error);
  },
);

/**
 * Reject với body ProblemDetails (có error_code) thay vì AxiosError thô, để getErrorCode() ở FE
 * đọc được thẳng (Quyết định #19). Dùng chung cho instance mặc định và client tạo bằng factory.
 */
function rejectWithProblemDetails(error: unknown): Promise<never> {
  if (isAxiosError(error) && error.response?.data) {
    return Promise.reject(error.response.data);
  }
  return Promise.reject(error);
}

export interface ApiClientOptions {
  /** URL tuyệt đối của BE — ở server không có "relative URL" (vd. `http://localhost:5270`). */
  baseURL: string;
  /**
   * Header gửi kèm MỌI request của client này. Ở SSR phải forward **`Host`** gốc của request trình
   * duyệt (vd. `{ Host: 'spa-abc.vsite.vn' }`) — BE resolve tenant theo `Request.Host` (Quyết định #7,
   * #27), mất Host thì audience sai. **Không** dùng `X-Forwarded-Host`: BE chưa bật
   * `UseForwardedHeaders` nên header đó bị bỏ qua (fail-closed về `vsite-main`).
   */
  headers?: Record<string, string>;
  /** Token của ĐÚNG request đang render; gọi lại mỗi lần gửi để lấy giá trị mới nhất. */
  getAccessToken?: () => string | undefined;
}

/**
 * Client riêng cho một request (SSR). Không chia sẻ state với instance mặc định hay client khác,
 * không tự refresh token (server không giữ refresh token của người dùng). Truyền vào hàm sinh ra
 * qua tham số cuối: `getShop(shopId, { client })`.
 */
export function createApiClient(options: ApiClientOptions): AxiosInstance {
  const client = axios.create({ baseURL: options.baseURL, headers: options.headers });

  client.interceptors.request.use((config) => {
    const token = options.getAccessToken?.();
    if (token) {
      config.headers.set('Authorization', `Bearer ${token}`);
    }
    return config;
  });
  client.interceptors.response.use((response) => response, rejectWithProblemDetails);

  return client;
}

/** Tham số thứ hai của mọi hàm Orval sinh ra (`SecondParameter<typeof customInstance>`). */
export interface RequestOptions {
  /** Bỏ trống = instance mặc định (Portal). Ở SSR luôn truyền client tạo bằng {@link createApiClient}. */
  client?: AxiosInstance;
}

export const customInstance = <T>(config: AxiosRequestConfig, options?: RequestOptions): Promise<T> =>
  (options?.client ?? axiosInstance).request<T>(config).then((response) => response.data);
