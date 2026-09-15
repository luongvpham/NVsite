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
  ShopKind,
  ShopStatus
} from '.././model';
import type {
  ShopDto,
  ShopSummaryDto
} from '.././model';


export const getPostShopsResponseMock = (overrideResponse: Partial< ShopDto > = {}): ShopDto => ({id: faker.string.uuid(), name: faker.string.alpha({length: {min: 10, max: 20}}), slug: faker.string.alpha({length: {min: 10, max: 20}}), kind: faker.helpers.arrayElement(Object.values(ShopKind)), externalUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), status: faker.helpers.arrayElement(Object.values(ShopStatus)), ...overrideResponse})

export const getGetShopsResponseMock = (): ShopSummaryDto[] => (Array.from({ length: faker.number.int({ min: 1, max: 10 }) }, (_, i) => i + 1).map(() => ({id: faker.string.uuid(), name: faker.string.alpha({length: {min: 10, max: 20}}), slug: faker.string.alpha({length: {min: 10, max: 20}}), kind: faker.helpers.arrayElement(Object.values(ShopKind)), status: faker.helpers.arrayElement(Object.values(ShopStatus)), roleCode: faker.string.alpha({length: {min: 10, max: 20}})})))

export const getGetShopsShopIdResponseMock = (overrideResponse: Partial< ShopDto > = {}): ShopDto => ({id: faker.string.uuid(), name: faker.string.alpha({length: {min: 10, max: 20}}), slug: faker.string.alpha({length: {min: 10, max: 20}}), kind: faker.helpers.arrayElement(Object.values(ShopKind)), externalUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), status: faker.helpers.arrayElement(Object.values(ShopStatus)), ...overrideResponse})

export const getPatchShopsShopIdResponseMock = (overrideResponse: Partial< ShopDto > = {}): ShopDto => ({id: faker.string.uuid(), name: faker.string.alpha({length: {min: 10, max: 20}}), slug: faker.string.alpha({length: {min: 10, max: 20}}), kind: faker.helpers.arrayElement(Object.values(ShopKind)), externalUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), status: faker.helpers.arrayElement(Object.values(ShopStatus)), ...overrideResponse})


export const getPostShopsMockHandler = (overrideResponse?: ShopDto | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<ShopDto> | ShopDto), options?: RequestHandlerOptions) => {
  return http.post('*/shops', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getPostShopsResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getGetShopsMockHandler = (overrideResponse?: ShopSummaryDto[] | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<ShopSummaryDto[]> | ShopSummaryDto[]), options?: RequestHandlerOptions) => {
  return http.get('*/shops', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetShopsResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getGetShopsShopIdMockHandler = (overrideResponse?: ShopDto | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<ShopDto> | ShopDto), options?: RequestHandlerOptions) => {
  return http.get('*/shops/:shopId', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetShopsShopIdResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getPatchShopsShopIdMockHandler = (overrideResponse?: ShopDto | ((info: Parameters<Parameters<typeof http.patch>[1]>[0]) => Promise<ShopDto> | ShopDto), options?: RequestHandlerOptions) => {
  return http.patch('*/shops/:shopId', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getPatchShopsShopIdResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}
export const getShopMock = () => [
  getPostShopsMockHandler(),
  getGetShopsMockHandler(),
  getGetShopsShopIdMockHandler(),
  getPatchShopsShopIdMockHandler()
]
