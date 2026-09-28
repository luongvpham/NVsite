/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import * as zod from 'zod';

export const putShopsShopIdLogoParams = zod.object({
  "shopId": zod.string().uuid()
})

export const putShopsShopIdLogoBody = zod.object({
  "file": zod.instanceof(File).optional()
})

export const putShopsShopIdLogoResponse = zod.object({
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


export const postShopsShopIdMediaSlotUploadsParams = zod.object({
  "shopId": zod.string().uuid()
})

export const postShopsShopIdMediaSlotUploadsBody = zod.object({
  "file": zod.instanceof(File).optional(),
  "preset": zod.string().optional(),
  "focalX": zod.number().optional(),
  "focalY": zod.number().optional(),
  "saveToLibrary": zod.boolean().optional(),
  "altText": zod.string().nullish()
})

export const postShopsShopIdMediaSlotUploadsResponse = zod.object({
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


export const postShopsShopIdMediaLibraryParams = zod.object({
  "shopId": zod.string().uuid()
})

export const postShopsShopIdMediaLibraryBody = zod.object({
  "file": zod.instanceof(File).optional(),
  "altText": zod.string().nullish(),
  "folder": zod.string().nullish()
})

export const postShopsShopIdMediaLibraryResponse = zod.object({
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


export const getShopsShopIdMediaLibraryParams = zod.object({
  "shopId": zod.string().uuid()
})

export const getShopsShopIdMediaLibraryQueryPageDefault = 1;export const getShopsShopIdMediaLibraryQueryPageSizeDefault = 24;

export const getShopsShopIdMediaLibraryQueryParams = zod.object({
  "page": zod.number().default(getShopsShopIdMediaLibraryQueryPageDefault),
  "pageSize": zod.number().default(getShopsShopIdMediaLibraryQueryPageSizeDefault)
})

export const getShopsShopIdMediaLibraryResponse = zod.object({
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


export const postShopsShopIdMediaLibraryAssetIdClonesParams = zod.object({
  "shopId": zod.string().uuid(),
  "assetId": zod.string().uuid()
})

export const postShopsShopIdMediaLibraryAssetIdClonesBody = zod.object({
  "preset": zod.string(),
  "focalX": zod.number().nullable(),
  "focalY": zod.number().nullable()
})

export const postShopsShopIdMediaLibraryAssetIdClonesResponse = zod.object({
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


export const getShopsShopIdMediaLibraryAssetIdReferencesParams = zod.object({
  "shopId": zod.string().uuid(),
  "assetId": zod.string().uuid()
})

export const getShopsShopIdMediaLibraryAssetIdReferencesResponse = zod.object({
  "references": zod.array(zod.object({
  "kind": zod.enum(['ShopLogo'])
}))
})


export const deleteShopsShopIdMediaLibraryAssetIdParams = zod.object({
  "shopId": zod.string().uuid(),
  "assetId": zod.string().uuid()
})


export const getShopsShopIdMediaAssetsParams = zod.object({
  "shopId": zod.string().uuid()
})

export const getShopsShopIdMediaAssetsQueryParams = zod.object({
  "ids": zod.array(zod.string().uuid()).optional()
})

export const getShopsShopIdMediaAssetsResponseItem = zod.object({
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
export const getShopsShopIdMediaAssetsResponse = zod.array(getShopsShopIdMediaAssetsResponseItem)


export const getShopsShopIdMediaUsageParams = zod.object({
  "shopId": zod.string().uuid()
})

export const getShopsShopIdMediaUsageResponse = zod.object({
  "usedBytes": zod.number()
})
