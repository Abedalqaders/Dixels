import { useCallback, useEffect, useState } from 'react'

type AsyncState<T> =
  | { status: 'loading'; data: undefined; error: undefined }
  | { status: 'success'; data: T; error: undefined }
  | { status: 'error'; data: undefined; error: Error }

/**
 * Removes the fetch/loading/error boilerplate that would otherwise be duplicated between
 * every page that loads its own data on mount (the hierarchy page, the constraints page).
 * `fetcher` re-runs whenever `deps` changes, matching useEffect's own dependency semantics.
 */
export function useAsync<T>(fetcher: () => Promise<T>, deps: unknown[]): AsyncState<T> & { refetch: () => void } {
  const [state, setState] = useState<AsyncState<T>>({ status: 'loading', data: undefined, error: undefined })
  const [refetchToken, setRefetchToken] = useState(0)

  const refetch = useCallback(() => setRefetchToken((t) => t + 1), [])

  useEffect(() => {
    let cancelled = false
    setState({ status: 'loading', data: undefined, error: undefined })

    fetcher()
      .then((data) => {
        if (!cancelled) setState({ status: 'success', data, error: undefined })
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState({ status: 'error', data: undefined, error: error instanceof Error ? error : new Error(String(error)) })
        }
      })

    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, refetchToken])

  return { ...state, refetch }
}
