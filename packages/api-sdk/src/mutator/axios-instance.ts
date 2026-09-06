import axios, { isAxiosError, type AxiosRequestConfig } from 'axios';

/**
 * Instance dùng chung cho mọi request sinh bởi Orval.
 * Access token giữ trong memory (Quyết định #3) — set qua setAccessToken(), không đọc localStorage.
 */
// Bước 1 chưa có Caddy/api.vsite.vn — mặc định trỏ về Api host local (backend/src/Api/Properties/launchSettings.json).
const baseURL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5270';

export const axiosInstance = axios.create({ baseURL });

let accessToken: string | undefined;

export function setAccessToken(token: string | undefined): void {
  accessToken = token;
}

axiosInstance.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.set('Authorization', `Bearer ${accessToken}`);
  }
  return config;
});

// Chỗ cắm cho refresh-token rotation (Quyết định #3) — chưa có Identity module thật ở Bước 1,
// nên chưa nối logic gọi /auth/refresh. Interceptor response sẽ thêm vào đây khi module Identity xong.
axiosInstance.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
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
