/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import * as zod from 'zod';

export const createShopBody = zod.object({
  "name": zod.string(),
  "slug": zod.string(),
  "kind": zod.enum(['Hosted', 'ExternalOnly']),
  "externalUrl": zod.string().nullable()
})

export const createShopResponse = zod.object({
  "id": zod.string().uuid(),
  "name": zod.string(),
  "slug": zod.string(),
  "kind": zod.enum(['Hosted', 'ExternalOnly']),
  "externalUrl": zod.string().nullable(),
  "status": zod.enum(['Draft', 'Active', 'Suspended', 'Closed']),
  "logoId": zod.string().uuid().nullable(),
  "logoUrl": zod.string().nullable()
})


export const listShopsResponseItem = zod.object({
  "id": zod.string().uuid(),
  "name": zod.string(),
  "slug": zod.string(),
  "kind": zod.enum(['Hosted', 'ExternalOnly']),
  "status": zod.enum(['Draft', 'Active', 'Suspended', 'Closed']),
  "roleCode": zod.string(),
  "logoUrl": zod.string().nullable()
})
export const listShopsResponse = zod.array(listShopsResponseItem)


export const getShopParams = zod.object({
  "shopId": zod.string().uuid()
})

export const getShopResponse = zod.object({
  "id": zod.string().uuid(),
  "name": zod.string(),
  "slug": zod.string(),
  "kind": zod.enum(['Hosted', 'ExternalOnly']),
  "externalUrl": zod.string().nullable(),
  "status": zod.enum(['Draft', 'Active', 'Suspended', 'Closed']),
  "logoId": zod.string().uuid().nullable(),
  "logoUrl": zod.string().nullable()
})


export const updateShopParams = zod.object({
  "shopId": zod.string().uuid()
})

export const updateShopBody = zod.object({
  "name": zod.string(),
  "slug": zod.string(),
  "kind": zod.enum(['Hosted', 'ExternalOnly']),
  "externalUrl": zod.string().nullable(),
  "status": zod.enum(['Draft', 'Active', 'Suspended', 'Closed'])
})

export const updateShopResponse = zod.object({
  "id": zod.string().uuid(),
  "name": zod.string(),
  "slug": zod.string(),
  "kind": zod.enum(['Hosted', 'ExternalOnly']),
  "externalUrl": zod.string().nullable(),
  "status": zod.enum(['Draft', 'Active', 'Suspended', 'Closed']),
  "logoId": zod.string().uuid().nullable(),
  "logoUrl": zod.string().nullable()
})
