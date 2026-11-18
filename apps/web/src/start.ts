import { createCsrfMiddleware, createMiddleware, createStart } from '@tanstack/react-start';
import { getResponseHeaders, getResponseStatus } from '@tanstack/react-start/server';
import { applyEventResponse } from './server/event-response';

/**
 * B1 (WEBSITE-005, S0 §3b): router Start chỉ đặt 200/404/500 cho HTML SSR và h3 bỏ header event ở
 * response không ok — middleware này đưa status (503/502/…) và header (`Cache-Control`) do server fn
 * `loadSite` đặt vào response cuối. Chạy cho mọi request (SSR, RPC, server route).
 */
const applyEventStatus = createMiddleware().server(async ({ next }) => {
  const result = await next();
  const current = result.response as Response | undefined; // RPC server fn: chưa có Response
  const response = applyEventResponse(current, getResponseStatus(), getResponseHeaders() as unknown as Headers);
  return response === current || response === undefined ? result : response; // Response trực tiếp: dạng S0 đã kiểm
});

/**
 * Có `startInstance` thì Start KHÔNG còn tự thêm CSRF mặc định cho server fn
 * (`start-server-core` createStartHandler) — giữ lại đúng cấu hình mặc định đó.
 */
const csrfMiddleware = createCsrfMiddleware({ filter: (ctx) => ctx.handlerType === 'serverFn' });

export const startInstance = createStart(() => ({ requestMiddleware: [csrfMiddleware, applyEventStatus] }));
