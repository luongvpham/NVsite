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
  GetShopsShopIdMediaAssetsParams,
  GetShopsShopIdMediaLibraryParams,
  HttpValidationProblemDetails,
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




export const putShopsShopIdLogo = (
    shopId: string,
    uploadShopLogoForm: UploadShopLogoForm,
 ) => {
      
      const formData = new FormData();
if(uploadShopLogoForm.file !== undefined) {
 formData.append(`file`, uploadShopLogoForm.file)
 }

      return customInstance<ShopLogoDto>(
      {url: `/shops/${shopId}/logo`, method: 'PUT',
      headers: {'Content-Type': 'multipart/form-data', },
       data: formData
    },
      );
    }
  


export const getPutShopsShopIdLogoMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof putShopsShopIdLogo>>, TError,{shopId: string;data: UploadShopLogoForm}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof putShopsShopIdLogo>>, TError,{shopId: string;data: UploadShopLogoForm}, TContext> => {

const mutationKey = ['putShopsShopIdLogo'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof putShopsShopIdLogo>>, {shopId: string;data: UploadShopLogoForm}> = (props) => {
          const {shopId,data} = props ?? {};

          return  putShopsShopIdLogo(shopId,data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type PutShopsShopIdLogoMutationResult = NonNullable<Awaited<ReturnType<typeof putShopsShopIdLogo>>>
    export type PutShopsShopIdLogoMutationBody = UploadShopLogoForm
    export type PutShopsShopIdLogoMutationError = ProblemDetails | HttpValidationProblemDetails

    export const usePutShopsShopIdLogo = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof putShopsShopIdLogo>>, TError,{shopId: string;data: UploadShopLogoForm}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof putShopsShopIdLogo>>,
        TError,
        {shopId: string;data: UploadShopLogoForm},
        TContext
      > => {

      const mutationOptions = getPutShopsShopIdLogoMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const postShopsShopIdMediaSlotUploads = (
    shopId: string,
    uploadToSlotForm: UploadToSlotForm,
 signal?: AbortSignal
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
      {url: `/shops/${shopId}/media/slot-uploads`, method: 'POST',
      headers: {'Content-Type': 'multipart/form-data', },
       data: formData, signal
    },
      );
    }
  


export const getPostShopsShopIdMediaSlotUploadsMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postShopsShopIdMediaSlotUploads>>, TError,{shopId: string;data: UploadToSlotForm}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof postShopsShopIdMediaSlotUploads>>, TError,{shopId: string;data: UploadToSlotForm}, TContext> => {

const mutationKey = ['postShopsShopIdMediaSlotUploads'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof postShopsShopIdMediaSlotUploads>>, {shopId: string;data: UploadToSlotForm}> = (props) => {
          const {shopId,data} = props ?? {};

          return  postShopsShopIdMediaSlotUploads(shopId,data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type PostShopsShopIdMediaSlotUploadsMutationResult = NonNullable<Awaited<ReturnType<typeof postShopsShopIdMediaSlotUploads>>>
    export type PostShopsShopIdMediaSlotUploadsMutationBody = UploadToSlotForm
    export type PostShopsShopIdMediaSlotUploadsMutationError = ProblemDetails | HttpValidationProblemDetails

    export const usePostShopsShopIdMediaSlotUploads = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postShopsShopIdMediaSlotUploads>>, TError,{shopId: string;data: UploadToSlotForm}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof postShopsShopIdMediaSlotUploads>>,
        TError,
        {shopId: string;data: UploadToSlotForm},
        TContext
      > => {

      const mutationOptions = getPostShopsShopIdMediaSlotUploadsMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const postShopsShopIdMediaLibrary = (
    shopId: string,
    uploadToLibraryForm: UploadToLibraryForm,
 signal?: AbortSignal
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
      {url: `/shops/${shopId}/media/library`, method: 'POST',
      headers: {'Content-Type': 'multipart/form-data', },
       data: formData, signal
    },
      );
    }
  


export const getPostShopsShopIdMediaLibraryMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postShopsShopIdMediaLibrary>>, TError,{shopId: string;data: UploadToLibraryForm}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof postShopsShopIdMediaLibrary>>, TError,{shopId: string;data: UploadToLibraryForm}, TContext> => {

const mutationKey = ['postShopsShopIdMediaLibrary'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof postShopsShopIdMediaLibrary>>, {shopId: string;data: UploadToLibraryForm}> = (props) => {
          const {shopId,data} = props ?? {};

          return  postShopsShopIdMediaLibrary(shopId,data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type PostShopsShopIdMediaLibraryMutationResult = NonNullable<Awaited<ReturnType<typeof postShopsShopIdMediaLibrary>>>
    export type PostShopsShopIdMediaLibraryMutationBody = UploadToLibraryForm
    export type PostShopsShopIdMediaLibraryMutationError = ProblemDetails | HttpValidationProblemDetails

    export const usePostShopsShopIdMediaLibrary = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postShopsShopIdMediaLibrary>>, TError,{shopId: string;data: UploadToLibraryForm}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof postShopsShopIdMediaLibrary>>,
        TError,
        {shopId: string;data: UploadToLibraryForm},
        TContext
      > => {

      const mutationOptions = getPostShopsShopIdMediaLibraryMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getShopsShopIdMediaLibrary = (
    shopId: string,
    params?: GetShopsShopIdMediaLibraryParams,
 signal?: AbortSignal
) => {
      
      
      return customInstance<PagedResultOfMediaAssetDto>(
      {url: `/shops/${shopId}/media/library`, method: 'GET',
        params, signal
    },
      );
    }
  



export const getGetShopsShopIdMediaLibraryQueryKey = (shopId?: string,
    params?: GetShopsShopIdMediaLibraryParams,) => {
    return [
    `/shops/${shopId}/media/library`, ...(params ? [params]: [])
    ] as const;
    }

    
export const getGetShopsShopIdMediaLibraryQueryOptions = <TData = Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>, TError = ProblemDetails | HttpValidationProblemDetails>(shopId: string,
    params?: GetShopsShopIdMediaLibraryParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>, TError, TData>>, }
) => {

const {query: queryOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetShopsShopIdMediaLibraryQueryKey(shopId,params);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>> = ({ signal }) => getShopsShopIdMediaLibrary(shopId,params, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetShopsShopIdMediaLibraryQueryResult = NonNullable<Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>>
export type GetShopsShopIdMediaLibraryQueryError = ProblemDetails | HttpValidationProblemDetails


export function useGetShopsShopIdMediaLibrary<TData = Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params: undefined |  GetShopsShopIdMediaLibraryParams, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>,
          TError,
          Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShopsShopIdMediaLibrary<TData = Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params?: GetShopsShopIdMediaLibraryParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>,
          TError,
          Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShopsShopIdMediaLibrary<TData = Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params?: GetShopsShopIdMediaLibraryParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>, TError, TData>>, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetShopsShopIdMediaLibrary<TData = Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params?: GetShopsShopIdMediaLibraryParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaLibrary>>, TError, TData>>, }
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetShopsShopIdMediaLibraryQueryOptions(shopId,params,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const postShopsShopIdMediaLibraryAssetIdClones = (
    shopId: string,
    assetId: string,
    cloneRequest: CloneRequest,
 signal?: AbortSignal
) => {
      
      
      return customInstance<MediaAssetDto>(
      {url: `/shops/${shopId}/media/library/${assetId}/clones`, method: 'POST',
      headers: {'Content-Type': 'application/json', },
      data: cloneRequest, signal
    },
      );
    }
  


export const getPostShopsShopIdMediaLibraryAssetIdClonesMutationOptions = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postShopsShopIdMediaLibraryAssetIdClones>>, TError,{shopId: string;assetId: string;data: CloneRequest}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof postShopsShopIdMediaLibraryAssetIdClones>>, TError,{shopId: string;assetId: string;data: CloneRequest}, TContext> => {

const mutationKey = ['postShopsShopIdMediaLibraryAssetIdClones'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof postShopsShopIdMediaLibraryAssetIdClones>>, {shopId: string;assetId: string;data: CloneRequest}> = (props) => {
          const {shopId,assetId,data} = props ?? {};

          return  postShopsShopIdMediaLibraryAssetIdClones(shopId,assetId,data,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type PostShopsShopIdMediaLibraryAssetIdClonesMutationResult = NonNullable<Awaited<ReturnType<typeof postShopsShopIdMediaLibraryAssetIdClones>>>
    export type PostShopsShopIdMediaLibraryAssetIdClonesMutationBody = CloneRequest
    export type PostShopsShopIdMediaLibraryAssetIdClonesMutationError = ProblemDetails | HttpValidationProblemDetails

    export const usePostShopsShopIdMediaLibraryAssetIdClones = <TError = ProblemDetails | HttpValidationProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof postShopsShopIdMediaLibraryAssetIdClones>>, TError,{shopId: string;assetId: string;data: CloneRequest}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof postShopsShopIdMediaLibraryAssetIdClones>>,
        TError,
        {shopId: string;assetId: string;data: CloneRequest},
        TContext
      > => {

      const mutationOptions = getPostShopsShopIdMediaLibraryAssetIdClonesMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getShopsShopIdMediaLibraryAssetIdReferences = (
    shopId: string,
    assetId: string,
 signal?: AbortSignal
) => {
      
      
      return customInstance<MediaReferencesDto>(
      {url: `/shops/${shopId}/media/library/${assetId}/references`, method: 'GET', signal
    },
      );
    }
  



export const getGetShopsShopIdMediaLibraryAssetIdReferencesQueryKey = (shopId?: string,
    assetId?: string,) => {
    return [
    `/shops/${shopId}/media/library/${assetId}/references`
    ] as const;
    }

    
export const getGetShopsShopIdMediaLibraryAssetIdReferencesQueryOptions = <TData = Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>, TError = ProblemDetails>(shopId: string,
    assetId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>, TError, TData>>, }
) => {

const {query: queryOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetShopsShopIdMediaLibraryAssetIdReferencesQueryKey(shopId,assetId);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>> = ({ signal }) => getShopsShopIdMediaLibraryAssetIdReferences(shopId,assetId, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId && assetId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetShopsShopIdMediaLibraryAssetIdReferencesQueryResult = NonNullable<Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>>
export type GetShopsShopIdMediaLibraryAssetIdReferencesQueryError = ProblemDetails


export function useGetShopsShopIdMediaLibraryAssetIdReferences<TData = Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>, TError = ProblemDetails>(
 shopId: string,
    assetId: string, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>,
          TError,
          Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShopsShopIdMediaLibraryAssetIdReferences<TData = Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>, TError = ProblemDetails>(
 shopId: string,
    assetId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>,
          TError,
          Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShopsShopIdMediaLibraryAssetIdReferences<TData = Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>, TError = ProblemDetails>(
 shopId: string,
    assetId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>, TError, TData>>, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetShopsShopIdMediaLibraryAssetIdReferences<TData = Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>, TError = ProblemDetails>(
 shopId: string,
    assetId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaLibraryAssetIdReferences>>, TError, TData>>, }
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetShopsShopIdMediaLibraryAssetIdReferencesQueryOptions(shopId,assetId,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const deleteShopsShopIdMediaLibraryAssetId = (
    shopId: string,
    assetId: string,
 ) => {
      
      
      return customInstance<void>(
      {url: `/shops/${shopId}/media/library/${assetId}`, method: 'DELETE'
    },
      );
    }
  


export const getDeleteShopsShopIdMediaLibraryAssetIdMutationOptions = <TError = ProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof deleteShopsShopIdMediaLibraryAssetId>>, TError,{shopId: string;assetId: string}, TContext>, }
): UseMutationOptions<Awaited<ReturnType<typeof deleteShopsShopIdMediaLibraryAssetId>>, TError,{shopId: string;assetId: string}, TContext> => {

const mutationKey = ['deleteShopsShopIdMediaLibraryAssetId'];
const {mutation: mutationOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof deleteShopsShopIdMediaLibraryAssetId>>, {shopId: string;assetId: string}> = (props) => {
          const {shopId,assetId} = props ?? {};

          return  deleteShopsShopIdMediaLibraryAssetId(shopId,assetId,)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type DeleteShopsShopIdMediaLibraryAssetIdMutationResult = NonNullable<Awaited<ReturnType<typeof deleteShopsShopIdMediaLibraryAssetId>>>
    
    export type DeleteShopsShopIdMediaLibraryAssetIdMutationError = ProblemDetails

    export const useDeleteShopsShopIdMediaLibraryAssetId = <TError = ProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof deleteShopsShopIdMediaLibraryAssetId>>, TError,{shopId: string;assetId: string}, TContext>, }
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof deleteShopsShopIdMediaLibraryAssetId>>,
        TError,
        {shopId: string;assetId: string},
        TContext
      > => {

      const mutationOptions = getDeleteShopsShopIdMediaLibraryAssetIdMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    export const getShopsShopIdMediaAssets = (
    shopId: string,
    params?: GetShopsShopIdMediaAssetsParams,
 signal?: AbortSignal
) => {
      
      
      return customInstance<MediaAssetDto[]>(
      {url: `/shops/${shopId}/media/assets`, method: 'GET',
        params, signal
    },
      );
    }
  



export const getGetShopsShopIdMediaAssetsQueryKey = (shopId?: string,
    params?: GetShopsShopIdMediaAssetsParams,) => {
    return [
    `/shops/${shopId}/media/assets`, ...(params ? [params]: [])
    ] as const;
    }

    
export const getGetShopsShopIdMediaAssetsQueryOptions = <TData = Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>, TError = ProblemDetails | HttpValidationProblemDetails>(shopId: string,
    params?: GetShopsShopIdMediaAssetsParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>, TError, TData>>, }
) => {

const {query: queryOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetShopsShopIdMediaAssetsQueryKey(shopId,params);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>> = ({ signal }) => getShopsShopIdMediaAssets(shopId,params, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetShopsShopIdMediaAssetsQueryResult = NonNullable<Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>>
export type GetShopsShopIdMediaAssetsQueryError = ProblemDetails | HttpValidationProblemDetails


export function useGetShopsShopIdMediaAssets<TData = Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params: undefined |  GetShopsShopIdMediaAssetsParams, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>,
          TError,
          Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShopsShopIdMediaAssets<TData = Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params?: GetShopsShopIdMediaAssetsParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>,
          TError,
          Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShopsShopIdMediaAssets<TData = Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params?: GetShopsShopIdMediaAssetsParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>, TError, TData>>, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetShopsShopIdMediaAssets<TData = Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>, TError = ProblemDetails | HttpValidationProblemDetails>(
 shopId: string,
    params?: GetShopsShopIdMediaAssetsParams, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaAssets>>, TError, TData>>, }
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetShopsShopIdMediaAssetsQueryOptions(shopId,params,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




export const getShopsShopIdMediaUsage = (
    shopId: string,
 signal?: AbortSignal
) => {
      
      
      return customInstance<MediaUsageDto>(
      {url: `/shops/${shopId}/media/usage`, method: 'GET', signal
    },
      );
    }
  



export const getGetShopsShopIdMediaUsageQueryKey = (shopId?: string,) => {
    return [
    `/shops/${shopId}/media/usage`
    ] as const;
    }

    
export const getGetShopsShopIdMediaUsageQueryOptions = <TData = Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>, TError = ProblemDetails>(shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>, TError, TData>>, }
) => {

const {query: queryOptions} = options ?? {};

  const queryKey =  queryOptions?.queryKey ?? getGetShopsShopIdMediaUsageQueryKey(shopId);

  

    const queryFn: QueryFunction<Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>> = ({ signal }) => getShopsShopIdMediaUsage(shopId, signal);

      

      

   return  { queryKey, queryFn, enabled: !!(shopId), ...queryOptions} as UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>, TError, TData> & { queryKey: DataTag<QueryKey, TData, TError> }
}

export type GetShopsShopIdMediaUsageQueryResult = NonNullable<Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>>
export type GetShopsShopIdMediaUsageQueryError = ProblemDetails


export function useGetShopsShopIdMediaUsage<TData = Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>, TError = ProblemDetails>(
 shopId: string, options: { query:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>, TError, TData>> & Pick<
        DefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>,
          TError,
          Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  DefinedUseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShopsShopIdMediaUsage<TData = Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>, TError, TData>> & Pick<
        UndefinedInitialDataOptions<
          Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>,
          TError,
          Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>
        > , 'initialData'
      >, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }
export function useGetShopsShopIdMediaUsage<TData = Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>, TError, TData>>, }
 , queryClient?: QueryClient
  ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> }

export function useGetShopsShopIdMediaUsage<TData = Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>, TError = ProblemDetails>(
 shopId: string, options?: { query?:Partial<UseQueryOptions<Awaited<ReturnType<typeof getShopsShopIdMediaUsage>>, TError, TData>>, }
 , queryClient?: QueryClient 
 ):  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> } {

  const queryOptions = getGetShopsShopIdMediaUsageQueryOptions(shopId,options)

  const query = useQuery(queryOptions, queryClient) as  UseQueryResult<TData, TError> & { queryKey: DataTag<QueryKey, TData, TError> };

  query.queryKey = queryOptions.queryKey ;

  return query;
}




