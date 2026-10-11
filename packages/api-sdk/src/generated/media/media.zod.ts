/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import * as zod from 'zod';

export const uploadToSlotParams = zod.object({
  "shopId": zod.string().uuid()
})

export const uploadToSlotBody = zod.object({
  "file": zod.instanceof(File).optional(),
  "preset": zod.string().optional(),
  "focalX": zod.number().optional(),
  "focalY": zod.number().optional(),
  "saveToLibrary": zod.boolean().optional(),
  "altText": zod.string().nullish()
})

export const uploadToSlotResponse = zod.object({
  "asset": zod.object({
  "id": zod.string().uuid(),
  "storageKey": zod.string(),
  "mimeType": zod.string(),
  "width": zod.number(),
  "height": zod.number(),
  "sizeBytes": zod.number(),
  "altText": zod.string().nullable(),
  "focalPointX": zod.number(),
  "focalPointY": zod.number(),
  "originalFileName": zod.string().nullable(),
  "folder": zod.string().nullable(),
  "isInLibrary": zod.boolean(),
  "preset": zod.string().nullable(),
  "sourceAssetId": zod.string().uuid().nullable(),
  "createdAt": zod.string().datetime({})
}),
  "libraryAsset": zod.object({
  "id": zod.string().uuid(),
  "storageKey": zod.string(),
  "mimeType": zod.string(),
  "width": zod.number(),
  "height": zod.number(),
  "sizeBytes": zod.number(),
  "altText": zod.string().nullable(),
  "focalPointX": zod.number(),
  "focalPointY": zod.number(),
  "originalFileName": zod.string().nullable(),
  "folder": zod.string().nullable(),
  "isInLibrary": zod.boolean(),
  "preset": zod.string().nullable(),
  "sourceAssetId": zod.string().uuid().nullable(),
  "createdAt": zod.string().datetime({})
}).nullable()
})


export const uploadToLibraryParams = zod.object({
  "shopId": zod.string().uuid()
})

export const uploadToLibraryBody = zod.object({
  "file": zod.instanceof(File).optional(),
  "altText": zod.string().nullish(),
  "folder": zod.string().nullish()
})

export const uploadToLibraryResponse = zod.object({
  "id": zod.string().uuid(),
  "storageKey": zod.string(),
  "mimeType": zod.string(),
  "width": zod.number(),
  "height": zod.number(),
  "sizeBytes": zod.number(),
  "altText": zod.string().nullable(),
  "focalPointX": zod.number(),
  "focalPointY": zod.number(),
  "originalFileName": zod.string().nullable(),
  "folder": zod.string().nullable(),
  "isInLibrary": zod.boolean(),
  "preset": zod.string().nullable(),
  "sourceAssetId": zod.string().uuid().nullable(),
  "createdAt": zod.string().datetime({})
})


export const listLibraryParams = zod.object({
  "shopId": zod.string().uuid()
})

export const listLibraryQueryPageDefault = 1;export const listLibraryQueryPageSizeDefault = 24;

export const listLibraryQueryParams = zod.object({
  "page": zod.number().default(listLibraryQueryPageDefault),
  "pageSize": zod.number().default(listLibraryQueryPageSizeDefault)
})

export const listLibraryResponse = zod.object({
  "items": zod.array(zod.object({
  "id": zod.string().uuid(),
  "storageKey": zod.string(),
  "mimeType": zod.string(),
  "width": zod.number(),
  "height": zod.number(),
  "sizeBytes": zod.number(),
  "altText": zod.string().nullable(),
  "focalPointX": zod.number(),
  "focalPointY": zod.number(),
  "originalFileName": zod.string().nullable(),
  "folder": zod.string().nullable(),
  "isInLibrary": zod.boolean(),
  "preset": zod.string().nullable(),
  "sourceAssetId": zod.string().uuid().nullable(),
  "createdAt": zod.string().datetime({})
})),
  "total": zod.number(),
  "page": zod.number(),
  "pageSize": zod.number()
})


export const cloneFromLibraryParams = zod.object({
  "shopId": zod.string().uuid(),
  "assetId": zod.string().uuid()
})

export const cloneFromLibraryBody = zod.object({
  "preset": zod.string(),
  "focalX": zod.number().nullable(),
  "focalY": zod.number().nullable()
})

