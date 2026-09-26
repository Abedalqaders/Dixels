import { useCallback, useEffect, useState } from 'react'

type AsyncState<T> =
  | { status: 'loading'; data: undefined; error: undefined }
  | { status: 'success'; data: T; error: undefined }
  | { status: 'error'; data: undefined; error: Error }

interface UseAsyncOptions {
  /** Keep showing the last successful data while a re-fetch runs (paging, searching), with
   * `isRefreshing` set, instead of dropping back to 'loading'. Off by default: a page whose
   * form drafts are seeded from `data` (the constraints page) must never show the previous
   * record's values while the next one loads. */
  keepPreviousData?: boolean
}

const LOADING = { status: 'loading', data: undefined, error: undefined } as const

/**
 * Removes the fetch/loading/error boilerplate that would otherwise be duplicated between
 * every page that loads its own data on mount (the hierarchy page, the constraints page).
 * `fetcher` re-runs whenever `deps` changes, matching useEffect's own dependency semantics.
 */
export function useAsync<T>(
  fetcher: () => Promise<T>,
  deps: unknown[],
  options: UseAsyncOptions = {},
): AsyncState<T> & { isRefreshing: boolean; refetch: () => void } {
  const [state, setState] = useState<AsyncState<T>>(LOADING)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [refetchToken, setRefetchToken] = useState(0)

  const refetch = useCallback(() => setRefetchToken((t) => t + 1), [])

  useEffect(() => {
    let cancelled = false
    if (options.keepPreviousData) {
      setState((prev) => (prev.status === 'success' ? prev : LOADING))
    } else {
      setState(LOADING)
    }
    setIsRefreshing(true)

    fetcher()
      .then((data) => {
        if (!cancelled) setState({ status: 'success', data, error: undefined })
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState({ status: 'error', data: undefined, error: error instanceof Error ? error : new Error(String(error)) })
        }
      })
      .finally(() => {
        if (!cancelled) setIsRefreshing(false)
      })

    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, refetchToken])

  return { ...state, isRefreshing: state.status === 'success' && isRefreshing, refetch }
}
