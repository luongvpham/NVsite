/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import {
  faker
} from '@faker-js/faker';

import {
  HttpResponse,
  delay,
  http
} from 'msw';
import type {
  RequestHandlerOptions
} from 'msw';

import {
  WebsiteStatus
} from '.././model';
import type {
  PublishedSiteDto
} from '.././model';


export const getGetPublishedSiteResponseMock = (overrideResponse: Partial< PublishedSiteDto > = {}): PublishedSiteDto => ({status: faker.helpers.arrayElement(Object.values(WebsiteStatus)), shop: {name: faker.string.alpha({length: {min: 10, max: 20}}), logoUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null])}, publication: {...{id: faker.string.uuid(), version: faker.number.int({min: undefined, max: undefined}), publishedAt: `${faker.date.past().toISOString().split('.')[0]}Z`, snapshot: {}},}, ...overrideResponse})


export const getGetPublishedSiteMockHandler = (overrideResponse?: PublishedSiteDto | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<PublishedSiteDto> | PublishedSiteDto), options?: RequestHandlerOptions) => {
  return http.get('*/api/site', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetPublishedSiteResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}
export const getSiteMock = () => [
  getGetPublishedSiteMockHandler()
]
