import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useAuth } from 'react-oidc-context'
import { useRefetchOnFocus } from '@/hooks/useRefetchOnFocus'
import { ApiError } from '@/lib/api/httpClient'
import { getGrantedPolicies } from './permissionsApi'
import { PermissionsContext } from './permissionsContext'
import type { PermissionsState, PermissionsValue } from './permissionsContext'

/** What was last fetched, and for whom — so another user's grants are never shown as this one's. */
type Loaded = { userId: string } & ({ granted: Record<string, boolean> } | { error: Error })

/**
 * Loads the signed-in user's granted permissions once per sign-in and shares them with
 * usePermission / <RequirePermission>. Re-fetched when the user changes (sign-out, another
 * account), when the token renews, and when the tab is looked at again — which is how a grant
 * changed in ABP's admin pages (usually in another tab) reaches this one. The grants already
 * on screen stay put meanwhile, so a refresh never flashes a spinner.
 */
export function PermissionsProvider({ children }: { children: ReactNode }) {
  const auth = useAuth()
  const userId = auth.user?.profile.sub
  const token = auth.user?.access_token
  const [loaded, setLoaded] = useState<Loaded | null>(null)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    if (!userId || !token) return
    let cancelled = false
    getGrantedPolicies(token)
      .then((granted) => {
        if (!cancelled) setLoaded({ userId, granted })
      })
      .catch((error: unknown) => {
        if (cancelled) return
        // The session is gone: the API client has already dropped the user, and <RequireAuth>
        // is sending the tab to sign in. Showing "couldn't check your access" meanwhile would
        // only flash a wrong explanation.
        if (error instanceof ApiError && error.status === 401) return
        const err = error instanceof Error ? error : new Error(String(error))
        // A failed refresh keeps the grants we already have for this user.
        setLoaded((prev) => (prev?.userId === userId && 'granted' in prev ? prev : { userId, error: err }))
      })
    return () => {
      cancelled = true
    }
  }, [userId, token, attempt])

  // A silent refresh: unlike retry, keeps what's loaded until the new grants arrive.
  useRefetchOnFocus(() => setAttempt((a) => a + 1))

  const retry = useCallback(() => {
    setLoaded(null)
    setAttempt((a) => a + 1)
  }, [])

  const value = useMemo<PermissionsValue>(() => {
    let state: PermissionsState = { status: 'loading' }
    if (loaded && loaded.userId === userId) {
      state = 'granted' in loaded ? { status: 'success', granted: loaded.granted } : { status: 'error', error: loaded.error }
    }
    return { ...state, retry }
  }, [loaded, userId, retry])

  return <PermissionsContext.Provider value={value}>{children}</PermissionsContext.Provider>
}
