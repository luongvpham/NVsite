import { create } from 'zustand';
import { setAccessToken, setOnSessionExpired, setRefreshToken } from '@vsite/api-sdk';

interface SessionTokens {
  accessToken: string;
  refreshToken: string;
}

interface SessionState {
  isAuthenticated: boolean;
  setSession: (tokens: SessionTokens) => void;
  clearSession: () => void;
}

/**
 * Client state — chỉ giữ cờ `isAuthenticated` để UI (route guard, layout) phản ứng (Quyết định #20).
 * Giá trị token thật nằm DUY NHẤT trong memory của axios mutator (@vsite/api-sdk), không copy vào
 * đây — access token in-memory only, không localStorage (Quyết định #3).
 */
export const useSessionStore = create<SessionState>((set) => ({
  isAuthenticated: false,
  setSession: ({ accessToken, refreshToken }) => {
    setAccessToken(accessToken);
    setRefreshToken(refreshToken);
    set({ isAuthenticated: true });
  },
  clearSession: () => {
    setAccessToken(undefined);
    setRefreshToken(undefined);
    set({ isAuthenticated: false });
  },
}));

// Đăng ký một lần lúc module load: khi refresh-token rotation thất bại (401 từ /auth/refresh-token
// = phiên hết hạn thật, Quyết định #3), axios mutator gọi callback này — store tự clear, route
// guard (_authenticated.tsx) sẽ redirect về /login ở lần điều hướng kế tiếp.
setOnSessionExpired(() => {
  useSessionStore.getState().clearSession();
});
