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
  PageKind,
  WebsiteStatus
} from '.././model';
import type {
  PageDraftDto,
  PageDto,
  SavePageDraftResultDto,
  ThemeDto,
  WebsiteDto
} from '.././model';


export const getCreateWebsiteResponseMock = (overrideResponse: Partial< WebsiteDto > = {}): WebsiteDto => ({id: faker.string.uuid(), status: faker.helpers.arrayElement(Object.values(WebsiteStatus)), defaultLocale: faker.string.alpha({length: {min: 10, max: 20}}), locales: Array.from({ length: faker.number.int({ min: 1, max: 10 }) }, (_, i) => i + 1).map(() => (faker.string.alpha({length: {min: 10, max: 20}}))), defaultSeoTitle: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), defaultSeoDescription: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), defaultOgImageId: faker.helpers.arrayElement([faker.string.uuid(), null]), faviconAssetId: faker.helpers.arrayElement([faker.string.uuid(), null]), trackingSnippets: {}, homePageId: faker.string.uuid(), ...overrideResponse})

export const getGetWebsiteResponseMock = (overrideResponse: Partial< WebsiteDto > = {}): WebsiteDto => ({id: faker.string.uuid(), status: faker.helpers.arrayElement(Object.values(WebsiteStatus)), defaultLocale: faker.string.alpha({length: {min: 10, max: 20}}), locales: Array.from({ length: faker.number.int({ min: 1, max: 10 }) }, (_, i) => i + 1).map(() => (faker.string.alpha({length: {min: 10, max: 20}}))), defaultSeoTitle: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), defaultSeoDescription: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), defaultOgImageId: faker.helpers.arrayElement([faker.string.uuid(), null]), faviconAssetId: faker.helpers.arrayElement([faker.string.uuid(), null]), trackingSnippets: {}, homePageId: faker.string.uuid(), ...overrideResponse})

export const getListPagesResponseMock = (): PageDto[] => (Array.from({ length: faker.number.int({ min: 1, max: 10 }) }, (_, i) => i + 1).map(() => ({id: faker.string.uuid(), kind: faker.helpers.arrayElement(Object.values(PageKind)), systemType: faker.helpers.arrayElement(['Contact','NotFound','ProductListing','ProductDetail','ProductSearch','Booking'] as const), slug: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), title: faker.string.alpha({length: {min: 10, max: 20}}), seoTitle: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), seoDescription: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), ogImageId: faker.helpers.arrayElement([faker.string.uuid(), null]), noIndex: faker.datatype.boolean(), isEnabled: faker.datatype.boolean(), sortOrder: faker.number.int({min: undefined, max: undefined}), isHomePage: faker.datatype.boolean(), isDirty: faker.datatype.boolean(), deletable: faker.datatype.boolean(), updatedAt: `${faker.date.past().toISOString().split('.')[0]}Z`})))

export const getCreatePageResponseMock = (overrideResponse: Partial< PageDto > = {}): PageDto => ({id: faker.string.uuid(), kind: faker.helpers.arrayElement(Object.values(PageKind)), systemType: faker.helpers.arrayElement(['Contact','NotFound','ProductListing','ProductDetail','ProductSearch','Booking'] as const), slug: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), title: faker.string.alpha({length: {min: 10, max: 20}}), seoTitle: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), seoDescription: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), ogImageId: faker.helpers.arrayElement([faker.string.uuid(), null]), noIndex: faker.datatype.boolean(), isEnabled: faker.datatype.boolean(), sortOrder: faker.number.int({min: undefined, max: undefined}), isHomePage: faker.datatype.boolean(), isDirty: faker.datatype.boolean(), deletable: faker.datatype.boolean(), updatedAt: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})

export const getUpdatePageResponseMock = (overrideResponse: Partial< PageDto > = {}): PageDto => ({id: faker.string.uuid(), kind: faker.helpers.arrayElement(Object.values(PageKind)), systemType: faker.helpers.arrayElement(['Contact','NotFound','ProductListing','ProductDetail','ProductSearch','Booking'] as const), slug: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), title: faker.string.alpha({length: {min: 10, max: 20}}), seoTitle: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), seoDescription: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), ogImageId: faker.helpers.arrayElement([faker.string.uuid(), null]), noIndex: faker.datatype.boolean(), isEnabled: faker.datatype.boolean(), sortOrder: faker.number.int({min: undefined, max: undefined}), isHomePage: faker.datatype.boolean(), isDirty: faker.datatype.boolean(), deletable: faker.datatype.boolean(), updatedAt: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})

