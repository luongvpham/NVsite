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


type SecondParameter<T extends (...args: never) => unknown> = Parameters<T>[1];



export const createShop = (
    createShopRequest: CreateShopRequest,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<ShopDto>(
      {url: `/api/shops`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: createShopRequest, signal
    },
      options);
    }
  


export const getCreateShopMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof createShop>>, TError,{data: CreateShopRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof createShop>>, TError,{data: CreateShopRequest}, TContext> => {

const mutationKey = ['createShop'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof createShop>>, {data: CreateShopRequest}> = (props) => {
          const {data} = props ?? {};

          return  createShop(data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type CreateShopMutationResult = NonNullable<Awaited<ReturnType<typeof createShop>>>
    export type CreateShopMutationBody = CreateShopRequest
    export type CreateShopMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useCreateShop = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof createShop>>, TError,{data: CreateShopRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof createShop>>,
        TError,
        {data: CreateShopRequest},
        TContext
      > => {

      const mutationOptions = getCreateShopMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const listShops = (
    
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<ShopSummaryDto[]>(
      {url: `/api/shops`, method: 'GET', signal
    },
      options);
    }
  



export const getListShopsQueryKey = () => {
    return [
    `/api/shops`
    ] as const;
    }

    
export const getListShopsQueryOptions = <TData = Awaited<ReturnType<typeof listShops>>, TError = ProblemDetails>( options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listShops>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getListShopsQueryKey();

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof listShops>>> = ({ signal }) => listShops(requestOptions, signal);

      

      

   return  { queryKey, queryFn, ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof listShops>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type ListShopsQueryResult = NonNullable<Awaited<ReturnType<typeof listShops>>>
export type ListShopsQueryError = ProblemDetails


export function useListShops<TData = Awaited<ReturnType<typeof listShops>>, TError = ProblemDetails>(
  options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof listShops>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof listShops>>,
          TError,
          Awaited<ReturnType<typeof listShops>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useListShops<TData = Awaited<ReturnType<typeof listShops>>, TError = ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listShops>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof listShops>>,
          TError,
          Awaited<ReturnType<typeof listShops>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useListShops<TData = Awaited<ReturnType<typeof listShops>>, TError = ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listShops>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useListShops<TData = Awaited<ReturnType<typeof listShops>>, TError = ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listShops>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getListShopsQueryOptions(options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const getShop = (
    shopId: string,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<ShopDto>(
      {url: `/api/shops/${shopId}`, method: 'GET', signal
    },
      options);
    }
  



export const getGetShopQueryKey = (shopId?: string,) => {
    return [
    `/api/shops/${shopId}`
    ] as const;
    }

    
export const getGetShopQueryOptions = <TData = Awaited<ReturnType<typeof getShop>>, TError = ProblemDetails>(shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShop>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetShopQueryKey(shopId);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getShop>>> = ({ signal }) => getShop(shopId, requestOptions, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getShop>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetShopQueryResult = NonNullable<Awaited<ReturnType<typeof getShop>>>
export type GetShopQueryError = ProblemDetails


export function useGetShop<TData = Awaited<ReturnType<typeof getShop>>, TError = ProblemDetails>(
 shopId: string, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShop>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShop>>,
          TError,
          Awaited<ReturnType<typeof getShop>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShop<TData = Awaited<ReturnType<typeof getShop>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShop>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShop>>,
          TError,
          Awaited<ReturnType<typeof getShop>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShop<TData = Awaited<ReturnType<typeof getShop>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShop>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetShop<TData = Awaited<ReturnType<typeof getShop>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShop>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetShopQueryOptions(shopId,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const updateShop = (
    shopId: string,
    updateShopRequest: UpdateShopRequest,
 options?: SecondParameter<typeof customInstance>,) => {
      
      
      return customInstance<ShopDto>(
      {url: `/api/shops/${shopId}`, method: 'PATCH',
      headers: {'Content-Type': 'application/json', },
      data: updateShopRequest
    },
      options);
    }
  


export const getUpdateShopMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof updateShop>>, TError,{shopId: string;data: UpdateShopRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof updateShop>>, TError,{shopId: string;data: UpdateShopRequest}, TContext> => {

const mutationKey = ['updateShop'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof updateShop>>, {shopId: string;data: UpdateShopRequest}> = (props) => {
          const {shopId,data} = props ?? {};

          return  updateShop(shopId,data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type UpdateShopMutationResult = NonNullable<Awaited<ReturnType<typeof updateShop>>>
    export type UpdateShopMutationBody = UpdateShopRequest
    export type UpdateShopMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useUpdateShop = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof updateShop>>, TError,{shopId: string;data: UpdateShopRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof updateShop>>,
        TError,
        {shopId: string;data: UpdateShopRequest},
        TContext
      > => {

      const mutationOptions = getUpdateShopMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    