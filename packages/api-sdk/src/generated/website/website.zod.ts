/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import * as zod from 'zod';

export const createWebsiteParams = zod.object({
  "shopId": zod.string().uuid()
})


export const getWebsiteParams = zod.object({
  "shopId": zod.string().uuid()
})

export const getWebsiteResponse = zod.object({
  "id": zod.string().uuid(),
  "status": zod.enum(['Draft', 'Published', 'Suspended']),
  "defaultLocale": zod.string(),
  "locales": zod.array(zod.string()),
  "defaultSeoTitle": zod.string().nullable(),
  "defaultSeoDescription": zod.string().nullable(),
  "defaultOgImageId": zod.string().uuid().nullable(),
  "faviconAssetId": zod.string().uuid().nullable(),
  "trackingSnippets": zod.unknown(),
  "homePageId": zod.string().uuid()
})


export const listPagesParams = zod.object({
  "shopId": zod.string().uuid()
})

export const listPagesResponseItem = zod.object({
  "id": zod.string().uuid(),
  "kind": zod.enum(['Composable', 'System']),
  "systemType": zod.union([zod.literal('Contact'),zod.literal('NotFound'),zod.literal('ProductListing'),zod.literal('ProductDetail'),zod.literal('ProductSearch'),zod.literal('Booking'),zod.literal(null)]).nullable(),
  "slug": zod.string().nullable(),
  "title": zod.string(),
  "seoTitle": zod.string().nullable(),
  "seoDescription": zod.string().nullable(),
  "ogImageId": zod.string().uuid().nullable(),
  "noIndex": zod.boolean(),
  "isEnabled": zod.boolean(),
  "sortOrder": zod.number(),
  "isHomePage": zod.boolean(),
  "isDirty": zod.boolean(),
  "deletable": zod.boolean(),
  "updatedAt": zod.string().datetime({})
})
export const listPagesResponse = zod.array(listPagesResponseItem)


export const createPageParams = zod.object({
  "shopId": zod.string().uuid()
})

export const createPageBodyNoIndexDefault = false;export const createPageBodyIsEnabledDefault = true;

export const createPageBody = zod.object({
  "slug": zod.string(),
  "title": zod.string(),
  "seoTitle": zod.string().nullable(),
  "seoDescription": zod.string().nullable(),
  "ogImageId": zod.string().uuid().nullable(),
  "noIndex": zod.boolean().optional(),
  "isEnabled": zod.boolean().default(createPageBodyIsEnabledDefault)
})


export const updatePageParams = zod.object({
  "shopId": zod.string().uuid(),
  "pageId": zod.string().uuid()
})

export const updatePageBody = zod.object({
  "slug": zod.string().nullable(),
  "title": zod.string(),
  "seoTitle": zod.string().nullable(),
  "seoDescription": zod.string().nullable(),
  "ogImageId": zod.string().uuid().nullable(),
  "noIndex": zod.boolean(),
  "isEnabled": zod.boolean()
})

export const updatePageResponse = zod.object({
  "id": zod.string().uuid(),
  "kind": zod.enum(['Composable', 'System']),
  "systemType": zod.union([zod.literal('Contact'),zod.literal('NotFound'),zod.literal('ProductListing'),zod.literal('ProductDetail'),zod.literal('ProductSearch'),zod.literal('Booking'),zod.literal(null)]).nullable(),
  "slug": zod.string().nullable(),
  "title": zod.string(),
  "seoTitle": zod.string().nullable(),
  "seoDescription": zod.string().nullable(),
  "ogImageId": zod.string().uuid().nullable(),
  "noIndex": zod.boolean(),
  "isEnabled": zod.boolean(),
  "sortOrder": zod.number(),
  "isHomePage": zod.boolean(),
  "isDirty": zod.boolean(),
  "deletable": zod.boolean(),
  "updatedAt": zod.string().datetime({})
})


export const deletePageParams = zod.object({
  "shopId": zod.string().uuid(),
  "pageId": zod.string().uuid()
})


export const getPageDraftParams = zod.object({
  "shopId": zod.string().uuid(),
  "pageId": zod.string().uuid()
})

export const getPageDraftResponse = zod.object({
  "tree": zod.unknown(),
  "version": zod.number(),
  "isDirty": zod.boolean(),
  "updatedAt": zod.string().datetime({}),
  "page": zod.object({
  "id": zod.string().uuid(),
  "kind": zod.enum(['Composable', 'System']),
  "systemType": zod.union([zod.literal('Contact'),zod.literal('NotFound'),zod.literal('ProductListing'),zod.literal('ProductDetail'),zod.literal('ProductSearch'),zod.literal('Booking'),zod.literal(null)]).nullable()
})
})


export const savePageDraftParams = zod.object({
  "shopId": zod.string().uuid(),
  "pageId": zod.string().uuid()
})

export const savePageDraftBody = zod.object({
  "tree": zod.unknown(),
  "version": zod.number()
})

export const savePageDraftResponse = zod.object({
  "version": zod.number(),
  "isDirty": zod.boolean(),
  "sanitized": zod.boolean()
})


export const getThemeParams = zod.object({
  "shopId": zod.string().uuid()
})

export const getThemeResponse = zod.object({
  "presetKey": zod.string().nullable(),
  "tokens": zod.unknown(),
  "updatedAt": zod.string().datetime({})
})


export const updateThemeParams = zod.object({
  "shopId": zod.string().uuid()
})

export const updateThemeBody = zod.object({
  "presetKey": zod.string().nullish(),
  "tokens": zod.unknown()
})

export const updateThemeResponse = zod.object({
  "presetKey": zod.string().nullable(),
  "tokens": zod.unknown(),
  "updatedAt": zod.string().datetime({})
})
