/**
 * B1 (WEBSITE-005): đưa status/header do server fn đặt (`setResponseStatus`/`setResponseHeader`)
 * vào response cuối. Hàm thuần — dùng bởi request middleware ở `src/start.ts`.
 *
 * - Router của Start chỉ sinh 200/404/500 cho HTML SSR; status event chỉ ghi đè khi router trả 200
 *   (không che 404/500 của router).
 * - Request RPC server fn: middleware không thấy Response → bỏ qua (đã kiểm ở build).
 * - h3 chỉ ghép header event vào response ok; response không ok thì ghép lại ở đây. Bỏ qua
 *   `set-cookie` (h3 đã tự ghép, `set` sẽ gộp mất nhiều cookie) và `content-type`/`content-length` (thuộc về body của router).
 */
const SKIP_EVENT_HEADERS: ReadonlySet<string> = new Set(['set-cookie', 'content-type', 'content-length']);

export function applyEventResponse(response: Response, eventStatus: number, eventHeaders: Headers): Response;
export function applyEventResponse(
  response: Response | undefined,
  eventStatus: number,
  eventHeaders: Headers,
): Response | undefined;
export function applyEventResponse(
  response: Response | undefined,
  eventStatus: number,
  eventHeaders: Headers,
): Response | undefined {
  // RPC `/_serverFn/*`: `next()` của request middleware chưa có Response (Start dựng sau, đã kèm
  // status/header event) — không đụng.
  if (!(response instanceof Response)) return response;
  const status = response.status === 200 && eventStatus !== 200 ? eventStatus : response.status;
  if (status === 200) return response;
  const headers = new Headers(response.headers);
  eventHeaders.forEach((value, key) => {
    if (!SKIP_EVENT_HEADERS.has(key.toLowerCase())) headers.set(key, value);
  });
  return new Response(response.body, { status, statusText: '', headers });
}
