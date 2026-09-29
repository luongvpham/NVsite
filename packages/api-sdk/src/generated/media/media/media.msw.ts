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
  MediaReferenceKind
} from '.././model';
import type {
  MediaAssetDto,
  MediaReferencesDto,
  MediaUsageDto,
  PagedResultOfMediaAssetDto,
  ShopLogoDto,
  SlotUploadResultDto
} from '.././model';


export const getPutShopsShopIdLogoResponseMock = (overrideResponse: Partial< ShopLogoDto > = {}): ShopLogoDto => ({libraryAsset: {id: faker.string.uuid(), storageKey: faker.string.alpha({length: {min: 10, max: 20}}), mimeType: faker.string.alpha({length: {min: 10, max: 20}}), width: faker.number.int({min: undefined, max: undefined}), height: faker.number.int({min: undefined, max: undefined}), sizeBytes: faker.number.int({min: undefined, max: undefined}), altText: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), focalPointX: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), focalPointY: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), originalFileName: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), folder: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), isInLibrary: faker.datatype.boolean(), preset: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), sourceAssetId: faker.helpers.arrayElement([faker.string.uuid(), null]), createdAt: `${faker.date.past().toISOString().split('.')[0]}Z`}, derivatives: Array.from({ length: faker.number.int({ min: 1, max: 10 }) }, (_, i) => i + 1).map(() => ({id: faker.string.uuid(), storageKey: faker.string.alpha({length: {min: 10, max: 20}}), mimeType: faker.string.alpha({length: {min: 10, max: 20}}), width: faker.number.int({min: undefined, max: undefined}), height: faker.number.int({min: undefined, max: undefined}), sizeBytes: faker.number.int({min: undefined, max: undefined}), altText: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), focalPointX: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), focalPointY: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), originalFileName: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), folder: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), isInLibrary: faker.datatype.boolean(), preset: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), sourceAssetId: faker.helpers.arrayElement([faker.string.uuid(), null]), createdAt: `${faker.date.past().toISOString().split('.')[0]}Z`})), ...overrideResponse})

export const getPostShopsShopIdMediaSlotUploadsResponseMock = (overrideResponse: Partial< SlotUploadResultDto > = {}): SlotUploadResultDto => ({asset: {id: faker.string.uuid(), storageKey: faker.string.alpha({length: {min: 10, max: 20}}), mimeType: faker.string.alpha({length: {min: 10, max: 20}}), width: faker.number.int({min: undefined, max: undefined}), height: faker.number.int({min: undefined, max: undefined}), sizeBytes: faker.number.int({min: undefined, max: undefined}), altText: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), focalPointX: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), focalPointY: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), originalFileName: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), folder: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), isInLibrary: faker.datatype.boolean(), preset: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), sourceAssetId: faker.helpers.arrayElement([faker.string.uuid(), null]), createdAt: `${faker.date.past().toISOString().split('.')[0]}Z`}, libraryAsset: {...{id: faker.string.uuid(), storageKey: faker.string.alpha({length: {min: 10, max: 20}}), mimeType: faker.string.alpha({length: {min: 10, max: 20}}), width: faker.number.int({min: undefined, max: undefined}), height: faker.number.int({min: undefined, max: undefined}), sizeBytes: faker.number.int({min: undefined, max: undefined}), altText: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), focalPointX: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), focalPointY: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), originalFileName: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), folder: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), isInLibrary: faker.datatype.boolean(), preset: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), sourceAssetId: faker.helpers.arrayElement([faker.string.uuid(), null]), createdAt: `${faker.date.past().toISOString().split('.')[0]}Z`},}, ...overrideResponse})

export const getPostShopsShopIdMediaLibraryResponseMock = (overrideResponse: Partial< MediaAssetDto > = {}): MediaAssetDto => ({id: faker.string.uuid(), storageKey: faker.string.alpha({length: {min: 10, max: 20}}), mimeType: faker.string.alpha({length: {min: 10, max: 20}}), width: faker.number.int({min: undefined, max: undefined}), height: faker.number.int({min: undefined, max: undefined}), sizeBytes: faker.number.int({min: undefined, max: undefined}), altText: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), focalPointX: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), focalPointY: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), originalFileName: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), folder: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), isInLibrary: faker.datatype.boolean(), preset: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), sourceAssetId: faker.helpers.arrayElement([faker.string.uuid(), null]), createdAt: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})

