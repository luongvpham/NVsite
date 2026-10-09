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


export const getRegisterResponseMock = (overrideResponse: Partial< RegisterResult > = {}): RegisterResult => ({verificationEmailSent: faker.datatype.boolean(), ...overrideResponse})

export const getVerifyEmailResponseMock = (overrideResponse: Partial< VerifyEmailResult > = {}): VerifyEmailResult => ({userId: faker.string.uuid(), shopMembershipCreated: faker.datatype.boolean(), ...overrideResponse})

export const getLoginResponseMock = (overrideResponse: Partial< AuthTokenResult > = {}): AuthTokenResult => ({accessToken: faker.string.alpha({length: {min: 10, max: 20}}), accessTokenExpiresAt: `${faker.date.past().toISOString().split('.')[0]}Z`, refreshToken: faker.string.alpha({length: {min: 10, max: 20}}), refreshTokenExpiresAt: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})

export const getRefreshTokenResponseMock = (overrideResponse: Partial< AuthTokenResult > = {}): AuthTokenResult => ({accessToken: faker.string.alpha({length: {min: 10, max: 20}}), accessTokenExpiresAt: `${faker.date.past().toISOString().split('.')[0]}Z`, refreshToken: faker.string.alpha({length: {min: 10, max: 20}}), refreshTokenExpiresAt: `${faker.date.past().toISOString().split('.')[0]}Z`, ...overrideResponse})

export const getGetMeResponseMock = (overrideResponse: Partial< MeDto > = {}): MeDto => ({userId: faker.string.uuid(), email: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), fullName: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), avatarUrl: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), phone: faker.helpers.arrayElement([faker.string.alpha({length: {min: 10, max: 20}}), null]), audience: faker.string.alpha({length: {min: 10, max: 20}}), ...overrideResponse})


export const getRegisterMockHandler = (overrideResponse?: RegisterResult | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<RegisterResult> | RegisterResult), options?: RequestHandlerOptions) => {
  return http.post('*/api/auth/register', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getRegisterResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getVerifyEmailMockHandler = (overrideResponse?: VerifyEmailResult | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<VerifyEmailResult> | VerifyEmailResult), options?: RequestHandlerOptions) => {
  return http.get('*/api/auth/verify-email', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getVerifyEmailResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getLoginMockHandler = (overrideResponse?: AuthTokenResult | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<AuthTokenResult> | AuthTokenResult), options?: RequestHandlerOptions) => {
  return http.post('*/api/auth/login', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getLoginResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getRefreshTokenMockHandler = (overrideResponse?: AuthTokenResult | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<AuthTokenResult> | AuthTokenResult), options?: RequestHandlerOptions) => {
  return http.post('*/api/auth/refresh-token', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getRefreshTokenResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getForgotPasswordMockHandler = (overrideResponse?: void | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<void> | void), options?: RequestHandlerOptions) => {
  return http.post('*/api/auth/forgot-password', async (info) => {await delay(1000);
  if (typeof overrideResponse === 'function') {await overrideResponse(info); }
    return new HttpResponse(null,
      { status: 204,
        
      })
  }, options)
}

export const getResetPasswordMockHandler = (overrideResponse?: void | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<void> | void), options?: RequestHandlerOptions) => {
  return http.post('*/api/auth/reset-password', async (info) => {await delay(1000);
  if (typeof overrideResponse === 'function') {await overrideResponse(info); }
    return new HttpResponse(null,
      { status: 204,
        
      })
  }, options)
}

export const getGetMeMockHandler = (overrideResponse?: MeDto | ((info: Parameters<Parameters<typeof http.get>[1]>[0]) => Promise<MeDto> | MeDto), options?: RequestHandlerOptions) => {
  return http.get('*/api/auth/me', async (info) => {await delay(1000);
  
    return new HttpResponse(JSON.stringify(overrideResponse !== undefined
    ? (typeof overrideResponse === "function" ? await overrideResponse(info) : overrideResponse)
    : getGetMeResponseMock()),
      { status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
  }, options)
}

export const getChangePasswordMockHandler = (overrideResponse?: void | ((info: Parameters<Parameters<typeof http.post>[1]>[0]) => Promise<void> | void), options?: RequestHandlerOptions) => {
  return http.post('*/api/auth/me/change-password', async (info) => {await delay(1000);
  if (typeof overrideResponse === 'function') {await overrideResponse(info); }
    return new HttpResponse(null,
      { status: 204,
        
      })
  }, options)
}
export const getIdentityMock = () => [
  getRegisterMockHandler(),
  getVerifyEmailMockHandler(),
  getLoginMockHandler(),
  getRefreshTokenMockHandler(),
  getForgotPasswordMockHandler(),
  getResetPasswordMockHandler(),
  getGetMeMockHandler(),
  getChangePasswordMockHandler()
]
