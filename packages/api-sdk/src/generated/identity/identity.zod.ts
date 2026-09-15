/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import * as zod from 'zod';

export const postAuthRegisterBody = zod.object({
  "email": zod.string(),
  "password": zod.string(),
  "fullName": zod.string().nullable()
})

export const postAuthRegisterResponse = zod.object({
  "verificationEmailSent": zod.boolean()
})


export const getAuthVerifyEmailQueryParams = zod.object({
  "token": zod.string()
})

export const getAuthVerifyEmailResponse = zod.object({
  "userId": zod.string().uuid(),
  "shopMembershipCreated": zod.boolean()
})


export const postAuthLoginBody = zod.object({
  "email": zod.string(),
  "password": zod.string()
})

export const postAuthLoginResponse = zod.object({
  "accessToken": zod.string(),
  "accessTokenExpiresAt": zod.string().datetime({}),
  "refreshToken": zod.string(),
  "refreshTokenExpiresAt": zod.string().datetime({})
})


export const postAuthRefreshTokenBody = zod.object({
  "refreshToken": zod.string()
})

export const postAuthRefreshTokenResponse = zod.object({
  "accessToken": zod.string(),
  "accessTokenExpiresAt": zod.string().datetime({}),
  "refreshToken": zod.string(),
  "refreshTokenExpiresAt": zod.string().datetime({})
})


export const postAuthForgotPasswordBody = zod.object({
  "email": zod.string()
})


export const postAuthResetPasswordBody = zod.object({
  "token": zod.string(),
  "newPassword": zod.string()
})


export const getAuthMeResponse = zod.object({
  "userId": zod.string().uuid(),
  "email": zod.string().nullable(),
  "fullName": zod.string().nullable(),
  "avatarUrl": zod.string().nullable(),
  "phone": zod.string().nullable(),
  "audience": zod.string()
})


export const postAuthMeChangePasswordBody = zod.object({
  "currentPassword": zod.string(),
  "newPassword": zod.string()
})
