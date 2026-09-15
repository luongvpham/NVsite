import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRouter } from '@tanstack/react-router';
import { render } from '@testing-library/react';
import { routeTree } from '../routeTree.gen';

/**
 * Dựng router + QueryClient thật cho test tích hợp (register → login → tạo/sửa shop), thay vì
 * render từng component route rời rạc — route component dùng `Route.useSearch()`/`useParams()`
 * cần context router thật, không mock được dễ dàng.
 */
export function renderApp(initialPath: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const history = createMemoryHistory({ initialEntries: [initialPath] });
  const router = createRouter({ routeTree, context: { queryClient }, history });

  const utils = render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );

  return { ...utils, router, queryClient };
}