export const cloneFromLibraryResponse = zod.object({
  "id": zod.string().uuid(),
  "storageKey": zod.string(),
  "mimeType": zod.string(),
  "width": zod.number(),
  "height": zod.number(),
  "sizeBytes": zod.number(),
  "altText": zod.string().nullable(),
  "focalPointX": zod.number(),
  "focalPointY": zod.number(),
  "originalFileName": zod.string().nullable(),
  "folder": zod.string().nullable(),
  "isInLibrary": zod.boolean(),
  "preset": zod.string().nullable(),
  "sourceAssetId": zod.string().uuid().nullable(),
  "createdAt": zod.string().datetime({})
})


export const getAssetReferencesParams = zod.object({
  "shopId": zod.string().uuid(),
  "assetId": zod.string().uuid()
})

export const getAssetReferencesResponse = zod.object({
  "references": zod.array(zod.object({
  "kind": zod.enum(['ShopLogo'])
}))
})


export const getDerivativesParams = zod.object({
  "shopId": zod.string().uuid(),
  "assetId": zod.string().uuid()
})

export const getDerivativesQueryParams = zod.object({
  "preset": zod.string().optional()
})

export const getDerivativesResponseItem = zod.object({
  "id": zod.string().uuid(),
  "storageKey": zod.string(),
  "mimeType": zod.string(),
  "width": zod.number(),
  "height": zod.number(),
  "sizeBytes": zod.number(),
  "altText": zod.string().nullable(),
  "focalPointX": zod.number(),
  "focalPointY": zod.number(),
  "originalFileName": zod.string().nullable(),
  "folder": zod.string().nullable(),
  "isInLibrary": zod.boolean(),
  "preset": zod.string().nullable(),
  "sourceAssetId": zod.string().uuid().nullable(),
  "createdAt": zod.string().datetime({})
})
export const getDerivativesResponse = zod.array(getDerivativesResponseItem)


export const deleteFromLibraryParams = zod.object({
  "shopId": zod.string().uuid(),
  "assetId": zod.string().uuid()
})


export const getAssetsByIdsParams = zod.object({
  "shopId": zod.string().uuid()
})

export const getAssetsByIdsQueryParams = zod.object({
  "ids": zod.array(zod.string().uuid()).optional()
})

export const getAssetsByIdsResponseItem = zod.object({
  "id": zod.string().uuid(),
  "storageKey": zod.string(),
  "mimeType": zod.string(),
  "width": zod.number(),
  "height": zod.number(),
  "sizeBytes": zod.number(),
  "altText": zod.string().nullable(),
  "focalPointX": zod.number(),
  "focalPointY": zod.number(),
  "originalFileName": zod.string().nullable(),
  "folder": zod.string().nullable(),
  "isInLibrary": zod.boolean(),
  "preset": zod.string().nullable(),
  "sourceAssetId": zod.string().uuid().nullable(),
  "createdAt": zod.string().datetime({})
})
export const getAssetsByIdsResponse = zod.array(getAssetsByIdsResponseItem)


export const getMediaUsageParams = zod.object({
  "shopId": zod.string().uuid()
})

export const getMediaUsageResponse = zod.object({
  "usedBytes": zod.number()
})


export const uploadShopLogoParams = zod.object({
  "shopId": zod.string().uuid()
})

export const uploadShopLogoBody = zod.object({
  "file": zod.instanceof(File).optional()
})

export const uploadShopLogoResponse = zod.object({
  "libraryAsset": zod.object({
  "id": zod.string().uuid(),
  "storageKey": zod.string(),
  "mimeType": zod.string(),
  "width": zod.number(),
  "height": zod.number(),
  "sizeBytes": zod.number(),
  "altText": zod.string().nullable(),
  "focalPointX": zod.number(),
  "focalPointY": zod.number(),
  "originalFileName": zod.string().nullable(),
  "folder": zod.string().nullable(),
  "isInLibrary": zod.boolean(),
  "preset": zod.string().nullable(),
  "sourceAssetId": zod.string().uuid().nullable(),
  "createdAt": zod.string().datetime({})
}),
  "derivatives": zod.array(zod.object({
  "id": zod.string().uuid(),
  "storageKey": zod.string(),
  "mimeType": zod.string(),
  "width": zod.number(),
  "height": zod.number(),
  "sizeBytes": zod.number(),
  "altText": zod.string().nullable(),
  "focalPointX": zod.number(),
  "focalPointY": zod.number(),
  "originalFileName": zod.string().nullable(),
  "folder": zod.string().nullable(),
  "isInLibrary": zod.boolean(),
  "preset": zod.string().nullable(),
  "sourceAssetId": zod.string().uuid().nullable(),
  "createdAt": zod.string().datetime({})
}))
})
