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
  CloneRequest,
  GetAssetsByIdsParams,
  GetDerivativesParams,
  HttpValidationProblemDetails,
  ListLibraryParams,
  MediaAssetDto,
  MediaReferencesDto,
  MediaUsageDto,
  PagedResultOfMediaAssetDto,
  ProblemDetails,
  ShopLogoDto,
  SlotUploadResultDto,
  UploadShopLogoForm,
  UploadToLibraryForm,
  UploadToSlotForm
} from '.././model';

import { customInstance } from '../../../mutator/axios-instance';


type SecondParameter<T extends (...args: never) => unknown> = Parameters<T>[1];



export const uploadToSlot = (
    shopId: string,
    uploadToSlotForm: UploadToSlotForm,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      const formData = new FormData();
if(uploadToSlotForm.file !== undefined) {
 formData.append(`file`, uploadToSlotForm.file)
 }
if(uploadToSlotForm.preset !== undefined) {
 formData.append(`preset`, uploadToSlotForm.preset)
 }
if(uploadToSlotForm.focalX !== undefined) {
 formData.append(`focalX`, uploadToSlotForm.focalX.toString())
 }
if(uploadToSlotForm.focalY !== undefined) {
 formData.append(`focalY`, uploadToSlotForm.focalY.toString())
 }
if(uploadToSlotForm.saveToLibrary !== undefined) {
 formData.append(`saveToLibrary`, uploadToSlotForm.saveToLibrary.toString())
 }
if(uploadToSlotForm.altText !== undefined && uploadToSlotForm.altText !== null) {
 formData.append(`altText`, uploadToSlotForm.altText)
 }

      return customInstance<SlotUploadResultDto>(
      {url: `/api/shops/${shopId}/media/slot-uploads`, method: 'POST',
      headers: {'Content-Type': 'multipart/form-data', },
       data: formData, signal
    },
      options);
    }
  


