import { create } from 'zustand';
import { restoreSession, setAccessToken, setOnSessionExpired } from '@vsite/api-sdk';

interface SessionTokens {
  accessToken: string;
}

interface SessionState {
  isAuthenticated: boolean;
  setSession: (tokens: SessionTokens) => void;
  clearSession: () => void;
}

/**
 * Client state — chỉ giữ cờ `isAuthenticated` để UI (route guard, layout) phản ứng (Quyết định #20).
 * Giá trị token thật nằm DUY NHẤT trong memory của axios mutator (@vsite/api-sdk), không copy vào
 * đây — access token in-memory only, không localStorage (Quyết định #3). Refresh token KHÔNG nằm ở
 * FE: cookie httpOnly do BE set (Quyết định #101), JS không đọc được.
 */
export const useSessionStore = create<SessionState>((set) => ({
  isAuthenticated: false,
  setSession: ({ accessToken }) => {
    setAccessToken(accessToken);
    set({ isAuthenticated: true });
  },
  clearSession: () => {
    setAccessToken(undefined);
    set({ isAuthenticated: false });
  },
}));

// Đăng ký một lần lúc module load: khi refresh-token rotation thất bại (401 từ /auth/refresh-token
// = phiên hết hạn thật, Quyết định #3), axios mutator gọi callback này — store tự clear, route
// guard (_authenticated.tsx) sẽ redirect về /login ở lần điều hướng kế tiếp.
setOnSessionExpired(() => {
  useSessionStore.getState().clearSession();
});

/**
 * Route guard dùng: đã đăng nhập trong phiên này thì true ngay; store rỗng (F5, mở tab mới) thì thử
 * khôi phục bằng cookie httpOnly (#101) — thành công thì bật cờ, không thì false (guard redirect /login).
 */
export async function ensureSession(): Promise<boolean> {
  if (useSessionStore.getState().isAuthenticated) return true;
  const accessToken = await restoreSession();
  if (!accessToken) return false;
  useSessionStore.setState({ isAuthenticated: true });
  return true;
}
