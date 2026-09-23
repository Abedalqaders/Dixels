import { useEffect } from 'react'
import type { ReactNode } from 'react'
import { useAuth } from 'react-oidc-context'

// Wrap any route that needs a signed-in user with <RequireAuth>.
// There's no in-app login form: "signing in" means redirecting the whole
// browser tab to the backend's own login page, and coming back once it's
// done. signinRedirect() is what kicks that redirect off.
export function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth()

  useEffect(() => {
    if (!auth.isLoading && !auth.isAuthenticated && !auth.activeNavigator) {
      auth.signinRedirect()
    }
  }, [auth])

  if (auth.isLoading || (!auth.isAuthenticated && !auth.error)) {
    return <p>Redirecting to sign in…</p>
  }

  if (auth.error) {
    return <p>Authentication error: {auth.error.message}</p>
  }

  return <>{children}</>
}
