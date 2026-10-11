/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import * as zod from 'zod';

export const registerBody = zod.object({
  "email": zod.string(),
  "password": zod.string(),
  "fullName": zod.string().nullable()
})

export const registerResponse = zod.object({
  "verificationEmailSent": zod.boolean()
})


export const verifyEmailQueryParams = zod.object({
  "token": zod.string()
})

export const verifyEmailResponse = zod.object({
  "userId": zod.string().uuid(),
  "shopMembershipCreated": zod.boolean()
})


export const loginBody = zod.object({
  "email": zod.string(),
  "password": zod.string()
})

export const loginResponse = zod.object({
  "accessToken": zod.string(),
  "accessTokenExpiresAt": zod.string().datetime({}),
  "refreshToken": zod.string(),
  "refreshTokenExpiresAt": zod.string().datetime({})
})


export const refreshTokenBody = zod.object({
  "refreshToken": zod.string()
})

export const refreshTokenResponse = zod.object({
  "accessToken": zod.string(),
  "accessTokenExpiresAt": zod.string().datetime({}),
  "refreshToken": zod.string(),
  "refreshTokenExpiresAt": zod.string().datetime({})
})


export const forgotPasswordBody = zod.object({
  "email": zod.string()
})


export const resetPasswordBody = zod.object({
  "token": zod.string(),
  "newPassword": zod.string()
})


export const getMeResponse = zod.object({
  "userId": zod.string().uuid(),
  "email": zod.string().nullable(),
  "fullName": zod.string().nullable(),
  "avatarUrl": zod.string().nullable(),
  "phone": zod.string().nullable(),
  "audience": zod.string()
})


export const changePasswordBody = zod.object({
  "currentPassword": zod.string(),
  "newPassword": zod.string()
})
