/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import {
  useMutation
} from '@tanstack/react-query';
import type {
  MutationFunction,
  QueryClient,
  UseMutationOptions,
  UseMutationResult
} from '@tanstack/react-query';

import type {
  ProblemDetails
} from '.././model';

import { customInstance } from '../../../mutator/axios-instance';


type SecondParameter<T extends (...args: never) => unknown> = Parameters<T>[1];



/**
 * Không body. Set cookie httpOnly `vsite_jobs_dashboard` (Path=/hangfire, SameSite=Strict, sống 15 phút). (#114, #115)
 * @summary Đổi JWT PlatformAdmin lấy cookie vào dashboard Hangfire (/hangfire).
 */
export const createJobsDashboardSession = (
    
 options?: SecondParameter<typeof customInstance>,signal?: AbortSignal
) => {
      
      
      return customInstance<void>(
      {url: `/api/platform/jobs/dashboard-session`, method: 'POST', signal
    },
      options);
    }
  


export const getCreateJobsDashboardSessionMutationOptions = <TError = ProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof createJobsDashboardSession>>, TError,void, TContext>, request?: SecondParameter<typeof customInstance>}
): UseMutationOptions<Awaited<ReturnType<typeof createJobsDashboardSession>>, TError,void, TContext> => {

const mutationKey = ['createJobsDashboardSession'];
const {mutation: mutationOptions, request: requestOptions} = options ?
      options.mutation && 'mutationKey' in options.mutation && options.mutation.mutationKey ?
      options
      : {...options, mutation: {...options.mutation, mutationKey}}
      : {mutation: { mutationKey, }, request: undefined};

      


      const mutationFn: MutationFunction<Awaited<ReturnType<typeof createJobsDashboardSession>>, void> = () => {
          

          return  createJobsDashboardSession(requestOptions)
        }

        


  return  { mutationFn, ...mutationOptions }}

    export type CreateJobsDashboardSessionMutationResult = NonNullable<Awaited<ReturnType<typeof createJobsDashboardSession>>>
    
    export type CreateJobsDashboardSessionMutationError = ProblemDetails

    /**
 * @summary Đổi JWT PlatformAdmin lấy cookie vào dashboard Hangfire (/hangfire).
 */
export const useCreateJobsDashboardSession = <TError = ProblemDetails,
    TContext = unknown>(options?: { mutation?:UseMutationOptions<Awaited<ReturnType<typeof createJobsDashboardSession>>, TError,void, TContext>, request?: SecondParameter<typeof customInstance>}
 , queryClient?: QueryClient): UseMutationResult<
        Awaited<ReturnType<typeof createJobsDashboardSession>>,
        TError,
        void,
        TContext
      > => {

      const mutationOptions = getCreateJobsDashboardSessionMutationOptions(options);

      return useMutation(mutationOptions, queryClient);
    }
    