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

import type {
  AuthTokenResult,
  MeDto,
  RegisterResult,
  VerifyEmailResult
} from '.././model';


export const getPostAuthRegisterResponseMock = (overrideResponse: Partial< RegisterResult > = {}): RegisterResult => ({verificationEmailSent: faker.datatype.boolean(), ...overrideResponse})

export const getGetAuthVerifyEmailResponseMock = (overrideResponse: Partial< VerifyEmailResult > = {}): VerifyEmailResult => ({userId: faker.string.uuid(), shopMembershipCreated: faker.datatype.boolean(), ...overrideResponse})

export const getPostAuthLoginResponseMock = (overrideResponse: Partial< AuthTokenResult > = {}): AuthTokenResult => ({accessToken: faker.string.alpha({length: {min: 10, max: 20}}), accessTokenExpiresAt: `${faker.date.past().toISOString().split('.')[0]}Z`, refreshToken: faker.string.alpha({length: {min: 10, max: 20}}), refreshTokenExpiresAt: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})

export const getPostAuthRefreshTokenResponseMock = (overrideResponse: Partial< AuthTokenResult > = {}): AuthTokenResult => ({accessToken: faker.string.alpha({length: {min: 10, max: 20}}), accessTokenExpiresAt: `${faker.date.past().toISOString().split('.')[0]}Z`, refreshToken: faker.string.alpha({length: {min: 10, max: 20}}), refreshTokenExpiresAt: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})

export const getGetAuthMeResponseMock = (overrideResponse: Partial< MeDto > = {}): MeDto => ({userId: faker.string.uuid(), email: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), fullName: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), avatarUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), phone: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), audience: faker.string.alpha({length: {min: 10, max: 20}}), ...overrideResponse})


export const getPostAuthRegisterMockHandler = (overrideResponse?: RegisterResult | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<RegisterResult> | RegisterResult), options?: RequestHandlerOptions) => {
  return http.post('*/auth/register', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getPostAuthRegisterResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getGetAuthVerifyEmailMockHandler = (overrideResponse?: VerifyEmailResult | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<VerifyEmailResult> | VerifyEmailResult), options?: RequestHandlerOptions) => {
  return http.get('*/auth/verify-email', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetAuthVerifyEmailResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getPostAuthLoginMockHandler = (overrideResponse?: AuthTokenResult | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<AuthTokenResult> | AuthTokenResult), options?: RequestHandlerOptions) => {
  return http.post('*/auth/login', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getPostAuthLoginResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getPostAuthRefreshTokenMockHandler = (overrideResponse?: AuthTokenResult | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<AuthTokenResult> | AuthTokenResult), options?: RequestHandlerOptions) => {
  return http.post('*/auth/refresh-token', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getPostAuthRefreshTokenResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getPostAuthForgotPasswordMockHandler = (overrideResponse?: void | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<void> | void), options?: RequestHandlerOptions) => {
  return http.post('*/auth/forgot-password', async (info) => {await delay(1000);
  if (typeof overrideResponse === 'function') {await overrideResponse(info); }
    return new HttpResponse(null,
      { status: 204,
        
      })
  }, options)
}

export const getPostAuthResetPasswordMockHandler = (overrideResponse?: void | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<void> | void), options?: RequestHandlerOptions) => {
  return http.post('*/auth/reset-password', async (info) => {await delay(1000);
  if (typeof overrideResponse === 'function') {await overrideResponse(info); }
    return new HttpResponse(null,
      { status: 204,
        
      })
  }, options)
}

export const getGetAuthMeMockHandler = (overrideResponse?: MeDto | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<MeDto> | MeDto), options?: RequestHandlerOptions) => {
  return http.get('*/auth/me', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetAuthMeResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getPostAuthMeChangePasswordMockHandler = (overrideResponse?: void | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<void> | void), options?: RequestHandlerOptions) => {
  return http.post('*/auth/me/change-password', async (info) => {await delay(1000);
  if (typeof overrideResponse === 'function') {await overrideResponse(info); }
    return new HttpResponse(null,
      { status: 204,
        
      })
  }, options)
}
export const getIdentityMock = () => [
  getPostAuthRegisterMockHandler(),
  getGetAuthVerifyEmailMockHandler(),
  getPostAuthLoginMockHandler(),
  getPostAuthRefreshTokenMockHandler(),
  getPostAuthForgotPasswordMockHandler(),
  getPostAuthResetPasswordMockHandler(),
  getGetAuthMeMockHandler(),
  getPostAuthMeChangePasswordMockHandler()
]
