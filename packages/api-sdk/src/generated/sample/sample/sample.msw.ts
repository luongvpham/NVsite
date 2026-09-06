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
  SampleStatus
} from '.././model';
import type {
  PagedResultOfSampleSummary,
  SampleDetail
} from '.././model';


export const getListSamplesResponseMock = (overrideResponse: Partial< PagedResultOfSampleSummary > = {}): PagedResultOfSampleSummary => ({items: Array.from({ length: faker.number.int({ min: 1, max: 10 }) }, (_, i) => i + 1).map(() => ({id: faker.string.uuid(), name: faker.string.alpha({length: {min: 10, max: 20}}), status: faker.helpers.arrayElement(Object.values(SampleStatus)), createdAtUtc: `${faker.date.past().toISOString().split('.')[0]}Z`})), total: faker.number.int({min: undefined, max: undefined}), page: faker.number.int({min: undefined, max: undefined}), pageSize: faker.number.int({min: undefined, max: undefined}), ...overrideResponse})

export const getCreateSampleResponseMock = (overrideResponse: Partial< SampleDetail > = {}): SampleDetail => ({id: faker.string.uuid(), shopId: faker.string.uuid(), name: faker.string.alpha({length: {min: 10, max: 20}}), status: faker.helpers.arrayElement(Object.values(SampleStatus)), createdAtUtc: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})

export const getGetSampleByIdResponseMock = (overrideResponse: Partial< SampleDetail > = {}): SampleDetail => ({id: faker.string.uuid(), shopId: faker.string.uuid(), name: faker.string.alpha({length: {min: 10, max: 20}}), status: faker.helpers.arrayElement(Object.values(SampleStatus)), createdAtUtc: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})

export const getGetSampleByIdForShopResponseMock = (overrideResponse: Partial< SampleDetail > = {}): SampleDetail => ({id: faker.string.uuid(), shopId: faker.string.uuid(), name: faker.string.alpha({length: {min: 10, max: 20}}), status: faker.helpers.arrayElement(Object.values(SampleStatus)), createdAtUtc: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})


export const getListSamplesMockHandler = (overrideResponse?: PagedResultOfSampleSummary | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<PagedResultOfSampleSummary> | PagedResultOfSampleSummary), options?: RequestHandlerOptions) => {
  return http.get('*/samples', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getListSamplesResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getCreateSampleMockHandler = (overrideResponse?: SampleDetail | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<SampleDetail> | SampleDetail), options?: RequestHandlerOptions) => {
  return http.post('*/samples', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getCreateSampleResponseMock()),
      { status: 201,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getGetSampleByIdMockHandler = (overrideResponse?: SampleDetail | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<SampleDetail> | SampleDetail), options?: RequestHandlerOptions) => {
  return http.get('*/samples/:id', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetSampleByIdResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getGetSampleByIdForShopMockHandler = (overrideResponse?: SampleDetail | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<SampleDetail> | SampleDetail), options?: RequestHandlerOptions) => {
  return http.get('*/shops/:shopId/samples/:id', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetSampleByIdForShopResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}
export const getSampleMock = () => [
  getListSamplesMockHandler(),
  getCreateSampleMockHandler(),
  getGetSampleByIdMockHandler(),
  getGetSampleByIdForShopMockHandler()
]
