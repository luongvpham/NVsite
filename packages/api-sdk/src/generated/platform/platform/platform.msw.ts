/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import {
  HttpResponse,
  delay,
  http
} from 'msw';
import type {
  RequestHandlerOptions
} from 'msw';



export const getCreateJobsDashboardSessionMockHandler = (overrideResponse?: void | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<void> | void), options?: RequestHandlerOptions) => {
  return http.post('*/api/platform/jobs/dashboard-session', async (info) => {await delay(1000);
  if (typeof overrideResponse === 'function') {await overrideResponse(info); }
    return new HttpResponse(null,
      { status: 204,
        
      })
  }, options)
}
export const getPlatformMock = () => [
  getCreateJobsDashboardSessionMockHandler()
]
