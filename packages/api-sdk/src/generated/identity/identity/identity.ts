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
  HttpValidationProblemDetails,
  LoginRequest,
  MeDto,
  ProblemDetails,
  RefreshTokenRequest,
  RegisterRequest,
  RegisterResult,
  ResetPasswordRequest,
  VerifyEmailParams,
  VerifyEmailResult
} from '.././model';

import { customInstance } from '../../../mutator/axios-instance';


type SecondParameter<T extends (...args: never) => unknown> = Parameters<T>[1];



export const register = (
    registerRequest: RegisterRequest,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<RegisterResult>(
      {url: `/api/auth/register`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: registerRequest, signal
    },
      options);
    }
  


export const getRegisterMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof register>>, TError,{data: RegisterRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof register>>, TError,{data: RegisterRequest}, TContext> => {

const mutationKey = ['register'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof register>>, {data: RegisterRequest}> = (props) => {
          const {data} = props ?? {};

          return  register(data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type RegisterMutationResult = NonNullable<Awaited<ReturnType<typeof register>>>
    export type RegisterMutationBody = RegisterRequest
    export type RegisterMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useRegister = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof register>>, TError,{data: RegisterRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof register>>,
        TError,
        {data: RegisterRequest},
        TContext
      > => {

      const mutationOptions = getRegisterMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const verifyEmail = (
    params: VerifyEmailParams,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<VerifyEmailResult>(
      {url: `/api/auth/verify-email`, method: 'GET',
        params, signal
    },
      options);
    }
  



export const getVerifyEmailQueryKey = (params?: VerifyEmailParams,) => {
    return [
    `/api/auth/verify-email`, ...(params ? [params]: [])
    ] as const;
    }

    
export const getVerifyEmailQueryOptions = <TData = Awaited<ReturnType<typeof verifyEmail>>, TError = ProblemDetails | HttpValidationProblemDetails>(params: VerifyEmailParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof verifyEmail>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getVerifyEmailQueryKey(params);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof verifyEmail>>> = ({ signal }) => verifyEmail(params, requestOptions, signal);

      

      

   return  { queryKey, queryFn, ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof verifyEmail>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type VerifyEmailQueryResult = NonNullable<Awaited<ReturnType<typeof verifyEmail>>>
export type VerifyEmailQueryError = ProblemDetails | HttpValidationProblemDetails


export function useVerifyEmail<TData = Awaited<ReturnType<typeof verifyEmail>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 params: VerifyEmailParams, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof verifyEmail>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof verifyEmail>>,
          TError,
          Awaited<ReturnType<typeof verifyEmail>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useVerifyEmail<TData = Awaited<ReturnType<typeof verifyEmail>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 params: VerifyEmailParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof verifyEmail>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof verifyEmail>>,
          TError,
          Awaited<ReturnType<typeof verifyEmail>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useVerifyEmail<TData = Awaited<ReturnType<typeof verifyEmail>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 params: VerifyEmailParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof verifyEmail>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useVerifyEmail<TData = Awaited<ReturnType<typeof verifyEmail>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 params: VerifyEmailParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof verifyEmail>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getVerifyEmailQueryOptions(params,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const login = (
    loginRequest: LoginRequest,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<AuthTokenResult>(
      {url: `/api/auth/login`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: loginRequest, signal
    },
      options);
    }
  


export const getLoginMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof login>>, TError,{data: LoginRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof login>>, TError,{data: LoginRequest}, TContext> => {

const mutationKey = ['login'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof login>>, {data: LoginRequest}> = (props) => {
          const {data} = props ?? {};

          return  login(data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type LoginMutationResult = NonNullable<Awaited<ReturnType<typeof login>>>
    export type LoginMutationBody = LoginRequest
    export type LoginMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useLogin = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof login>>, TError,{data: LoginRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof login>>,
        TError,
        {data: LoginRequest},
        TContext
      > => {

      const mutationOptions = getLoginMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const refreshToken = (
    refreshTokenRequest: RefreshTokenRequest,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<AuthTokenResult>(
      {url: `/api/auth/refresh-token`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: refreshTokenRequest, signal
    },
      options);
    }
  


export const getRefreshTokenMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof refreshToken>>, TError,{data: RefreshTokenRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof refreshToken>>, TError,{data: RefreshTokenRequest}, TContext> => {

const mutationKey = ['refreshToken'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof refreshToken>>, {data: RefreshTokenRequest}> = (props) => {
          const {data} = props ?? {};

          return  refreshToken(data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type RefreshTokenMutationResult = NonNullable<Awaited<ReturnType<typeof refreshToken>>>
    export type RefreshTokenMutationBody = RefreshTokenRequest
    export type RefreshTokenMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useRefreshToken = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof refreshToken>>, TError,{data: RefreshTokenRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof refreshToken>>,
        TError,
        {data: RefreshTokenRequest},
        TContext
      > => {

      const mutationOptions = getRefreshTokenMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const forgotPassword = (
    forgotPasswordRequest: ForgotPasswordRequest,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<void>(
      {url: `/api/auth/forgot-password`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: forgotPasswordRequest, signal
    },
      options);
    }
  


export const getForgotPasswordMutationOptions = <TError = HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof forgotPassword>>, TError,{data: ForgotPasswordRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof forgotPassword>>, TError,{data: ForgotPasswordRequest}, TContext> => {

const mutationKey = ['forgotPassword'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof forgotPassword>>, {data: ForgotPasswordRequest}> = (props) => {
          const {data} = props ?? {};

          return  forgotPassword(data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type ForgotPasswordMutationResult = NonNullable<Awaited<ReturnType<typeof forgotPassword>>>
    export type ForgotPasswordMutationBody = ForgotPasswordRequest
    export type ForgotPasswordMutationError = HttpValidationProblemDetails

    export const useForgotPassword = <TError = HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof forgotPassword>>, TError,{data: ForgotPasswordRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof forgotPassword>>,
        TError,
        {data: ForgotPasswordRequest},
        TContext
      > => {

      const mutationOptions = getForgotPasswordMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const resetPassword = (
    resetPasswordRequest: ResetPasswordRequest,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<void>(
      {url: `/api/auth/reset-password`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: resetPasswordRequest, signal
    },
      options);
    }
  


export const getResetPasswordMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof resetPassword>>, TError,{data: ResetPasswordRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof resetPassword>>, TError,{data: ResetPasswordRequest}, TContext> => {

const mutationKey = ['resetPassword'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof resetPassword>>, {data: ResetPasswordRequest}> = (props) => {
          const {data} = props ?? {};

          return  resetPassword(data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type ResetPasswordMutationResult = NonNullable<Awaited<ReturnType<typeof resetPassword>>>
    export type ResetPasswordMutationBody = ResetPasswordRequest
    export type ResetPasswordMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useResetPassword = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof resetPassword>>, TError,{data: ResetPasswordRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof resetPassword>>,
        TError,
        {data: ResetPasswordRequest},
        TContext
      > => {

      const mutationOptions = getResetPasswordMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getMe = (
    
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<MeDto>(
      {url: `/api/auth/me`, method: 'GET', signal
    },
      options);
    }
  



export const getGetMeQueryKey = () => {
    return [
    `/api/auth/me`
    ] as const;
    }

    
export const getGetMeQueryOptions = <TData = Awaited<ReturnType<typeof getMe>>, TError = ProblemDetails>( options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getMe>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetMeQueryKey();

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getMe>>> = ({ signal }) => getMe(requestOptions, signal);

      

      

   return  { queryKey, queryFn, ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getMe>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetMeQueryResult = NonNullable<Awaited<ReturnType<typeof getMe>>>
export type GetMeQueryError = ProblemDetails


export function useGetMe<TData = Awaited<ReturnType<typeof getMe>>, TError = ProblemDetails>(
  options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getMe>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getMe>>,
          TError,
          Awaited<ReturnType<typeof getMe>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetMe<TData = Awaited<ReturnType<typeof getMe>>, TError = ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getMe>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getMe>>,
          TError,
          Awaited<ReturnType<typeof getMe>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetMe<TData = Awaited<ReturnType<typeof getMe>>, TError = ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getMe>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetMe<TData = Awaited<ReturnType<typeof getMe>>, TError = ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getMe>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetMeQueryOptions(options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const changePassword = (
    changePasswordRequest: ChangePasswordRequest,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<void>(
      {url: `/api/auth/me/change-password`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: changePasswordRequest, signal
    },
      options);
    }
  


export const getChangePasswordMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof changePassword>>, TError,{data: ChangePasswordRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof changePassword>>, TError,{data: ChangePasswordRequest}, TContext> => {

const mutationKey = ['changePassword'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof changePassword>>, {data: ChangePasswordRequest}> = (props) => {
          const {data} = props ?? {};

          return  changePassword(data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type ChangePasswordMutationResult = NonNullable<Awaited<ReturnType<typeof changePassword>>>
    export type ChangePasswordMutationBody = ChangePasswordRequest
    export type ChangePasswordMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useChangePassword = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof changePassword>>, TError,{data: ChangePasswordRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof changePassword>>,
        TError,
        {data: ChangePasswordRequest},
        TContext
      > => {

      const mutationOptions = getChangePasswordMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    