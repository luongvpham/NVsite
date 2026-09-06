import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../test/msw-server';
import { SamplesPage } from './samples-page';

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <SamplesPage />
    </QueryClientProvider>,
  );
}

describe('SamplesPage', () => {
  it('shows loading then the list (default MSW mock)', async () => {
    renderPage();
    expect(screen.getByText('Đang tải…')).toBeInTheDocument();
    expect(await screen.findByRole('list', {}, { timeout: 3000 })).toBeInTheDocument();
  });

  it('shows empty state when the list has no items', async () => {
    server.use(
      http.get('*/samples', () =>
        HttpResponse.json({ items: [], total: 0, page: 1, pageSize: 20 }),
      ),
    );
    renderPage();
    expect(await screen.findByText('Chưa có sample nào.')).toBeInTheDocument();
  });

  it('shows error state when the list request fails', async () => {
    server.use(
      http.get('*/samples', () =>
        HttpResponse.json({ error_code: 'boom' }, { status: 500 }),
      ),
    );
    renderPage();
    expect(await screen.findByText(/Lỗi: boom/)).toBeInTheDocument();
  });
});
