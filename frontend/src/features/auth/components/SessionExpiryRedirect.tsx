import { useEffect } from 'react'
import { useAuth } from 'react-oidc-context'
import { setUnauthorizedHandler } from '@/lib/api/httpClient'

/**
 * When the API answers 401 the stored token is no good any more (expired, revoked, the
 * account was removed or deactivated). Drop it: <RequireAuth> then sees nobody signed in
 * and sends the tab to the login page, coming back to the same page afterwards.
 */
export function SessionExpiryRedirect() {
  // Stable for the life of the provider, so the handler is registered once.
  const { removeUser } = useAuth()

  useEffect(() => {
    let clearing = false
    setUnauthorizedHandler(() => {
      // A page fires several requests at once; one 401 is enough to act on.
      if (clearing) return
      clearing = true
      void removeUser().finally(() => {
        clearing = false
      })
    })
    return () => setUnauthorizedHandler(undefined)
  }, [removeUser])

  return null
}
