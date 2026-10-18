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
  CreatePageRequest,
  HttpValidationProblemDetails,
  PageDraftDto,
  PageDto,
  ProblemDetails,
  SavePageDraftRequest,
  SavePageDraftResultDto,
  ThemeDto,
  UpdatePageRequest,
  UpdateThemeRequest,
  WebsiteDto
} from '.././model';

import { customInstance } from '../../../mutator/axios-instance';


type SecondParameter<T extends (...args: never) => unknown> = Parameters<T>[1];



export const createWebsite = (
    shopId: string,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<WebsiteDto>(
      {url: `/api/shops/${shopId}/website`, method: 'POST', signal
    },
      options);
    }
  


export const getCreateWebsiteMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof createWebsite>>, TError,{shopId: string}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof createWebsite>>, TError,{shopId: string}, TContext> => {

const mutationKey = ['createWebsite'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof createWebsite>>, {shopId: string}> = (props) => {
          const {shopId} = props ?? {};

          return  createWebsite(shopId,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type CreateWebsiteMutationResult = NonNullable<Awaited<ReturnType<typeof createWebsite>>>
    
    export type CreateWebsiteMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useCreateWebsite = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof createWebsite>>, TError,{shopId: string}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof createWebsite>>,
        TError,
        {shopId: string},
        TContext
      > => {

      const mutationOptions = getCreateWebsiteMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getWebsite = (
    shopId: string,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<WebsiteDto>(
      {url: `/api/shops/${shopId}/website`, method: 'GET', signal
    },
      options);
    }
  



export const getGetWebsiteQueryKey = (shopId?: string,) => {
    return [
    `/api/shops/${shopId}/website`
    ] as const;
    }

    
export const getGetWebsiteQueryOptions = <TData = Awaited<ReturnType<typeof getWebsite>>, TError = ProblemDetails>(shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getWebsite>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetWebsiteQueryKey(shopId);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getWebsite>>> = ({ signal }) => getWebsite(shopId, requestOptions, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getWebsite>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetWebsiteQueryResult = NonNullable<Awaited<ReturnType<typeof getWebsite>>>
export type GetWebsiteQueryError = ProblemDetails


export function useGetWebsite<TData = Awaited<ReturnType<typeof getWebsite>>, TError = ProblemDetails>(
 shopId: string, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getWebsite>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getWebsite>>,
          TError,
          Awaited<ReturnType<typeof getWebsite>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetWebsite<TData = Awaited<ReturnType<typeof getWebsite>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getWebsite>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getWebsite>>,
          TError,
          Awaited<ReturnType<typeof getWebsite>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetWebsite<TData = Awaited<ReturnType<typeof getWebsite>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getWebsite>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetWebsite<TData = Awaited<ReturnType<typeof getWebsite>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getWebsite>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetWebsiteQueryOptions(shopId,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const listPages = (
    shopId: string,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<PageDto[]>(
      {url: `/api/shops/${shopId}/website/pages`, method: 'GET', signal
    },
      options);
    }
  



export const getListPagesQueryKey = (shopId?: string,) => {
    return [
    `/api/shops/${shopId}/website/pages`
    ] as const;
    }

    
export const getListPagesQueryOptions = <TData = Awaited<ReturnType<typeof listPages>>, TError = ProblemDetails>(shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listPages>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getListPagesQueryKey(shopId);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof listPages>>> = ({ signal }) => listPages(shopId, requestOptions, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof listPages>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type ListPagesQueryResult = NonNullable<Awaited<ReturnType<typeof listPages>>>
export type ListPagesQueryError = ProblemDetails


export function useListPages<TData = Awaited<ReturnType<typeof listPages>>, TError = ProblemDetails>(
 shopId: string, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof listPages>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof listPages>>,
          TError,
          Awaited<ReturnType<typeof listPages>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useListPages<TData = Awaited<ReturnType<typeof listPages>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listPages>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof listPages>>,
          TError,
          Awaited<ReturnType<typeof listPages>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useListPages<TData = Awaited<ReturnType<typeof listPages>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listPages>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useListPages<TData = Awaited<ReturnType<typeof listPages>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listPages>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getListPagesQueryOptions(shopId,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const createPage = (
    shopId: string,
    createPageRequest: CreatePageRequest,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<PageDto>(
      {url: `/api/shops/${shopId}/website/pages`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: createPageRequest, signal
    },
      options);
    }
  


export const getCreatePageMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof createPage>>, TError,{shopId: string;data: CreatePageRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof createPage>>, TError,{shopId: string;data: CreatePageRequest}, TContext> => {

const mutationKey = ['createPage'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof createPage>>, {shopId: string;data: CreatePageRequest}> = (props) => {
          const {shopId,data} = props ?? {};

          return  createPage(shopId,data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type CreatePageMutationResult = NonNullable<Awaited<ReturnType<typeof createPage>>>
    export type CreatePageMutationBody = CreatePageRequest
    export type CreatePageMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useCreatePage = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof createPage>>, TError,{shopId: string;data: CreatePageRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof createPage>>,
        TError,
        {shopId: string;data: CreatePageRequest},
        TContext
      > => {

      const mutationOptions = getCreatePageMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const updatePage = (
    shopId: string,
    pageId: string,
    updatePageRequest: UpdatePageRequest,
 options?: SecondParameter<typeof customInstance>,) => {
      
      
      return customInstance<PageDto>(
      {url: `/api/shops/${shopId}/website/pages/${pageId}`, method: 'PUT',
      headers: {'Content-Type': 'application/json', },
      data: updatePageRequest
    },
      options);
    }
  


export const getUpdatePageMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof updatePage>>, TError,{shopId: string;pageId: string;data: UpdatePageRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof updatePage>>, TError,{shopId: string;pageId: string;data: UpdatePageRequest}, TContext> => {

const mutationKey = ['updatePage'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof updatePage>>, {shopId: string;pageId: string;data: UpdatePageRequest}> = (props) => {
          const {shopId,pageId,data} = props ?? {};

          return  updatePage(shopId,pageId,data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type UpdatePageMutationResult = NonNullable<Awaited<ReturnType<typeof updatePage>>>
    export type UpdatePageMutationBody = UpdatePageRequest
    export type UpdatePageMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useUpdatePage = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof updatePage>>, TError,{shopId: string;pageId: string;data: UpdatePageRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof updatePage>>,
        TError,
        {shopId: string;pageId: string;data: UpdatePageRequest},
        TContext
      > => {

      const mutationOptions = getUpdatePageMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const deletePage = (
    shopId: string,
    pageId: string,
 options?: SecondParameter<typeof customInstance>,) => {
      
      
      return customInstance<void>(
      {url: `/api/shops/${shopId}/website/pages/${pageId}`, method: 'DELETE'
    },
      options);
    }
  


export const getDeletePageMutationOptions = <TError = ProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof deletePage>>, TError,{shopId: string;pageId: string}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof deletePage>>, TError,{shopId: string;pageId: string}, TContext> => {

const mutationKey = ['deletePage'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof deletePage>>, {shopId: string;pageId: string}> = (props) => {
          const {shopId,pageId} = props ?? {};

          return  deletePage(shopId,pageId,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type DeletePageMutationResult = NonNullable<Awaited<ReturnType<typeof deletePage>>>
    
    export type DeletePageMutationError = ProblemDetails

    export const useDeletePage = <TError = ProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof deletePage>>, TError,{shopId: string;pageId: string}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof deletePage>>,
        TError,
        {shopId: string;pageId: string},
        TContext
      > => {

      const mutationOptions = getDeletePageMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getPageDraft = (
    shopId: string,
    pageId: string,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<PageDraftDto>(
      {url: `/api/shops/${shopId}/website/pages/${pageId}/draft`, method: 'GET', signal
    },
      options);
    }
  



export const getGetPageDraftQueryKey = (shopId?: string,
    pageId?: string,) => {
    return [
    `/api/shops/${shopId}/website/pages/${pageId}/draft`
    ] as const;
    }

    
export const getGetPageDraftQueryOptions = <TData = Awaited<ReturnType<typeof getPageDraft>>, TError = ProblemDetails>(shopId: string,
    pageId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getPageDraft>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetPageDraftQueryKey(shopId,pageId);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getPageDraft>>> = ({ signal }) => getPageDraft(shopId,pageId, requestOptions, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId && pageId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getPageDraft>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetPageDraftQueryResult = NonNullable<Awaited<ReturnType<typeof getPageDraft>>>
export type GetPageDraftQueryError = ProblemDetails


export function useGetPageDraft<TData = Awaited<ReturnType<typeof getPageDraft>>, TError = ProblemDetails>(
 shopId: string,
    pageId: string, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getPageDraft>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getPageDraft>>,
          TError,
          Awaited<ReturnType<typeof getPageDraft>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetPageDraft<TData = Awaited<ReturnType<typeof getPageDraft>>, TError = ProblemDetails>(
 shopId: string,
    pageId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getPageDraft>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getPageDraft>>,
          TError,
          Awaited<ReturnType<typeof getPageDraft>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetPageDraft<TData = Awaited<ReturnType<typeof getPageDraft>>, TError = ProblemDetails>(
 shopId: string,
    pageId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getPageDraft>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetPageDraft<TData = Awaited<ReturnType<typeof getPageDraft>>, TError = ProblemDetails>(
 shopId: string,
    pageId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getPageDraft>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetPageDraftQueryOptions(shopId,pageId,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const savePageDraft = (
    shopId: string,
    pageId: string,
    savePageDraftRequest: SavePageDraftRequest,
 options?: SecondParameter<typeof customInstance>,) => {
      
      
      return customInstance<SavePageDraftResultDto>(
      {url: `/api/shops/${shopId}/website/pages/${pageId}/draft`, method: 'PUT',
      headers: {'Content-Type': 'application/json', },
      data: savePageDraftRequest
    },
      options);
    }
  


export const getSavePageDraftMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof savePageDraft>>, TError,{shopId: string;pageId: string;data: SavePageDraftRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof savePageDraft>>, TError,{shopId: string;pageId: string;data: SavePageDraftRequest}, TContext> => {

const mutationKey = ['savePageDraft'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof savePageDraft>>, {shopId: string;pageId: string;data: SavePageDraftRequest}> = (props) => {
          const {shopId,pageId,data} = props ?? {};

          return  savePageDraft(shopId,pageId,data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type SavePageDraftMutationResult = NonNullable<Awaited<ReturnType<typeof savePageDraft>>>
    export type SavePageDraftMutationBody = SavePageDraftRequest
    export type SavePageDraftMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useSavePageDraft = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof savePageDraft>>, TError,{shopId: string;pageId: string;data: SavePageDraftRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof savePageDraft>>,
        TError,
        {shopId: string;pageId: string;data: SavePageDraftRequest},
        TContext
      > => {

      const mutationOptions = getSavePageDraftMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getTheme = (
    shopId: string,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<ThemeDto>(
      {url: `/api/shops/${shopId}/website/theme`, method: 'GET', signal
    },
      options);
    }
  



export const getGetThemeQueryKey = (shopId?: string,) => {
    return [
    `/api/shops/${shopId}/website/theme`
    ] as const;
    }

    
export const getGetThemeQueryOptions = <TData = Awaited<ReturnType<typeof getTheme>>, TError = ProblemDetails>(shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getTheme>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetThemeQueryKey(shopId);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getTheme>>> = ({ signal }) => getTheme(shopId, requestOptions, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getTheme>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetThemeQueryResult = NonNullable<Awaited<ReturnType<typeof getTheme>>>
export type GetThemeQueryError = ProblemDetails


export function useGetTheme<TData = Awaited<ReturnType<typeof getTheme>>, TError = ProblemDetails>(
 shopId: string, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getTheme>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getTheme>>,
          TError,
          Awaited<ReturnType<typeof getTheme>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetTheme<TData = Awaited<ReturnType<typeof getTheme>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getTheme>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getTheme>>,
          TError,
          Awaited<ReturnType<typeof getTheme>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetTheme<TData = Awaited<ReturnType<typeof getTheme>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getTheme>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetTheme<TData = Awaited<ReturnType<typeof getTheme>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getTheme>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetThemeQueryOptions(shopId,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const updateTheme = (
    shopId: string,
    updateThemeRequest: UpdateThemeRequest,
 options?: SecondParameter<typeof customInstance>,) => {
      
      
      return customInstance<ThemeDto>(
      {url: `/api/shops/${shopId}/website/theme`, method: 'PUT',
      headers: {'Content-Type': 'application/json', },
      data: updateThemeRequest
    },
      options);
    }
  


export const getUpdateThemeMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof updateTheme>>, TError,{shopId: string;data: UpdateThemeRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof updateTheme>>, TError,{shopId: string;data: UpdateThemeRequest}, TContext> => {

const mutationKey = ['updateTheme'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof updateTheme>>, {shopId: string;data: UpdateThemeRequest}> = (props) => {
          const {shopId,data} = props ?? {};

          return  updateTheme(shopId,data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type UpdateThemeMutationResult = NonNullable<Awaited<ReturnType<typeof updateTheme>>>
    export type UpdateThemeMutationBody = UpdateThemeRequest
    export type UpdateThemeMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useUpdateTheme = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof updateTheme>>, TError,{shopId: string;data: UpdateThemeRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof updateTheme>>,
        TError,
        {shopId: string;data: UpdateThemeRequest},
        TContext
      > => {

      const mutationOptions = getUpdateThemeMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    