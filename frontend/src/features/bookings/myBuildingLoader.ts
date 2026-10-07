import type { QueryClient } from '@tanstack/react-query'
import type { LoaderFunction, ShouldRevalidateFunction } from 'react-router-dom'
import { userManager } from '@/features/auth/userManager'
import { getMyBookableBuilding } from '@/features/bookings/api/bookingsApi'
import { queryKeys } from '@/lib/api/queryKeys'

/**
 * Starts loading the signed-in user's building the moment My calendar or Find a space is
 * opened, beside the permissions check and the page's code instead of after them: the page
 * only renders once its permission is known, and only then would its own query start.
 *
 * Not awaited, so the page never waits on it. It goes into the shared cache under the same
 * key, with the same request, as the pages' useApiQuery — they pick it up from there (or
 * join it while it's still on its way) rather than asking a second time.
 */
export function myBuildingLoader(queryClient: QueryClient): LoaderFunction {
  return async () => {
    const user = await userManager.getUser()
    // Signed out or expired: <RequireAuth> and the token renew deal with that; the page
    // asks for the building itself once there's a token.
    if (user && !user.expired) {
      void queryClient.prefetchQuery({
        queryKey: queryKeys.bookings.myBuilding(),
        queryFn: () => getMyBookableBuilding(user.access_token),
      })
    }
    return null
  }
}

/** Only the first visit needs a head start: stepping weeks (?view=…&date=…) must not re-run it. */
export const loadOnce: ShouldRevalidateFunction = () => false
