/**
 * Validator input của server fn `loadSite` (R13). Isomorphic, không import gì — file này có thể nằm
 * trong bundle client. Server fn là endpoint RPC gọi được từ ngoài: input CHỈ có `path`; mọi khoá
 * khác (`host`, `shopId`, …) bị bỏ. Host luôn đọc ở server từ request gốc.
 */
export interface LoadSiteInput {
  readonly path: string;
}

export function parseLoadSiteInput(input: unknown): LoadSiteInput {
  const path = typeof input === 'object' && input !== null ? (input as Record<string, unknown>).path : undefined;
  if (typeof path !== 'string' || !path.startsWith('/')) {
    throw new TypeError('loadSite: input phải là { path: string } bắt đầu bằng "/".');
  }
  return { path };
}
