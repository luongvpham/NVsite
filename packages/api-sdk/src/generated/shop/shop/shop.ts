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
  CreateShopRequest,
  HttpValidationProblemDetails,
  ProblemDetails,
  ShopDto,
  ShopSummaryDto,
  UpdateShopRequest
} from '.././model';

import { customInstance } from '../../../mutator/axios-instance';




export const postShops = (
    createShopRequest: CreateShopRequest,
 signal?: AbortSignal
) => {
      
      
      return customInstance<ShopDto>(
      {url: `/shops`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: createShopRequest, signal
    },
      );
    }
  


export const getPostShopsMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postShops>>, TError,{data: CreateShopRequest}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof postShops>>, TError,{data: CreateShopRequest}, TContext> => {

const mutationKey = ['postShops'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof postShops>>, {data: CreateShopRequest}> = (props) => {
          const {data} = props ?? {};

          return  postShops(data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type PostShopsMutationResult = NonNullable<Awaited<ReturnType<typeof postShops>>>
    export type PostShopsMutationBody = CreateShopRequest
    export type PostShopsMutationError = ProblemDetails | HttpValidationProblemDetails

    export const usePostShops = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postShops>>, TError,{data: CreateShopRequest}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof postShops>>,
        TError,
        {data: CreateShopRequest},
        TContext
      > => {

      const mutationOptions = getPostShopsMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getShops = (
    
 signal?: AbortSignal
) => {
      
      
      return customInstance<ShopSummaryDto[]>(
      {url: `/shops`, method: 'GET', signal
    },
      );
    }
  



export const getGetShopsQueryKey = () => {
    return [
    `/shops`
    ] as const;
    }

    
export const getGetShopsQueryOptions = <TData = Awaited<ReturnType<typeof getShops>>, TError = ProblemDetails>( options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShops>>, TError, TData>>, }
) => {

const {query: queryOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetShopsQueryKey();

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getShops>>> = ({ signal }) => getShops(signal);

      

      

   return  { queryKey, queryFn, ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getShops>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetShopsQueryResult = NonNullable<Awaited<ReturnType<typeof getShops>>>
export type GetShopsQueryError = ProblemDetails


export function useGetShops<TData = Awaited<ReturnType<typeof getShops>>, TError = ProblemDetails>(
  options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShops>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShops>>,
          TError,
          Awaited<ReturnType<typeof getShops>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShops<TData = Awaited<ReturnType<typeof getShops>>, TError = ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShops>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShops>>,
          TError,
          Awaited<ReturnType<typeof getShops>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShops<TData = Awaited<ReturnType<typeof getShops>>, TError = ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShops>>, TError, TData>>, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetShops<TData = Awaited<ReturnType<typeof getShops>>, TError = ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShops>>, TError, TData>>, }
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetShopsQueryOptions(options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const getShopsShopId = (
    shopId: string,
 signal?: AbortSignal
) => {
      
      
      return customInstance<ShopDto>(
      {url: `/shops/${shopId}`, method: 'GET', signal
    },
      );
    }
  



export const getGetShopsShopIdQueryKey = (shopId?: string,) => {
    return [
    `/shops/${shopId}`
    ] as const;
    }

    
export const getGetShopsShopIdQueryOptions = <TData = Awaited<ReturnType<typeof getShopsShopId>>, TError = ProblemDetails>(shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopId>>, TError, TData>>, }
) => {

const {query: queryOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetShopsShopIdQueryKey(shopId);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getShopsShopId>>> = ({ signal }) => getShopsShopId(shopId, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getShopsShopId>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetShopsShopIdQueryResult = NonNullable<Awaited<ReturnType<typeof getShopsShopId>>>
export type GetShopsShopIdQueryError = ProblemDetails


export function useGetShopsShopId<TData = Awaited<ReturnType<typeof getShopsShopId>>, TError = ProblemDetails>(
 shopId: string, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopId>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShopsShopId>>,
          TError,
          Awaited<ReturnType<typeof getShopsShopId>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShopsShopId<TData = Awaited<ReturnType<typeof getShopsShopId>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopId>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShopsShopId>>,
          TError,
          Awaited<ReturnType<typeof getShopsShopId>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShopsShopId<TData = Awaited<ReturnType<typeof getShopsShopId>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopId>>, TError, TData>>, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetShopsShopId<TData = Awaited<ReturnType<typeof getShopsShopId>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopId>>, TError, TData>>, }
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetShopsShopIdQueryOptions(shopId,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const patchShopsShopId = (
    shopId: string,
    updateShopRequest: UpdateShopRequest,
 ) => {
      
      
      return customInstance<ShopDto>(
      {url: `/shops/${shopId}`, method: 'PATCH',
      headers: {'Content-Type': 'application/json', },
      data: updateShopRequest
    },
      );
    }
  


export const getPatchShopsShopIdMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof patchShopsShopId>>, TError,{shopId: string;data: UpdateShopRequest}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof patchShopsShopId>>, TError,{shopId: string;data: UpdateShopRequest}, TContext> => {

const mutationKey = ['patchShopsShopId'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof patchShopsShopId>>, {shopId: string;data: UpdateShopRequest}> = (props) => {
          const {shopId,data} = props ?? {};

          return  patchShopsShopId(shopId,data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type PatchShopsShopIdMutationResult = NonNullable<Awaited<ReturnType<typeof patchShopsShopId>>>
    export type PatchShopsShopIdMutationBody = UpdateShopRequest
    export type PatchShopsShopIdMutationError = ProblemDetails | HttpValidationProblemDetails

    export const usePatchShopsShopId = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof patchShopsShopId>>, TError,{shopId: string;data: UpdateShopRequest}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof patchShopsShopId>>,
        TError,
        {shopId: string;data: UpdateShopRequest},
        TContext
      > => {

      const mutationOptions = getPatchShopsShopIdMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    