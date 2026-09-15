/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import * as zod from 'zod';

export const postShopsBody = zod.object({
  "name": zod.string(),
  "slug": zod.string(),
  "kind": zod.enum(['Hosted', 'ExternalOnly']),
  "externalUrl": zod.string().nullable()
})

export const postShopsResponse = zod.object({
  "id": zod.string().uuid(),
  "name": zod.string(),
  "slug": zod.string(),
  "kind": zod.enum(['Hosted', 'ExternalOnly']),
  "externalUrl": zod.string().nullable(),
  "status": zod.enum(['Draft', 'Active', 'Suspended', 'Closed'])
})


export const getShopsResponseItem = zod.object({
  "id": zod.string().uuid(),
  "name": zod.string(),
  "slug": zod.string(),
  "kind": zod.enum(['Hosted', 'ExternalOnly']),
  "status": zod.enum(['Draft', 'Active', 'Suspended', 'Closed']),
  "roleCode": zod.string()
})
export const getShopsResponse = zod.array(getShopsResponseItem)


export const getShopsShopIdParams = zod.object({
  "shopId": zod.string().uuid()
})

export const getShopsShopIdResponse = zod.object({
  "id": zod.string().uuid(),
  "name": zod.string(),
  "slug": zod.string(),
  "kind": zod.enum(['Hosted', 'ExternalOnly']),
  "externalUrl": zod.string().nullable(),
  "status": zod.enum(['Draft', 'Active', 'Suspended', 'Closed'])
})


export const patchShopsShopIdParams = zod.object({
  "shopId": zod.string().uuid()
})

export const patchShopsShopIdBody = zod.object({
  "name": zod.string(),
  "slug": zod.string(),
  "kind": zod.enum(['Hosted', 'ExternalOnly']),
  "externalUrl": zod.string().nullable(),
  "status": zod.enum(['Draft', 'Active', 'Suspended', 'Closed'])
})

export const patchShopsShopIdResponse = zod.object({
  "id": zod.string().uuid(),
  "name": zod.string(),
  "slug": zod.string(),
  "kind": zod.enum(['Hosted', 'ExternalOnly']),
  "externalUrl": zod.string().nullable(),
  "status": zod.enum(['Draft', 'Active', 'Suspended', 'Closed'])
})
