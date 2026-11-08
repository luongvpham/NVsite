import { http, HttpResponse } from 'msw';
import type { ShopSummaryDto } from '@vsite/api-sdk';
import { useSessionStore } from '../stores/session-store';
import { renderApp } from './render-app';
import { server } from './msw-server';
// Fixture dùng chung với BE — nạp trực tiếp, không chép (plan §6).
import validHomeJson from '../../../../packages/builder-components/tests/fixtures/tree/valid-home.json';
import validEmptyHomeJson from '../../../../packages/builder-components/tests/fixtures/tree/valid-empty-home.json';
import validSystemContactJson from '../../../../packages/builder-components/tests/fixtures/tree/valid-system-contact.json';

export interface Fixture {
  pageKind: string;
  systemType?: string;
  tree: unknown;
}

export const validHome: Fixture = validHomeJson;
export const validEmptyHome: Fixture = validEmptyHomeJson;
export const validSystemContact: Fixture = validSystemContactJson;

export const EDITOR_PATH = '/shops/shop-1/website/pages/p-1';

export function shop(roleCode: string): ShopSummaryDto {
  return { id: 'shop-1', name: 'Spa ABC', slug: 'spa-abc', kind: 'Hosted', status: 'Active', roleCode, logoUrl: null };
}

export function draftBody(fixture: Fixture) {
  return {
    tree: fixture.tree,
    version: 1,
    isDirty: false,
    updatedAt: '2026-11-08T00:00:00Z',
    page: { id: 'p-1', kind: fixture.pageKind, systemType: fixture.systemType ?? null },
  };
}

export function openEditor(draft: () => Response | Promise<Response>, role = 'Owner') {
  useSessionStore.getState().setSession({ accessToken: 't' });
  server.use(
    http.get('*/api/shops', () => HttpResponse.json([shop(role)])),
    http.get('*/api/shops/:shopId/website/pages/:pageId/draft', draft),
    // Ảnh trong fixture: không có asset thật → rỗng (canvas rơi về placeholder).
    http.get('*/api/shops/:shopId/media/assets', () => HttpResponse.json([])),
  );
  return renderApp(EDITOR_PATH);
}

export const openFixture = (fixture: Fixture, role?: string) =>
  openEditor(() => HttpResponse.json(draftBody(fixture)), role);

export function nodeEl(id: string): HTMLElement {
  const el = document.querySelector<HTMLElement>(`[data-node-id="${id}"]`);
  if (!el) throw new Error(`Không thấy node ${id} trên canvas`);
  return el;
}

/** Id các node là con trực tiếp của gốc Page (không nằm trong node nào khác) theo thứ tự DOM. */
export function topLevelIds(): string[] {
  return Array.from(document.querySelectorAll('[data-testid="editor-canvas"] [data-node-id]'))
    .filter((el) => el.parentElement?.closest('[data-node-id]') == null)
    .map((el) => el.getAttribute('data-node-id') ?? '');
}

/** Id mọi node đang trên canvas theo thứ tự DOM — dùng kiểm thứ tự sau add/move. */
export function canvasNodeIds(): string[] {
  return Array.from(document.querySelectorAll('[data-testid="editor-canvas"] [data-node-id]')).map(
    (el) => el.getAttribute('data-node-id') ?? '',
  );
}
