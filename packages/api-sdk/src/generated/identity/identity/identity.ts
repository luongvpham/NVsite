/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import {
  useMutation,
  useQuery
} from '@tanstack/react-query';
import type {
  DataTag,
  DefinedInitialDataOptions,
  DefinedUseQueryResult,
  MutationFunction,
  QueryClient,
  QueryFunction,
  QueryKey,
  UndefinedInitialDataOptions,
  UseMutationOptions,
  UseMutationResult,
  UseQueryOptions,
  UseQueryResult
} from '@tanstack/react-query';

import type {
  AuthTokenResult,
  ChangePasswordRequest,
  ForgotPasswordRequest,
  GetAuthVerifyEmailParams,
  HttpValidationProblemDetails,
  LoginRequest,
  MeDto,
  ProblemDetails,
  RefreshTokenRequest,
  RegisterRequest,
  RegisterResult,
  ResetPasswordRequest,
  VerifyEmailResult
} from '.././model';

import { customInstance } from '../../../mutator/axios-instance';




export const postAuthRegister = (
    registerRequest: RegisterRequest,
 signal?: AbortSignal
) => {
      
      
      return customInstance<RegisterResult>(
      {url: `/auth/register`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: registerRequest, signal
    },
      );
    }
  


export const getPostAuthRegisterMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postAuthRegister>>, TError,{data: RegisterRequest}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof postAuthRegister>>, TError,{data: RegisterRequest}, TContext> => {

const mutationKey = ['postAuthRegister'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof postAuthRegister>>, {data: RegisterRequest}> = (props) => {
          const {data} = props ?? {};

          return  postAuthRegister(data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type PostAuthRegisterMutationResult = NonNullable<Awaited<ReturnType<typeof postAuthRegister>>>
    export type PostAuthRegisterMutationBody = RegisterRequest
    export type PostAuthRegisterMutationError = ProblemDetails | HttpValidationProblemDetails

    export const usePostAuthRegister = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postAuthRegister>>, TError,{data: RegisterRequest}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof postAuthRegister>>,
        TError,
        {data: RegisterRequest},
        TContext
      > => {

      const mutationOptions = getPostAuthRegisterMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getAuthVerifyEmail = (
    params: GetAuthVerifyEmailParams,
 signal?: AbortSignal
) => {
      
      
      return customInstance<VerifyEmailResult>(
      {url: `/auth/verify-email`, method: 'GET',
        params, signal
    },
      );
    }
  



export const getGetAuthVerifyEmailQueryKey = (params?: GetAuthVerifyEmailParams,) => {
    return [
    `/auth/verify-email`, ...(params ? [params]: [])
    ] as const;
    }

    
export const getGetAuthVerifyEmailQueryOptions = <TData = Awaited<ReturnType<typeof getAuthVerifyEmail>>, TError = ProblemDetails | HttpValidationProblemDetails>(params: GetAuthVerifyEmailParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAuthVerifyEmail>>, TError, TData>>, }
) => {

const {query: queryOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetAuthVerifyEmailQueryKey(params);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getAuthVerifyEmail>>> = ({ signal }) => getAuthVerifyEmail(params, signal);

      

      

   return  { queryKey, queryFn, ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getAuthVerifyEmail>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetAuthVerifyEmailQueryResult = NonNullable<Awaited<ReturnType<typeof getAuthVerifyEmail>>>
export type GetAuthVerifyEmailQueryError = ProblemDetails | HttpValidationProblemDetails


export function useGetAuthVerifyEmail<TData = Awaited<ReturnType<typeof getAuthVerifyEmail>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 params: GetAuthVerifyEmailParams, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAuthVerifyEmail>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getAuthVerifyEmail>>,
          TError,
          Awaited<ReturnType<typeof getAuthVerifyEmail>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetAuthVerifyEmail<TData = Awaited<ReturnType<typeof getAuthVerifyEmail>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 params: GetAuthVerifyEmailParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAuthVerifyEmail>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getAuthVerifyEmail>>,
          TError,
          Awaited<ReturnType<typeof getAuthVerifyEmail>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetAuthVerifyEmail<TData = Awaited<ReturnType<typeof getAuthVerifyEmail>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 params: GetAuthVerifyEmailParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAuthVerifyEmail>>, TError, TData>>, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetAuthVerifyEmail<TData = Awaited<ReturnType<typeof getAuthVerifyEmail>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 params: GetAuthVerifyEmailParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAuthVerifyEmail>>, TError, TData>>, }
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetAuthVerifyEmailQueryOptions(params,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const postAuthLogin = (
    loginRequest: LoginRequest,
 signal?: AbortSignal
) => {
      
      
      return customInstance<AuthTokenResult>(
      {url: `/auth/login`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: loginRequest, signal
    },
      );
    }
  


export const getPostAuthLoginMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postAuthLogin>>, TError,{data: LoginRequest}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof postAuthLogin>>, TError,{data: LoginRequest}, TContext> => {

const mutationKey = ['postAuthLogin'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof postAuthLogin>>, {data: LoginRequest}> = (props) => {
          const {data} = props ?? {};

          return  postAuthLogin(data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type PostAuthLoginMutationResult = NonNullable<Awaited<ReturnType<typeof postAuthLogin>>>
    export type PostAuthLoginMutationBody = LoginRequest
    export type PostAuthLoginMutationError = ProblemDetails | HttpValidationProblemDetails

    export const usePostAuthLogin = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postAuthLogin>>, TError,{data: LoginRequest}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof postAuthLogin>>,
        TError,
        {data: LoginRequest},
        TContext
      > => {

      const mutationOptions = getPostAuthLoginMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const postAuthRefreshToken = (
    refreshTokenRequest: RefreshTokenRequest,
 signal?: AbortSignal
) => {
      
      
      return customInstance<AuthTokenResult>(
      {url: `/auth/refresh-token`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: refreshTokenRequest, signal
    },
      );
    }
  


export const getPostAuthRefreshTokenMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postAuthRefreshToken>>, TError,{data: RefreshTokenRequest}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof postAuthRefreshToken>>, TError,{data: RefreshTokenRequest}, TContext> => {

const mutationKey = ['postAuthRefreshToken'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof postAuthRefreshToken>>, {data: RefreshTokenRequest}> = (props) => {
          const {data} = props ?? {};

          return  postAuthRefreshToken(data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type PostAuthRefreshTokenMutationResult = NonNullable<Awaited<ReturnType<typeof postAuthRefreshToken>>>
    export type PostAuthRefreshTokenMutationBody = RefreshTokenRequest
    export type PostAuthRefreshTokenMutationError = ProblemDetails | HttpValidationProblemDetails

    export const usePostAuthRefreshToken = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postAuthRefreshToken>>, TError,{data: RefreshTokenRequest}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof postAuthRefreshToken>>,
        TError,
        {data: RefreshTokenRequest},
        TContext
      > => {

      const mutationOptions = getPostAuthRefreshTokenMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const postAuthForgotPassword = (
    forgotPasswordRequest: ForgotPasswordRequest,
 signal?: AbortSignal
) => {
      
      
      return customInstance<void>(
      {url: `/auth/forgot-password`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: forgotPasswordRequest, signal
    },
      );
    }
  


export const getPostAuthForgotPasswordMutationOptions = <TError = HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postAuthForgotPassword>>, TError,{data: ForgotPasswordRequest}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof postAuthForgotPassword>>, TError,{data: ForgotPasswordRequest}, TContext> => {

const mutationKey = ['postAuthForgotPassword'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof postAuthForgotPassword>>, {data: ForgotPasswordRequest}> = (props) => {
          const {data} = props ?? {};

          return  postAuthForgotPassword(data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type PostAuthForgotPasswordMutationResult = NonNullable<Awaited<ReturnType<typeof postAuthForgotPassword>>>
    export type PostAuthForgotPasswordMutationBody = ForgotPasswordRequest
    export type PostAuthForgotPasswordMutationError = HttpValidationProblemDetails

    export const usePostAuthForgotPassword = <TError = HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postAuthForgotPassword>>, TError,{data: ForgotPasswordRequest}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof postAuthForgotPassword>>,
        TError,
        {data: ForgotPasswordRequest},
        TContext
      > => {

      const mutationOptions = getPostAuthForgotPasswordMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const postAuthResetPassword = (
    resetPasswordRequest: ResetPasswordRequest,
 signal?: AbortSignal
) => {
      
      
      return customInstance<void>(
      {url: `/auth/reset-password`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: resetPasswordRequest, signal
    },
      );
    }
  


export const getPostAuthResetPasswordMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postAuthResetPassword>>, TError,{data: ResetPasswordRequest}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof postAuthResetPassword>>, TError,{data: ResetPasswordRequest}, TContext> => {

const mutationKey = ['postAuthResetPassword'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof postAuthResetPassword>>, {data: ResetPasswordRequest}> = (props) => {
          const {data} = props ?? {};

          return  postAuthResetPassword(data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type PostAuthResetPasswordMutationResult = NonNullable<Awaited<ReturnType<typeof postAuthResetPassword>>>
    export type PostAuthResetPasswordMutationBody = ResetPasswordRequest
    export type PostAuthResetPasswordMutationError = ProblemDetails | HttpValidationProblemDetails

    export const usePostAuthResetPassword = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postAuthResetPassword>>, TError,{data: ResetPasswordRequest}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof postAuthResetPassword>>,
        TError,
        {data: ResetPasswordRequest},
        TContext
      > => {

      const mutationOptions = getPostAuthResetPasswordMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getAuthMe = (
    
 signal?: AbortSignal
) => {
      
      
      return customInstance<MeDto>(
      {url: `/auth/me`, method: 'GET', signal
    },
      );
    }
  



export const getGetAuthMeQueryKey = () => {
    return [
    `/auth/me`
    ] as const;
    }

    
export const getGetAuthMeQueryOptions = <TData = Awaited<ReturnType<typeof getAuthMe>>, TError = ProblemDetails>( options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAuthMe>>, TError, TData>>, }
) => {

const {query: queryOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetAuthMeQueryKey();

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getAuthMe>>> = ({ signal }) => getAuthMe(signal);

      

      

   return  { queryKey, queryFn, ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getAuthMe>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetAuthMeQueryResult = NonNullable<Awaited<ReturnType<typeof getAuthMe>>>
export type GetAuthMeQueryError = ProblemDetails


export function useGetAuthMe<TData = Awaited<ReturnType<typeof getAuthMe>>, TError = ProblemDetails>(
  options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAuthMe>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getAuthMe>>,
          TError,
          Awaited<ReturnType<typeof getAuthMe>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetAuthMe<TData = Awaited<ReturnType<typeof getAuthMe>>, TError = ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAuthMe>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getAuthMe>>,
          TError,
          Awaited<ReturnType<typeof getAuthMe>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetAuthMe<TData = Awaited<ReturnType<typeof getAuthMe>>, TError = ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAuthMe>>, TError, TData>>, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetAuthMe<TData = Awaited<ReturnType<typeof getAuthMe>>, TError = ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAuthMe>>, TError, TData>>, }
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetAuthMeQueryOptions(options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const postAuthMeChangePassword = (
    changePasswordRequest: ChangePasswordRequest,
 signal?: AbortSignal
) => {
      
      
      return customInstance<void>(
      {url: `/auth/me/change-password`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: changePasswordRequest, signal
    },
      );
    }
  


export const getPostAuthMeChangePasswordMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postAuthMeChangePassword>>, TError,{data: ChangePasswordRequest}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof postAuthMeChangePassword>>, TError,{data: ChangePasswordRequest}, TContext> => {

const mutationKey = ['postAuthMeChangePassword'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof postAuthMeChangePassword>>, {data: ChangePasswordRequest}> = (props) => {
          const {data} = props ?? {};

          return  postAuthMeChangePassword(data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type PostAuthMeChangePasswordMutationResult = NonNullable<Awaited<ReturnType<typeof postAuthMeChangePassword>>>
    export type PostAuthMeChangePasswordMutationBody = ChangePasswordRequest
    export type PostAuthMeChangePasswordMutationError = ProblemDetails | HttpValidationProblemDetails

    export const usePostAuthMeChangePassword = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postAuthMeChangePassword>>, TError,{data: ChangePasswordRequest}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof postAuthMeChangePassword>>,
        TError,
        {data: ChangePasswordRequest},
        TContext
      > => {

      const mutationOptions = getPostAuthMeChangePasswordMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    