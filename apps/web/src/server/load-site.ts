import { createServerFn } from '@tanstack/react-start';
import { getRequestHost, setResponseHeader, setResponseStatus } from '@tanstack/react-start/server';
import { readServerEnv } from './env';
import { loadSiteForHost, SiteLoaderKind, type SiteLoaderData } from './load-site-core';
import { parseLoadSiteInput } from './load-site-input';
import { fetchPublishedSite } from './site-client';

/**
 * Vỏ mỏng của loader Shop Site (WEBSITE-005 T4). Endpoint RPC gọi được từ ngoài nên:
 * - input CHỈ `{ path }` (R13) — host/shopId trong input bị validator bỏ;
 * - host CHỈ đọc từ request gốc, không tin header host do proxy chuyển tiếp (R1, #108);
 * - status/header đặt qua event; `src/start.ts` đưa chúng vào HTML SSR (B1).
 * Handler + import server-only bị bộ biên dịch server fn bỏ khỏi bundle client.
 */
export const loadSite = createServerFn({ method: 'GET' })
  .validator(parseLoadSiteInput)
  .handler(async ({ data }): Promise<SiteLoaderData> => {
    const host = getRequestHost({ xForwardedHost: false });
    let rootDomain: string;
    try {
      rootDomain = readServerEnv().rootDomain;
    } catch (error) {
      console.error('[loadSite] thiếu cấu hình server', error);
      setResponseStatus(500);
      return { kind: SiteLoaderKind.Error };
    }
    const outcome = await loadSiteForHost(host, data.path, {
      rootDomain,
      fetchSite: fetchPublishedSite,
      now: () => new Date(),
      logError: (message, error) => {
        console.error(message, error);
      },
    });
    if (outcome.status !== 200) setResponseStatus(outcome.status);
    for (const [name, value] of Object.entries(outcome.headers)) setResponseHeader(name, value);
    return outcome.data;
  });
