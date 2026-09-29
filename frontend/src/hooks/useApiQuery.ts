import { keepPreviousData, useQuery } from '@tanstack/react-query'
import type { QueryKey } from '@tanstack/react-query'

interface UseApiQueryOptions {
  /** Keep showing the last successful data while a re-fetch runs (paging, searching), with
   * `isRefreshing` set, instead of dropping back to 'loading'. Off by default: a page whose
   * form drafts are seeded from `data` (the constraints page) must never show the previous
   * record's values while the next one loads. */
  keepPreviousData?: boolean
}

export type ApiQueryResult<T> = (
  | { status: 'loading'; data: undefined; error: undefined }
  | { status: 'success'; data: T; error: undefined }
  | { status: 'error'; data: undefined; error: Error }
) & {
  /** A re-fetch is running behind data that is already on screen. */
  isRefreshing: boolean
  refetch: () => void
}

/**
 * A page's read of the server, through the shared query cache (see lib/api/queryClient).
 * `key` names the data (see lib/api/queryKeys); `fetcher` is how to get it. The fetcher
 * may close over the access token — the token is deliberately *not* in the key, so a
 * silent renew mid-edit doesn't refetch and reset the page.
 *
 * Returns the same `{ status, data, error }` shape as a hand-rolled fetch so pages read
 * naturally, plus the cache's benefits: one request per key however many components
 * ask, instant re-visits, refetch when the tab regains focus, and invalidation by key
 * (a booking made anywhere refreshes every bookings query).
 */
export function useApiQuery<T>(key: QueryKey, fetcher: () => Promise<T>, options: UseApiQueryOptions = {}): ApiQueryResult<T> {
  const query = useQuery({
    queryKey: key,
    queryFn: fetcher,
    placeholderData: options.keepPreviousData ? keepPreviousData : undefined,
  })

  const refetch = () => void query.refetch()

  if (query.isPending) {
    return { status: 'loading', data: undefined, error: undefined, isRefreshing: false, refetch }
  }
  if (query.isError) {
    return { status: 'error', data: undefined, error: query.error, isRefreshing: false, refetch }
  }
  return { status: 'success', data: query.data as T, error: undefined, isRefreshing: query.isFetching, refetch }
}
