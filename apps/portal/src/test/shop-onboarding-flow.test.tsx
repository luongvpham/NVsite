import { fireEvent, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';
import type { ShopDto } from '@vsite/api-sdk';
import { useSessionStore } from '../stores/session-store';
import { renderApp } from './render-app';
import { server } from './msw-server';

afterEach(() => {
  useSessionStore.getState().clearSession();
});

describe('SHOP-001 onboarding flow', () => {
  it('đăng ký → đăng nhập → tạo shop → thấy trong danh sách → sửa', async () => {
    const shops: ShopDto[] = [];

    // Handler có state thật (không phải faker random) để mô phỏng shop vừa tạo xuất hiện trong
    // danh sách và load lại đúng khi sửa — ghi đè handler mặc định (faker) từ msw-server.ts.
    server.use(
      http.post('/auth/register', () => HttpResponse.json({ verificationEmailSent: true })),
      http.post('/auth/login', () =>
        HttpResponse.json({
          accessToken: 'test-access-token',
          accessTokenExpiresAt: new Date(Date.now() + 3_600_000).toISOString(),
          refreshToken: 'test-refresh-token',
          refreshTokenExpiresAt: new Date(Date.now() + 86_400_000).toISOString(),
        }),
      ),
      http.post('/shops', async ({ request }) => {
        const body = (await request.json()) as { name: string; slug: string; kind: ShopDto['kind']; externalUrl: string | null };
        const created: ShopDto = { id: 'shop-1', status: 'Draft', ...body };
        shops.push(created);
        return HttpResponse.json(created);
      }),
      http.get('/shops', () =>
        HttpResponse.json(
          shops.map((shop) => ({
            id: shop.id,
            name: shop.name,
            slug: shop.slug,
            kind: shop.kind,
            status: shop.status,
            roleCode: 'Owner',
          })),
        ),
      ),
      http.get('/shops/:shopId', ({ params }) => {
        const shop = shops.find((s) => s.id === params.shopId);
        return shop ? HttpResponse.json(shop) : new HttpResponse(null, { status: 404 });
      }),
      http.patch('/shops/:shopId', async ({ params, request }) => {
        const shop = shops.find((s) => s.id === params.shopId);
        if (!shop) return new HttpResponse(null, { status: 404 });
        Object.assign(shop, await request.json());
        return HttpResponse.json(shop);
      }),
    );

    // 1. Đăng ký
    renderApp('/register');
    await screen.findByRole('heading', { name: 'Đăng ký' });

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'owner@example.com' } });
    fireEvent.change(screen.getByLabelText('Họ tên'), { target: { value: 'Nguyen Van A' } });
    fireEvent.change(screen.getByLabelText('Mật khẩu'), { target: { value: 'Password123!' } });
    fireEvent.click(screen.getByRole('button', { name: 'Đăng ký' }));

    await screen.findByText('Kiểm tra email để xác minh');

    // Xác minh email KHÔNG có trang FE (brief: "KHÔNG tự chế trang FE xử lý token verify-email
    // cho task này") — dev tự lấy token từ console log và gọi thẳng BE. Test bỏ qua bước này,
    // xem như đã verify xong, đi thẳng sang đăng nhập.
    fireEvent.click(screen.getByRole('link', { name: 'đăng nhập' }));

    // 2. Đăng nhập
    await screen.findByRole('heading', { name: 'Đăng nhập' });
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'owner@example.com' } });
    fireEvent.change(screen.getByLabelText('Mật khẩu'), { target: { value: 'Password123!' } });
    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập' }));

    // 3. Danh sách shop — rỗng
    await screen.findByRole('heading', { name: 'Shop của tôi' });
    await screen.findByText('Bạn chưa có shop nào. Tạo shop đầu tiên để bắt đầu.');

    // 4. Tạo shop
    fireEvent.click(screen.getByRole('link', { name: 'Tạo shop' }));
    await screen.findByRole('heading', { name: 'Tạo shop' });

    fireEvent.change(screen.getByLabelText('Tên shop'), { target: { value: 'Spa Demo' } });
    fireEvent.change(screen.getByLabelText('Slug'), { target: { value: 'demo-spa' } });
    fireEvent.click(screen.getByRole('button', { name: 'Tạo shop' }));

    // 5. Sau khi tạo, điều hướng sang màn sửa — form load đúng dữ liệu vừa tạo
    await screen.findByRole('heading', { name: 'Sửa shop' });
    await waitFor(() => {
      expect(screen.getByLabelText('Tên shop')).toHaveValue('Spa Demo');
    });
    expect(screen.getByLabelText('Slug')).toHaveValue('demo-spa');

    // 6. Sửa tên, lưu — full-replace, gửi đủ 5 field
    fireEvent.change(screen.getByLabelText('Tên shop'), { target: { value: 'Spa Demo (đã sửa)' } });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    // 7. Quay lại danh sách, thấy tên đã cập nhật
    await screen.findByRole('heading', { name: 'Shop của tôi' });
    await screen.findByText('Spa Demo (đã sửa)');
    expect(shops[0]?.name).toBe('Spa Demo (đã sửa)');
  });
});