export const getGetShopsShopIdMediaLibraryResponseMock = (overrideResponse: Partial< PagedResultOfMediaAssetDto > = {}): PagedResultOfMediaAssetDto => ({items: Array.from({ length: faker.number.int({ min: 1, max: 10 }) }, (_, i) => i + 1).map(() => ({id: faker.string.uuid(), storageKey: faker.string.alpha({length: {min: 10, max: 20}}), mimeType: faker.string.alpha({length: {min: 10, max: 20}}), width: faker.number.int({min: undefined, max: undefined}), height: faker.number.int({min: undefined, max: undefined}), sizeBytes: faker.number.int({min: undefined, max: undefined}), altText: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), focalPointX: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), focalPointY: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), originalFileName: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), folder: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), isInLibrary: faker.datatype.boolean(), preset: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), sourceAssetId: faker.helpers.arrayElement([faker.string.uuid(), null]), createdAt: `${faker.date.past().toISOString().split('.')[0]}Z`})), total: faker.number.int({min: undefined, max: undefined}), page: faker.number.int({min: undefined, max: undefined}), pageSize: faker.number.int({min: undefined, max: undefined}), ...overrideResponse})

export const getPostShopsShopIdMediaLibraryAssetIdClonesResponseMock = (overrideResponse: Partial< MediaAssetDto > = {}): MediaAssetDto => ({id: faker.string.uuid(), storageKey: faker.string.alpha({length: {min: 10, max: 20}}), mimeType: faker.string.alpha({length: {min: 10, max: 20}}), width: faker.number.int({min: undefined, max: undefined}), height: faker.number.int({min: undefined, max: undefined}), sizeBytes: faker.number.int({min: undefined, max: undefined}), altText: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), focalPointX: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), focalPointY: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), originalFileName: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), folder: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), isInLibrary: faker.datatype.boolean(), preset: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), sourceAssetId: faker.helpers.arrayElement([faker.string.uuid(), null]), createdAt: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})

export const getGetShopsShopIdMediaLibraryAssetIdReferencesResponseMock = (overrideResponse: Partial< MediaReferencesDto > = {}): MediaReferencesDto => ({references: Array.from({ length: faker.number.int({ min: 1, max: 10 }) }, (_, i) => i + 1).map(() => ({kind: faker.helpers.arrayElement(Object.values(MediaReferenceKind))})), ...overrideResponse})

export const getGetShopsShopIdMediaLibraryAssetIdDerivativesResponseMock = (): MediaAssetDto[] => (Array.from({ length: faker.number.int({ min: 1, max: 10 }) }, (_, i) => i + 1).map(() => ({id: faker.string.uuid(), storageKey: faker.string.alpha({length: {min: 10, max: 20}}), mimeType: faker.string.alpha({length: {min: 10, max: 20}}), width: faker.number.int({min: undefined, max: undefined}), height: faker.number.int({min: undefined, max: undefined}), sizeBytes: faker.number.int({min: undefined, max: undefined}), altText: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), focalPointX: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), focalPointY: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), originalFileName: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), folder: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), isInLibrary: faker.datatype.boolean(), preset: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), sourceAssetId: faker.helpers.arrayElement([faker.string.uuid(), null]), createdAt: `${faker.date.past().toISOString().split('.')[0]}Z`})))

export const getGetShopsShopIdMediaAssetsResponseMock = (): MediaAssetDto[] => (Array.from({ length: faker.number.int({ min: 1, max: 10 }) }, (_, i) => i + 1).map(() => ({id: faker.string.uuid(), storageKey: faker.string.alpha({length: {min: 10, max: 20}}), mimeType: faker.string.alpha({length: {min: 10, max: 20}}), width: faker.number.int({min: undefined, max: undefined}), height: faker.number.int({min: undefined, max: undefined}), sizeBytes: faker.number.int({min: undefined, max: undefined}), altText: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), focalPointX: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), focalPointY: faker.number.float({min: undefined, max: undefined, fractionDigits: 2}), originalFileName: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), folder: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), isInLibrary: faker.datatype.boolean(), preset: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), sourceAssetId: faker.helpers.arrayElement([faker.string.uuid(), null]), createdAt: `${faker.date.past().toISOString().split('.')[0]}Z`})))

export const getGetShopsShopIdMediaUsageResponseMock = (overrideResponse: Partial< MediaUsageDto > = {}): MediaUsageDto => ({usedBytes: faker.number.int({min: undefined, max: undefined}), ...overrideResponse})


