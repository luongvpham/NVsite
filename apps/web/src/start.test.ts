// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { startInstance } from './start';

type Mw = { options: { server: (ctx: unknown) => Promise<unknown> } };

describe('start config (T4)', () => {
  it('requestMiddleware giữ CSRF mặc định của Start đứng đầu chuỗi', async () => {
    const opts = await (startInstance as unknown as { getOptions: () => Promise<{ requestMiddleware?: Mw[] }> }).getOptions();
    const chain = opts.requestMiddleware ?? [];
    expect(chain.length).toBeGreaterThanOrEqual(2);
    // CSRF chạy trước: server fn cross-site bị chặn, không gọi next().
    let nextCalled = false;
    const result = await (chain[0] as Mw).options.server({
      handlerType: 'serverFn',
      request: new Request('https://spa-abc.vsite.vn/_serverFn/x', {
        method: 'POST',
        headers: { origin: 'https://evil.example', 'sec-fetch-site': 'cross-site' },
      }),
      next: () => {
        nextCalled = true;
        return Promise.resolve('next');
      },
    });
    expect(nextCalled).toBe(false);
    expect(result).toBeInstanceOf(Response);
    expect((result as Response).status).toBeGreaterThanOrEqual(400);
  });
});
