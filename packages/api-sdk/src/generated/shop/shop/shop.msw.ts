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


export const getCreateShopResponseMock = (overrideResponse: Partial< ShopDto > = {}): ShopDto => ({id: faker.string.uuid(), name: faker.string.alpha({length: {min: 10, max: 20}}), slug: faker.string.alpha({length: {min: 10, max: 20}}), kind: faker.helpers.arrayElement(Object.values(ShopKind)), externalUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), status: faker.helpers.arrayElement(Object.values(ShopStatus)), logoId: faker.helpers.arrayElement([faker.string.uuid(), null]), logoUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), ...overrideResponse})

export const getListShopsResponseMock = (): ShopSummaryDto[] => (Array.from({ length: faker.number.int({ min: 1, max: 10 }) }, (_, i) => i + 1).map(() => ({id: faker.string.uuid(), name: faker.string.alpha({length: {min: 10, max: 20}}), slug: faker.string.alpha({length: {min: 10, max: 20}}), kind: faker.helpers.arrayElement(Object.values(ShopKind)), status: faker.helpers.arrayElement(Object.values(ShopStatus)), roleCode: faker.string.alpha({length: {min: 10, max: 20}}), logoUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null])})))

export const getGetShopResponseMock = (overrideResponse: Partial< ShopDto > = {}): ShopDto => ({id: faker.string.uuid(), name: faker.string.alpha({length: {min: 10, max: 20}}), slug: faker.string.alpha({length: {min: 10, max: 20}}), kind: faker.helpers.arrayElement(Object.values(ShopKind)), externalUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), status: faker.helpers.arrayElement(Object.values(ShopStatus)), logoId: faker.helpers.arrayElement([faker.string.uuid(), null]), logoUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), ...overrideResponse})

export const getUpdateShopResponseMock = (overrideResponse: Partial< ShopDto > = {}): ShopDto => ({id: faker.string.uuid(), name: faker.string.alpha({length: {min: 10, max: 20}}), slug: faker.string.alpha({length: {min: 10, max: 20}}), kind: faker.helpers.arrayElement(Object.values(ShopKind)), externalUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), status: faker.helpers.arrayElement(Object.values(ShopStatus)), logoId: faker.helpers.arrayElement([faker.string.uuid(), null]), logoUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), ...overrideResponse})


export const getCreateShopMockHandler = (overrideResponse?: ShopDto | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<ShopDto> | ShopDto), options?: RequestHandlerOptions) => {
  return http.post('*/api/shops', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getCreateShopResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getListShopsMockHandler = (overrideResponse?: ShopSummaryDto[] | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<ShopSummaryDto[]> | ShopSummaryDto[]), options?: RequestHandlerOptions) => {
  return http.get('*/api/shops', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getListShopsResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getGetShopMockHandler = (overrideResponse?: ShopDto | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<ShopDto> | ShopDto), options?: RequestHandlerOptions) => {
  return http.get('*/api/shops/:shopId', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetShopResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getUpdateShopMockHandler = (overrideResponse?: ShopDto | ((info: Parameters<Parameters<typeof http.patch>[1]>[0]) => Promise<ShopDto> | ShopDto), options?: RequestHandlerOptions) => {
  return http.patch('*/api/shops/:shopId', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getUpdateShopResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}
export const getShopMock = () => [
  getCreateShopMockHandler(),
  getListShopsMockHandler(),
  getGetShopMockHandler(),
  getUpdateShopMockHandler()
]
