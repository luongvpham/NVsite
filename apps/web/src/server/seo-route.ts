import { getRequestHost, getRequestUrl } from '@tanstack/react-start/server';
import { readRootDomain } from './env';
import { fetchPublishedSite } from './site-client';
import { handleSeoFile, type SeoFileKind } from './site-seo-files';

/**
 * Vỏ chung của hai server route robots/sitemap (T9). Host CHỈ từ request gốc; protocol từ URL request
 * (không tin X-Forwarded-*, A3, #108).
 */
export async function serveSeoFile(kind: SeoFileKind): Promise<Response> {
  const host = getRequestHost({ xForwardedHost: false });
  const protocol = getRequestUrl({ xForwardedHost: false, xForwardedProto: false }).protocol;
  let rootDomain: string;
  try {
    rootDomain = readRootDomain();
  } catch (error) {
    console.error('[seo-files] thiếu cấu hình server', error);
    return new Response('Internal server error\n', {
      status: 500,
      headers: { 'Content-Type': 'text/plain; charset=utf-8' },
    });
  }
  const r = await handleSeoFile(kind, host, protocol, {
    rootDomain,
    fetchSite: fetchPublishedSite,
    logError: (message, error) => {
      console.error(message, error);
    },
  });
  return new Response(r.body, { status: r.status, headers: r.headers });
}
