/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import {
  useQuery
} from '@tanstack/react-query';
import type {
  DataTag,
  DefinedInitialDataOptions,
  DefinedUseQueryResult,
  QueryClient,
  QueryFunction,
  QueryKey,
  UndefinedInitialDataOptions,
  UseQueryOptions,
  UseQueryResult
} from '@tanstack/react-query';

import type {
  ProblemDetails,
  PublishedSiteDto
} from '.././model';

import { customInstance } from '../../../mutator/axios-instance';


type SecondParameter<T extends (...args: never) => unknown> = Parameters<T>[1];



export const getPublishedSite = (
    
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<PublishedSiteDto>(
      {url: `/api/site`, method: 'GET', signal
    },
      options);
    }
  



export const getGetPublishedSiteQueryKey = () => {
    return [
    `/api/site`
    ] as const;
    }

    
export const getGetPublishedSiteQueryOptions = <TData = Awaited<ReturnType<typeof getPublishedSite>>, TError = void | ProblemDetails>( options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getPublishedSite>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetPublishedSiteQueryKey();

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getPublishedSite>>> = ({ signal }) => getPublishedSite(requestOptions, signal);

      

      

   return  { queryKey, queryFn, ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getPublishedSite>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetPublishedSiteQueryResult = NonNullable<Awaited<ReturnType<typeof getPublishedSite>>>
export type GetPublishedSiteQueryError = void | ProblemDetails


export function useGetPublishedSite<TData = Awaited<ReturnType<typeof getPublishedSite>>, TError = void | ProblemDetails>(
  options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getPublishedSite>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getPublishedSite>>,
          TError,
          Awaited<ReturnType<typeof getPublishedSite>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetPublishedSite<TData = Awaited<ReturnType<typeof getPublishedSite>>, TError = void | ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getPublishedSite>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getPublishedSite>>,
          TError,
          Awaited<ReturnType<typeof getPublishedSite>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetPublishedSite<TData = Awaited<ReturnType<typeof getPublishedSite>>, TError = void | ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getPublishedSite>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetPublishedSite<TData = Awaited<ReturnType<typeof getPublishedSite>>, TError = void | ProblemDetails>(
  options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getPublishedSite>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetPublishedSiteQueryOptions(options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