export const getPutShopsShopIdLogoMockHandler = (overrideResponse?: ShopLogoDto | ((info: Parameters<Parameters<typeof http.put>[1]>[0]) => Promise<ShopLogoDto> | ShopLogoDto), options?: RequestHandlerOptions) => {
  return http.put('*/shops/:shopId/logo', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getPutShopsShopIdLogoResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getPostShopsShopIdMediaSlotUploadsMockHandler = (overrideResponse?: SlotUploadResultDto | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<SlotUploadResultDto> | SlotUploadResultDto), options?: RequestHandlerOptions) => {
  return http.post('*/shops/:shopId/media/slot-uploads', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getPostShopsShopIdMediaSlotUploadsResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getPostShopsShopIdMediaLibraryMockHandler = (overrideResponse?: MediaAssetDto | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<MediaAssetDto> | MediaAssetDto), options?: RequestHandlerOptions) => {
  return http.post('*/shops/:shopId/media/library', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getPostShopsShopIdMediaLibraryResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getGetShopsShopIdMediaLibraryMockHandler = (overrideResponse?: PagedResultOfMediaAssetDto | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<PagedResultOfMediaAssetDto> | PagedResultOfMediaAssetDto), options?: RequestHandlerOptions) => {
  return http.get('*/shops/:shopId/media/library', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetShopsShopIdMediaLibraryResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getPostShopsShopIdMediaLibraryAssetIdClonesMockHandler = (overrideResponse?: MediaAssetDto | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<MediaAssetDto> | MediaAssetDto), options?: RequestHandlerOptions) => {
  return http.post('*/shops/:shopId/media/library/:assetId/clones', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getPostShopsShopIdMediaLibraryAssetIdClonesResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getGetShopsShopIdMediaLibraryAssetIdReferencesMockHandler = (overrideResponse?: MediaReferencesDto | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<MediaReferencesDto> | MediaReferencesDto), options?: RequestHandlerOptions) => {
  return http.get('*/shops/:shopId/media/library/:assetId/references', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetShopsShopIdMediaLibraryAssetIdReferencesResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getGetShopsShopIdMediaLibraryAssetIdDerivativesMockHandler = (overrideResponse?: MediaAssetDto[] | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<MediaAssetDto[]> | MediaAssetDto[]), options?: RequestHandlerOptions) => {
  return http.get('*/shops/:shopId/media/library/:assetId/derivatives', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetShopsShopIdMediaLibraryAssetIdDerivativesResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getDeleteShopsShopIdMediaLibraryAssetIdMockHandler = (overrideResponse?: void | ((info: Parameters<Parameters<typeof http.delete>[1]>[0]) => Promise<void> | void), options?: RequestHandlerOptions) => {
  return http.delete('*/shops/:shopId/media/library/:assetId', async (info) => {await delay(1000);
  if (typeof overrideResponse === 'function') {await overrideResponse(info); }
    return new HttpResponse(null,
      { status: 204,
        
      })
  }, options)
}

export const getGetShopsShopIdMediaAssetsMockHandler = (overrideResponse?: MediaAssetDto[] | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<MediaAssetDto[]> | MediaAssetDto[]), options?: RequestHandlerOptions) => {
  return http.get('*/shops/:shopId/media/assets', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetShopsShopIdMediaAssetsResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getGetShopsShopIdMediaUsageMockHandler = (overrideResponse?: MediaUsageDto | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<MediaUsageDto> | MediaUsageDto), options?: RequestHandlerOptions) => {
  return http.get('*/shops/:shopId/media/usage', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetShopsShopIdMediaUsageResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}
export const getMediaMock = () => [
  getPutShopsShopIdLogoMockHandler(),
  getPostShopsShopIdMediaSlotUploadsMockHandler(),
  getPostShopsShopIdMediaLibraryMockHandler(),
  getGetShopsShopIdMediaLibraryMockHandler(),
  getPostShopsShopIdMediaLibraryAssetIdClonesMockHandler(),
  getGetShopsShopIdMediaLibraryAssetIdReferencesMockHandler(),
  getGetShopsShopIdMediaLibraryAssetIdDerivativesMockHandler(),
  getDeleteShopsShopIdMediaLibraryAssetIdMockHandler(),
  getGetShopsShopIdMediaAssetsMockHandler(),
  getGetShopsShopIdMediaUsageMockHandler()
]
