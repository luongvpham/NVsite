import { fireEvent, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';
import { useSessionStore } from '../stores/session-store';
import { server } from '../test/msw-server';
import { nodeEl, openFixture, validHome } from '../test/editor-helpers';

afterEach(() => {
  useSessionStore.getState().clearSession();
});

function mockSiteApis() {
  server.use(
    http.get('*/api/shops/:shopId/website', () =>
      HttpResponse.json({
        id: 'w1', status: 'Draft', defaultLocale: 'vi', locales: ['vi'], defaultSeoTitle: null, defaultSeoDescription: null,
        defaultOgImageId: null, faviconAssetId: null, trackingSnippets: {}, homePageId: 'p-1',
      }),
    ),
    http.get('*/api/shops/:shopId/website/pages', () =>
      HttpResponse.json([
        {
          id: 'p-1', kind: 'Composable', systemType: null, slug: '', title: 'Trang chủ', seoTitle: null, seoDescription: null,
          ogImageId: null, noIndex: false, isEnabled: true, sortOrder: 0, isHomePage: true, isDirty: false, deletable: false,
          updatedAt: '2026-11-11T00:00:00Z',
        },
      ]),
    ),
    http.get('*/api/shops/:shopId', () =>
      HttpResponse.json({
        id: 'shop-1', name: 'Spa ABC', slug: 'spa-abc', kind: 'Hosted', externalUrl: null, status: 'Active', logoId: null, logoUrl: null,
      }),
    ),
  );
}

describe('Editor — nút Xem trước (WEBSITE-003 T4)', () => {
  it('mở lớp phủ xem trước, đóng lại thì về editor còn nguyên khối đang chọn', async () => {
    openFixture(validHome);
    mockSiteApis();
    await screen.findByTestId('editor-canvas');
    fireEvent.click(nodeEl('c_hero1'));

    fireEvent.click(screen.getByRole('button', { name: 'Xem trước' }));
    const dialog = await screen.findByRole('dialog', { name: 'Xem trước trang' });
    await waitFor(() => {
      expect(dialog.querySelector('header')).toHaveTextContent('Spa ABC');
    });

    fireEvent.click(screen.getByRole('button', { name: 'Đóng xem trước' }));
    expect(screen.queryByRole('dialog', { name: 'Xem trước trang' })).not.toBeInTheDocument();
    expect(nodeEl('c_hero1')).toHaveAttribute('data-selected', 'true');
  });

  it('phím Delete khi đang xem trước không xoá khối phía sau', async () => {
    openFixture(validHome);
    mockSiteApis();
    await screen.findByTestId('editor-canvas');
    fireEvent.click(nodeEl('c_hero1'));
    fireEvent.click(screen.getByRole('button', { name: 'Xem trước' }));
    await screen.findByRole('dialog', { name: 'Xem trước trang' });

    fireEvent.keyDown(document.body, { key: 'Delete' });

    fireEvent.click(screen.getByRole('button', { name: 'Đóng xem trước' }));
    expect(nodeEl('c_hero1')).toBeInTheDocument();
  });
});
