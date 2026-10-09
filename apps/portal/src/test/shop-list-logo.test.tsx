import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';
import type { ShopSummaryDto } from '@vsite/api-sdk';
import { useSessionStore } from '../stores/session-store';
import { renderApp } from './render-app';
import { server } from './msw-server';

afterEach(() => {
  useSessionStore.getState().clearSession();
});

const withLogo: ShopSummaryDto = {
  id: 'shop-1',
  name: 'Spa ABC',
  slug: 'spa-abc',
  kind: 'Hosted',
  status: 'Active',
  roleCode: 'Owner',
  logoUrl: '/media/shop-1/logo-320x96.jpg',
};
const withoutLogo: ShopSummaryDto = { ...withLogo, id: 'shop-2', name: 'Salon Xinh', slug: 'salon-xinh', logoUrl: null };

describe('Danh sách shop — logo (F6)', () => {
  it('hiện logo nguyên trạng khi có logoUrl, chữ cái đầu khi null', async () => {
    useSessionStore.getState().setSession({ accessToken: 't', refreshToken: 'r' });
    server.use(http.get('/api/shops', () => HttpResponse.json([withLogo, withoutLogo])));
    renderApp('/shops');

    const img = await screen.findByAltText('Spa ABC');
    expect(img).toHaveAttribute('src', '/media/shop-1/logo-320x96.jpg');
    expect(img.getAttribute('src')).not.toContain('/media//media/');

    expect(screen.queryByAltText('Salon Xinh')).not.toBeInTheDocument();
    const fallbacks = screen.getAllByTestId('shop-logo-fallback');
    expect(fallbacks).toHaveLength(1);
    expect(fallbacks[0]).toHaveTextContent('S');
  });

  it('loading và error được giữ nguyên', async () => {
    useSessionStore.getState().setSession({ accessToken: 't', refreshToken: 'r' });
    server.use(
      http.get('/api/shops', () => HttpResponse.json({ error_code: 'SHOP_ACCESS_DENIED', title: 'x' }, { status: 403 })),
    );
    renderApp('/shops');
    expect(await screen.findByRole('alert')).toHaveTextContent('Bạn không có quyền truy cập shop này.');
  });
});
