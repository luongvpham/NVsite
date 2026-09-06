/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import * as zod from 'zod';

export const listSamplesQueryParams = zod.object({
  "page": zod.number().optional(),
  "pageSize": zod.number().optional()
})

export const listSamplesResponse = zod.object({
  "items": zod.array(zod.object({
  "id": zod.string().uuid(),
  "name": zod.string(),
  "status": zod.enum(['Draft', 'Active', 'Archived']),
  "createdAtUtc": zod.string().datetime({})
})),
  "total": zod.number(),
  "page": zod.number(),
  "pageSize": zod.number()
})


export const createSampleBody = zod.object({
  "shopId": zod.string().uuid(),
  "name": zod.string(),
  "status": zod.enum(['Draft', 'Active', 'Archived'])
})


export const getSampleByIdParams = zod.object({
  "id": zod.string().uuid()
})

export const getSampleByIdResponse = zod.object({
  "id": zod.string().uuid(),
  "shopId": zod.string().uuid(),
  "name": zod.string(),
  "status": zod.enum(['Draft', 'Active', 'Archived']),
  "createdAtUtc": zod.string().datetime({})
})


export const getSampleByIdForShopParams = zod.object({
  "shopId": zod.string().uuid(),
  "id": zod.string().uuid()
})

export const getSampleByIdForShopResponse = zod.object({
  "id": zod.string().uuid(),
  "shopId": zod.string().uuid(),
  "name": zod.string(),
  "status": zod.enum(['Draft', 'Active', 'Archived']),
  "createdAtUtc": zod.string().datetime({})
})