export const getUploadToSlotMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof uploadToSlot>>, TError,{shopId: string;data: UploadToSlotForm}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof uploadToSlot>>, TError,{shopId: string;data: UploadToSlotForm}, TContext> => {

const mutationKey = ['uploadToSlot'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof uploadToSlot>>, {shopId: string;data: UploadToSlotForm}> = (props) => {
          const {shopId,data} = props ?? {};

          return  uploadToSlot(shopId,data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type UploadToSlotMutationResult = NonNullable<Awaited<ReturnType<typeof uploadToSlot>>>
    export type UploadToSlotMutationBody = UploadToSlotForm
    export type UploadToSlotMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useUploadToSlot = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof uploadToSlot>>, TError,{shopId: string;data: UploadToSlotForm}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof uploadToSlot>>,
        TError,
        {shopId: string;data: UploadToSlotForm},
        TContext
      > => {

      const mutationOptions = getUploadToSlotMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const uploadToLibrary = (
    shopId: string,
    uploadToLibraryForm: UploadToLibraryForm,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      const formData = new FormData();
if(uploadToLibraryForm.file !== undefined) {
 formData.append(`file`, uploadToLibraryForm.file)
 }
if(uploadToLibraryForm.altText !== undefined && uploadToLibraryForm.altText !== null) {
 formData.append(`altText`, uploadToLibraryForm.altText)
 }
if(uploadToLibraryForm.folder !== undefined && uploadToLibraryForm.folder !== null) {
 formData.append(`folder`, uploadToLibraryForm.folder)
 }

      return customInstance<MediaAssetDto>(
      {url: `/api/shops/${shopId}/media/library`, method: 'POST',
      headers: {'Content-Type': 'multipart/form-data', },
       data: formData, signal
    },
      options);
    }
  


export const getUploadToLibraryMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof uploadToLibrary>>, TError,{shopId: string;data: UploadToLibraryForm}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof uploadToLibrary>>, TError,{shopId: string;data: UploadToLibraryForm}, TContext> => {

const mutationKey = ['uploadToLibrary'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof uploadToLibrary>>, {shopId: string;data: UploadToLibraryForm}> = (props) => {
          const {shopId,data} = props ?? {};

          return  uploadToLibrary(shopId,data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type UploadToLibraryMutationResult = NonNullable<Awaited<ReturnType<typeof uploadToLibrary>>>
    export type UploadToLibraryMutationBody = UploadToLibraryForm
    export type UploadToLibraryMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useUploadToLibrary = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof uploadToLibrary>>, TError,{shopId: string;data: UploadToLibraryForm}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof uploadToLibrary>>,
        TError,
        {shopId: string;data: UploadToLibraryForm},
        TContext
      > => {

      const mutationOptions = getUploadToLibraryMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const listLibrary = (
    shopId: string,
    params?: ListLibraryParams,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<PagedResultOfMediaAssetDto>(
      {url: `/api/shops/${shopId}/media/library`, method: 'GET',
        params, signal
    },
      options);
    }
  



export const getListLibraryQueryKey = (shopId?: string,
    params?: ListLibraryParams,) => {
    return [
    `/api/shops/${shopId}/media/library`, ...(params ? [params]: [])
    ] as const;
    }

    
export const getListLibraryQueryOptions = <TData = Awaited<ReturnType<typeof listLibrary>>, TError = ProblemDetails | HttpValidationProblemDetails>(shopId: string,
    params?: ListLibraryParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listLibrary>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getListLibraryQueryKey(shopId,params);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof listLibrary>>> = ({ signal }) => listLibrary(shopId,params, requestOptions, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof listLibrary>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type ListLibraryQueryResult = NonNullable<Awaited<ReturnType<typeof listLibrary>>>
export type ListLibraryQueryError = ProblemDetails | HttpValidationProblemDetails


export function useListLibrary<TData = Awaited<ReturnType<typeof listLibrary>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params: undefined |  ListLibraryParams, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof listLibrary>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof listLibrary>>,
          TError,
          Awaited<ReturnType<typeof listLibrary>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useListLibrary<TData = Awaited<ReturnType<typeof listLibrary>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params?: ListLibraryParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listLibrary>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof listLibrary>>,
          TError,
          Awaited<ReturnType<typeof listLibrary>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useListLibrary<TData = Awaited<ReturnType<typeof listLibrary>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params?: ListLibraryParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listLibrary>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useListLibrary<TData = Awaited<ReturnType<typeof listLibrary>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params?: ListLibraryParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof listLibrary>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getListLibraryQueryOptions(shopId,params,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const cloneFromLibrary = (
    shopId: string,
    assetId: string,
    cloneRequest: CloneRequest,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<MediaAssetDto>(
      {url: `/api/shops/${shopId}/media/library/${assetId}/clones`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: cloneRequest, signal
    },
      options);
    }
  


export const getCloneFromLibraryMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof cloneFromLibrary>>, TError,{shopId: string;assetId: string;data: CloneRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof cloneFromLibrary>>, TError,{shopId: string;assetId: string;data: CloneRequest}, TContext> => {

const mutationKey = ['cloneFromLibrary'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof cloneFromLibrary>>, {shopId: string;assetId: string;data: CloneRequest}> = (props) => {
          const {shopId,assetId,data} = props ?? {};

          return  cloneFromLibrary(shopId,assetId,data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type CloneFromLibraryMutationResult = NonNullable<Awaited<ReturnType<typeof cloneFromLibrary>>>
    export type CloneFromLibraryMutationBody = CloneRequest
    export type CloneFromLibraryMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useCloneFromLibrary = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof cloneFromLibrary>>, TError,{shopId: string;assetId: string;data: CloneRequest}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof cloneFromLibrary>>,
        TError,
        {shopId: string;assetId: string;data: CloneRequest},
        TContext
      > => {

      const mutationOptions = getCloneFromLibraryMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getAssetReferences = (
    shopId: string,
    assetId: string,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<MediaReferencesDto>(
      {url: `/api/shops/${shopId}/media/library/${assetId}/references`, method: 'GET', signal
    },
      options);
    }
  



export const getGetAssetReferencesQueryKey = (shopId?: string,
    assetId?: string,) => {
    return [
    `/api/shops/${shopId}/media/library/${assetId}/references`
    ] as const;
    }

    
export const getGetAssetReferencesQueryOptions = <TData = Awaited<ReturnType<typeof getAssetReferences>>, TError = ProblemDetails>(shopId: string,
    assetId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAssetReferences>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetAssetReferencesQueryKey(shopId,assetId);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getAssetReferences>>> = ({ signal }) => getAssetReferences(shopId,assetId, requestOptions, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId && assetId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getAssetReferences>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetAssetReferencesQueryResult = NonNullable<Awaited<ReturnType<typeof getAssetReferences>>>
export type GetAssetReferencesQueryError = ProblemDetails


export function useGetAssetReferences<TData = Awaited<ReturnType<typeof getAssetReferences>>, TError = ProblemDetails>(
 shopId: string,
    assetId: string, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAssetReferences>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getAssetReferences>>,
          TError,
          Awaited<ReturnType<typeof getAssetReferences>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetAssetReferences<TData = Awaited<ReturnType<typeof getAssetReferences>>, TError = ProblemDetails>(
 shopId: string,
    assetId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAssetReferences>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getAssetReferences>>,
          TError,
          Awaited<ReturnType<typeof getAssetReferences>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetAssetReferences<TData = Awaited<ReturnType<typeof getAssetReferences>>, TError = ProblemDetails>(
 shopId: string,
    assetId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAssetReferences>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetAssetReferences<TData = Awaited<ReturnType<typeof getAssetReferences>>, TError = ProblemDetails>(
 shopId: string,
    assetId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAssetReferences>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetAssetReferencesQueryOptions(shopId,assetId,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const getDerivatives = (
    shopId: string,
    assetId: string,
    params?: GetDerivativesParams,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<MediaAssetDto[]>(
      {url: `/api/shops/${shopId}/media/library/${assetId}/derivatives`, method: 'GET',
        params, signal
    },
      options);
    }
  



export const getGetDerivativesQueryKey = (shopId?: string,
    assetId?: string,
    params?: GetDerivativesParams,) => {
    return [
    `/api/shops/${shopId}/media/library/${assetId}/derivatives`, ...(params ? [params]: [])
    ] as const;
    }

    
export const getGetDerivativesQueryOptions = <TData = Awaited<ReturnType<typeof getDerivatives>>, TError = ProblemDetails | HttpValidationProblemDetails>(shopId: string,
    assetId: string,
    params?: GetDerivativesParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getDerivatives>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetDerivativesQueryKey(shopId,assetId,params);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getDerivatives>>> = ({ signal }) => getDerivatives(shopId,assetId,params, requestOptions, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId && assetId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getDerivatives>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetDerivativesQueryResult = NonNullable<Awaited<ReturnType<typeof getDerivatives>>>
export type GetDerivativesQueryError = ProblemDetails | HttpValidationProblemDetails


export function useGetDerivatives<TData = Awaited<ReturnType<typeof getDerivatives>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    assetId: string,
    params: undefined |  GetDerivativesParams, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getDerivatives>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getDerivatives>>,
          TError,
          Awaited<ReturnType<typeof getDerivatives>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetDerivatives<TData = Awaited<ReturnType<typeof getDerivatives>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    assetId: string,
    params?: GetDerivativesParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getDerivatives>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getDerivatives>>,
          TError,
          Awaited<ReturnType<typeof getDerivatives>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetDerivatives<TData = Awaited<ReturnType<typeof getDerivatives>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    assetId: string,
    params?: GetDerivativesParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getDerivatives>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetDerivatives<TData = Awaited<ReturnType<typeof getDerivatives>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    assetId: string,
    params?: GetDerivativesParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getDerivatives>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetDerivativesQueryOptions(shopId,assetId,params,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const deleteFromLibrary = (
    shopId: string,
    assetId: string,
 options?: SecondParameter<typeof customInstance>,) => {
      
      
      return customInstance<void>(
      {url: `/api/shops/${shopId}/media/library/${assetId}`, method: 'DELETE'
    },
      options);
    }
  


export const getDeleteFromLibraryMutationOptions = <TError = ProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof deleteFromLibrary>>, TError,{shopId: string;assetId: string}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof deleteFromLibrary>>, TError,{shopId: string;assetId: string}, TContext> => {

const mutationKey = ['deleteFromLibrary'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof deleteFromLibrary>>, {shopId: string;assetId: string}> = (props) => {
          const {shopId,assetId} = props ?? {};

          return  deleteFromLibrary(shopId,assetId,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type DeleteFromLibraryMutationResult = NonNullable<Awaited<ReturnType<typeof deleteFromLibrary>>>
    
    export type DeleteFromLibraryMutationError = ProblemDetails

    export const useDeleteFromLibrary = <TError = ProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof deleteFromLibrary>>, TError,{shopId: string;assetId: string}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof deleteFromLibrary>>,
        TError,
        {shopId: string;assetId: string},
        TContext
      > => {

      const mutationOptions = getDeleteFromLibraryMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getAssetsByIds = (
    shopId: string,
    params?: GetAssetsByIdsParams,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<MediaAssetDto[]>(
      {url: `/api/shops/${shopId}/media/assets`, method: 'GET',
        params, signal
    },
      options);
    }
  



export const getGetAssetsByIdsQueryKey = (shopId?: string,
    params?: GetAssetsByIdsParams,) => {
    return [
    `/api/shops/${shopId}/media/assets`, ...(params ? [params]: [])
    ] as const;
    }

    
export const getGetAssetsByIdsQueryOptions = <TData = Awaited<ReturnType<typeof getAssetsByIds>>, TError = ProblemDetails | HttpValidationProblemDetails>(shopId: string,
    params?: GetAssetsByIdsParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAssetsByIds>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetAssetsByIdsQueryKey(shopId,params);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getAssetsByIds>>> = ({ signal }) => getAssetsByIds(shopId,params, requestOptions, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getAssetsByIds>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetAssetsByIdsQueryResult = NonNullable<Awaited<ReturnType<typeof getAssetsByIds>>>
export type GetAssetsByIdsQueryError = ProblemDetails | HttpValidationProblemDetails


export function useGetAssetsByIds<TData = Awaited<ReturnType<typeof getAssetsByIds>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params: undefined |  GetAssetsByIdsParams, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAssetsByIds>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getAssetsByIds>>,
          TError,
          Awaited<ReturnType<typeof getAssetsByIds>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetAssetsByIds<TData = Awaited<ReturnType<typeof getAssetsByIds>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params?: GetAssetsByIdsParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAssetsByIds>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getAssetsByIds>>,
          TError,
          Awaited<ReturnType<typeof getAssetsByIds>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetAssetsByIds<TData = Awaited<ReturnType<typeof getAssetsByIds>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params?: GetAssetsByIdsParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAssetsByIds>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetAssetsByIds<TData = Awaited<ReturnType<typeof getAssetsByIds>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params?: GetAssetsByIdsParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getAssetsByIds>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetAssetsByIdsQueryOptions(shopId,params,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const getMediaUsage = (
    shopId: string,
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<MediaUsageDto>(
      {url: `/api/shops/${shopId}/media/usage`, method: 'GET', signal
    },
      options);
    }
  



export const getGetMediaUsageQueryKey = (shopId?: string,) => {
    return [
    `/api/shops/${shopId}/media/usage`
    ] as const;
    }

    
export const getGetMediaUsageQueryOptions = <TData = Awaited<ReturnType<typeof getMediaUsage>>, TError = ProblemDetails>(shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getMediaUsage>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
) => {

const {query: queryOptions, request: requestOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetMediaUsageQueryKey(shopId);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getMediaUsage>>> = ({ signal }) => getMediaUsage(shopId, requestOptions, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getMediaUsage>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetMediaUsageQueryResult = NonNullable<Awaited<ReturnType<typeof getMediaUsage>>>
export type GetMediaUsageQueryError = ProblemDetails


export function useGetMediaUsage<TData = Awaited<ReturnType<typeof getMediaUsage>>, TError = ProblemDetails>(
 shopId: string, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getMediaUsage>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getMediaUsage>>,
          TError,
          Awaited<ReturnType<typeof getMediaUsage>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetMediaUsage<TData = Awaited<ReturnType<typeof getMediaUsage>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getMediaUsage>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getMediaUsage>>,
          TError,
          Awaited<ReturnType<typeof getMediaUsage>>
        > , 'initialData'
      >, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetMediaUsage<TData = Awaited<ReturnType<typeof getMediaUsage>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getMediaUsage>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetMediaUsage<TData = Awaited<ReturnType<typeof getMediaUsage>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getMediaUsage>>, TError, TData>>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetMediaUsageQueryOptions(shopId,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const uploadShopLogo = (
    shopId: string,
    uploadShopLogoForm: UploadShopLogoForm,
 options?: SecondParameter<typeof customInstance>,) => {
      
      const formData = new FormData();
if(uploadShopLogoForm.file !== undefined) {
 formData.append(`file`, uploadShopLogoForm.file)
 }

      return customInstance<ShopLogoDto>(
      {url: `/api/shops/${shopId}/logo`, method: 'PUT',
      headers: {'Content-Type': 'multipart/form-data', },
       data: formData
    },
      options);
    }
  


export const getUploadShopLogoMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof uploadShopLogo>>, TError,{shopId: string;data: UploadShopLogoForm}, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof uploadShopLogo>>, TError,{shopId: string;data: UploadShopLogoForm}, TContext> => {

const mutationKey = ['uploadShopLogo'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof uploadShopLogo>>, {shopId: string;data: UploadShopLogoForm}> = (props) => {
          const {shopId,data} = props ?? {};

          return  uploadShopLogo(shopId,data,requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type UploadShopLogoMutationResult = NonNullable<Awaited<ReturnType<typeof uploadShopLogo>>>
    export type UploadShopLogoMutationBody = UploadShopLogoForm
    export type UploadShopLogoMutationError = ProblemDetails | HttpValidationProblemDetails

    export const useUploadShopLogo = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof uploadShopLogo>>, TError,{shopId: string;data: UploadShopLogoForm}, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof uploadShopLogo>>,
        TError,
        {shopId: string;data: UploadShopLogoForm},
        TContext
      > => {

      const mutationOptions = getUploadShopLogoMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    