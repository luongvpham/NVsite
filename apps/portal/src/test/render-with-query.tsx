import type { ReactElement } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';

/**
 * Dựng QueryClient thật cho test component đứng riêng (dialog/picker) không cần router —
 * khác `renderApp` (dùng cho flow điều hướng qua nhiều route).
 *
 * `mutationRetry`: mặc định `false`. Test "không tự thử lại" truyền số > 0 để hook thiếu `retry: false` bị lộ.
 */
export function renderWithQuery(ui: ReactElement, options: { mutationRetry?: number | false } = {}) {
  const mutationRetry = options.mutationRetry ?? false;
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: mutationRetry, retryDelay: 0 },
    },
  });
  return { ...render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>), queryClient };
}