export const getGetPageDraftResponseMock = (overrideResponse: Partial< PageDraftDto > = {}): PageDraftDto => ({tree: {}, version: faker.number.int({min: undefined, max: undefined}), isDirty: faker.datatype.boolean(), updatedAt: `${faker.date.past().toISOString().split('.')[0]}Z`, page: {id: faker.string.uuid(), kind: faker.helpers.arrayElement(Object.values(PageKind)), systemType: faker.helpers.arrayElement(['Contact','NotFound','ProductListing','ProductDetail','ProductSearch','Booking'] as const)}, ...overrideResponse})

export const getSavePageDraftResponseMock = (overrideResponse: Partial< SavePageDraftResultDto > = {}): SavePageDraftResultDto => ({version: faker.number.int({min: undefined, max: undefined}), isDirty: faker.datatype.boolean(), sanitized: faker.datatype.boolean(), ...overrideResponse})

export const getGetThemeResponseMock = (overrideResponse: Partial< ThemeDto > = {}): ThemeDto => ({presetKey: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), tokens: {}, updatedAt: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})

export const getUpdateThemeResponseMock = (overrideResponse: Partial< ThemeDto > = {}): ThemeDto => ({presetKey: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), tokens: {}, updatedAt: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})


export const getCreateWebsiteMockHandler = (overrideResponse?: WebsiteDto | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<WebsiteDto> | WebsiteDto), options?: RequestHandlerOptions) => {
  return http.post('*/api/shops/:shopId/website', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getCreateWebsiteResponseMock()),
      { status: 201,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getGetWebsiteMockHandler = (overrideResponse?: WebsiteDto | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<WebsiteDto> | WebsiteDto), options?: RequestHandlerOptions) => {
  return http.get('*/api/shops/:shopId/website', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetWebsiteResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getListPagesMockHandler = (overrideResponse?: PageDto[] | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<PageDto[]> | PageDto[]), options?: RequestHandlerOptions) => {
  return http.get('*/api/shops/:shopId/website/pages', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getListPagesResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getCreatePageMockHandler = (overrideResponse?: PageDto | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<PageDto> | PageDto), options?: RequestHandlerOptions) => {
  return http.post('*/api/shops/:shopId/website/pages', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getCreatePageResponseMock()),
      { status: 201,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getUpdatePageMockHandler = (overrideResponse?: PageDto | ((info: Parameters<Parameters<typeof http.put>[1]>[0]) => Promise<PageDto> | PageDto), options?: RequestHandlerOptions) => {
  return http.put('*/api/shops/:shopId/website/pages/:pageId', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getUpdatePageResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getDeletePageMockHandler = (overrideResponse?: void | ((info: Parameters<Parameters<typeof http.delete>[1]>[0]) => Promise<void> | void), options?: RequestHandlerOptions) => {
  return http.delete('*/api/shops/:shopId/website/pages/:pageId', async (info) => {await delay(1000);
  if (typeof overrideResponse === 'function') {await overrideResponse(info); }
    return new HttpResponse(null,
      { status: 204,
        
      })
  }, options)
}

export const getGetPageDraftMockHandler = (overrideResponse?: PageDraftDto | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<PageDraftDto> | PageDraftDto), options?: RequestHandlerOptions) => {
  return http.get('*/api/shops/:shopId/website/pages/:pageId/draft', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetPageDraftResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getSavePageDraftMockHandler = (overrideResponse?: SavePageDraftResultDto | ((info: Parameters<Parameters<typeof http.put>[1]>[0]) => Promise<SavePageDraftResultDto> | SavePageDraftResultDto), options?: RequestHandlerOptions) => {
  return http.put('*/api/shops/:shopId/website/pages/:pageId/draft', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getSavePageDraftResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getGetThemeMockHandler = (overrideResponse?: ThemeDto | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<ThemeDto> | ThemeDto), options?: RequestHandlerOptions) => {
  return http.get('*/api/shops/:shopId/website/theme', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetThemeResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getUpdateThemeMockHandler = (overrideResponse?: ThemeDto | ((info: Parameters<Parameters<typeof http.put>[1]>[0]) => Promise<ThemeDto> | ThemeDto), options?: RequestHandlerOptions) => {
  return http.put('*/api/shops/:shopId/website/theme', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getUpdateThemeResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}
export const getWebsiteMock = () => [
  getCreateWebsiteMockHandler(),
  getGetWebsiteMockHandler(),
  getListPagesMockHandler(),
  getCreatePageMockHandler(),
  getUpdatePageMockHandler(),
  getDeletePageMockHandler(),
  getGetPageDraftMockHandler(),
  getSavePageDraftMockHandler(),
  getGetThemeMockHandler(),
  getUpdateThemeMockHandler()
]
