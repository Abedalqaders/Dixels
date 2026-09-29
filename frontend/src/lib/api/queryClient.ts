import { QueryClient } from '@tanstack/react-query'
import { ApiError } from './httpClient'

/**
 * One QueryClient for the app: the shared cache every `useApiQuery` reads from. Two pages
 * asking for the same thing share one request; a page revisited shows what it had while
 * the fresh copy loads; returning to the tab re-reads anything stale (an admin edited a
 * room's hours, a colleague booked it) without polling while nobody is watching.
 */
export function createQueryClient(overrides: { retry?: boolean } = {}): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Fresh for 10s: a StrictMode double-mount or a quick hop between two pages that
        // need the same building doesn't fetch it twice.
        staleTime: 10_000,
        refetchOnWindowFocus: true,
        // Don't retry what the server already answered (a 404, a broken rule, no
        // permission); one more try for a dropped connection.
        retry: (failureCount, error) => {
          if (overrides.retry === false) return false
          if (error instanceof ApiError && !error.isNetworkError) return false
          return failureCount < 1
        },
      },
    },
  })
}

let current: QueryClient | undefined

/** Set by <AppQueryProvider>: lets plain functions (the "bookings changed" signal) reach the cache. */
export function registerQueryClient(client: QueryClient | undefined): void {
  current = client
}

export function getQueryClient(): QueryClient | undefined {
  return current
}
