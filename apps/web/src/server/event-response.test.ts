// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { applyEventResponse } from './event-response';

/**
 * B1 (WEBSITE-005): status/header do server fn đặt (`setResponseStatus`/`setResponseHeader`) phải tới
 * HTML SSR. Router chỉ sinh 200/404/500 và h3 bỏ header event khi response không ok (S0 §3b).
 */
const html = (status: number, headers: Record<string, string> = {}) =>
  new Response('<html></html>', { status, headers: { 'content-type': 'text/html', ...headers } });

const ev = (h: Record<string, string> = {}) => new Headers(h);

describe('applyEventResponse', () => {
  it('router 200 + event 200 → trả nguyên response (cùng object)', () => {
    const res = html(200);
    expect(applyEventResponse(res, 200, ev({ 'cache-control': 'private, no-cache' }))).toBe(res);
  });

  it.each([404, 503, 502, 500])('router 200 + event %i → status event, ghép header event', async (status) => {
    const out = applyEventResponse(html(200), status, ev({ 'cache-control': 'private, no-cache' }));
    expect(out.status).toBe(status);
    expect(out.headers.get('cache-control')).toBe('private, no-cache');
    expect(out.headers.get('content-type')).toBe('text/html');
    expect(await out.text()).toBe('<html></html>');
  });

  it('router 404/500 không bị che bởi status event, nhưng vẫn nhận header event', () => {
    const out = applyEventResponse(html(404), 503, ev({ 'cache-control': 'private, no-cache' }));
    expect(out.status).toBe(404);
    expect(out.headers.get('cache-control')).toBe('private, no-cache');
    expect(applyEventResponse(html(500), 200, ev()).status).toBe(500);
  });

  it('không có Response (RPC server fn: Start tự dựng response sau middleware) → trả nguyên giá trị', () => {
    expect(applyEventResponse(undefined, 503, ev({ 'cache-control': 'x' }))).toBeUndefined();
  });

  it('không đụng set-cookie (h3 đã tự ghép ở response không ok)', () => {
    const res = new Response('x', { status: 200, headers: { 'set-cookie': 'a=1' } });
    const out = applyEventResponse(res, 503, ev({ 'set-cookie': 'b=2' }));
    expect(out.headers.getSetCookie()).toEqual(['a=1']);
  });

  it('header event không ghi đè content-type/content-length của router', () => {
    const res = new Response('<html></html>', { status: 200, headers: { 'content-type': 'text/html', 'content-length': '13' } });
    const out = applyEventResponse(res, 503, ev({ 'content-type': 'application/json', 'content-length': '999', 'cache-control': 'x' }));
    expect(out.headers.get('content-type')).toBe('text/html');
    expect(out.headers.get('content-length')).toBe('13');
    expect(out.headers.get('cache-control')).toBe('x');
  });
});
