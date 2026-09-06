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
  CreateSampleCommand,
  HttpValidationProblemDetails,
  ListSamplesParams,
  PagedResultOfSampleSummary,
  ProblemDetails,
  SampleDetail
} from '.././model';

import { customInstance } from '../../../mutator/axios-instance';




export const listSamples = (
    params?: ListSamplesParams,
 signal?: AbortSignal
) => {
      
      
      return customInstance<PagedResultOfSampleSummary>(
      {url: `/samples`, method: 'GET',
        params, signal
    },
      );
    }
  



export const getListSamplesQueryKey = (params?: ListSamplesParams,) => {
    return [
    `/samples`, ...(params ? [params]: [])
    ] as const;
    }

    
export const getListSamplesQueryOptions = <TData = Awaited<ReturnType<typeof listSamples>>, TError = unknown>(params?: ListSamplesParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listSamples>>, TError, TData>>, }
) => {

const {query: queryOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getListSamplesQueryKey(params);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof listSamples>>> = ({ signal }) => listSamples(params, signal);

      

      

   return  { queryKey, queryFn, ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof listSamples>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type ListSamplesQueryResult = NonNullable<Awaited<ReturnType<typeof listSamples>>>
export type ListSamplesQueryError = unknown


export function useListSamples<TData = Awaited<ReturnType<typeof listSamples>>, TError = unknown>(
 params: undefined |  ListSamplesParams, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof listSamples>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof listSamples>>,
          TError,
          Awaited<ReturnType<typeof listSamples>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useListSamples<TData = Awaited<ReturnType<typeof listSamples>>, TError = unknown>(
 params?: ListSamplesParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listSamples>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof listSamples>>,
          TError,
          Awaited<ReturnType<typeof listSamples>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useListSamples<TData = Awaited<ReturnType<typeof listSamples>>, TError = unknown>(
 params?: ListSamplesParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listSamples>>, TError, TData>>, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useListSamples<TData = Awaited<ReturnType<typeof listSamples>>, TError = unknown>(
 params?: ListSamplesParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listSamples>>, TError, TData>>, }
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getListSamplesQueryOptions(params,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const createSample = (
    createSampleCommand: CreateSampleCommand,
 signal?: AbortSignal
) => {
      
      
      return customInstance<SampleDetail>(
      {url: `/samples`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: createSampleCommand, signal
    },
      );
    }
  


export const getCreateSampleMutationOptions = <TError = HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof createSample>>, TError,{data: CreateSampleCommand}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof createSample>>, TError,{data: CreateSampleCommand}, TContext> => {

const mutationKey = ['createSample'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof createSample>>, {data: CreateSampleCommand}> = (props) => {
          const {data} = props ?? {};

          return  createSample(data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type CreateSampleMutationResult = NonNullable<Awaited<ReturnType<typeof createSample>>>
    export type CreateSampleMutationBody = CreateSampleCommand
    export type CreateSampleMutationError = HttpValidationProblemDetails

    export const useCreateSample = <TError = HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof createSample>>, TError,{data: CreateSampleCommand}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof createSample>>,
        TError,
        {data: CreateSampleCommand},
        TContext
      > => {

      const mutationOptions = getCreateSampleMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getSampleById = (
    id: string,
 signal?: AbortSignal
) => {
      
      
      return customInstance<SampleDetail>(
      {url: `/samples/${id}`, method: 'GET', signal
    },
      );
    }
  



export const getGetSampleByIdQueryKey = (id?: string,) => {
    return [
    `/samples/${id}`
    ] as const;
    }

    
export const getGetSampleByIdQueryOptions = <TData = Awaited<ReturnType<typeof getSampleById>>, TError = ProblemDetails>(id: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getSampleById>>, TError, TData>>, }
) => {

const {query: queryOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetSampleByIdQueryKey(id);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getSampleById>>> = ({ signal }) => getSampleById(id, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(id), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getSampleById>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetSampleByIdQueryResult = NonNullable<Awaited<ReturnType<typeof getSampleById>>>
export type GetSampleByIdQueryError = ProblemDetails


export function useGetSampleById<TData = Awaited<ReturnType<typeof getSampleById>>, TError = ProblemDetails>(
 id: string, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getSampleById>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getSampleById>>,
          TError,
          Awaited<ReturnType<typeof getSampleById>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetSampleById<TData = Awaited<ReturnType<typeof getSampleById>>, TError = ProblemDetails>(
 id: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getSampleById>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getSampleById>>,
          TError,
          Awaited<ReturnType<typeof getSampleById>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetSampleById<TData = Awaited<ReturnType<typeof getSampleById>>, TError = ProblemDetails>(
 id: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getSampleById>>, TError, TData>>, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetSampleById<TData = Awaited<ReturnType<typeof getSampleById>>, TError = ProblemDetails>(
 id: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getSampleById>>, TError, TData>>, }
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetSampleByIdQueryOptions(id,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const getSampleByIdForShop = (
    shopId: string,
    id: string,
 signal?: AbortSignal
) => {
      
      
      return customInstance<SampleDetail>(
      {url: `/shops/${shopId}/samples/${id}`, method: 'GET', signal
    },
      );
    }
  



export const getGetSampleByIdForShopQueryKey = (shopId?: string,
    id?: string,) => {
    return [
    `/shops/${shopId}/samples/${id}`
    ] as const;
    }

    
export const getGetSampleByIdForShopQueryOptions = <TData = Awaited<ReturnType<typeof getSampleByIdForShop>>, TError = ProblemDetails>(shopId: string,
    id: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getSampleByIdForShop>>, TError, TData>>, }
) => {

const {query: queryOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetSampleByIdForShopQueryKey(shopId,id);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getSampleByIdForShop>>> = ({ signal }) => getSampleByIdForShop(shopId,id, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId && id), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getSampleByIdForShop>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetSampleByIdForShopQueryResult = NonNullable<Awaited<ReturnType<typeof getSampleByIdForShop>>>
export type GetSampleByIdForShopQueryError = ProblemDetails


export function useGetSampleByIdForShop<TData = Awaited<ReturnType<typeof getSampleByIdForShop>>, TError = ProblemDetails>(
 shopId: string,
    id: string, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getSampleByIdForShop>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getSampleByIdForShop>>,
          TError,
          Awaited<ReturnType<typeof getSampleByIdForShop>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetSampleByIdForShop<TData = Awaited<ReturnType<typeof getSampleByIdForShop>>, TError = ProblemDetails>(
 shopId: string,
    id: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getSampleByIdForShop>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getSampleByIdForShop>>,
          TError,
          Awaited<ReturnType<typeof getSampleByIdForShop>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetSampleByIdForShop<TData = Awaited<ReturnType<typeof getSampleByIdForShop>>, TError = ProblemDetails>(
 shopId: string,
    id: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getSampleByIdForShop>>, TError, TData>>, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetSampleByIdForShop<TData = Awaited<ReturnType<typeof getSampleByIdForShop>>, TError = ProblemDetails>(
 shopId: string,
    id: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getSampleByIdForShop>>, TError, TData>>, }
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetSampleByIdForShopQueryOptions(shopId,id,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




