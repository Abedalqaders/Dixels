import { useEffect } from 'react'
import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { AuthStatusScreen } from './AuthStatusScreen'

/** Where to land after signing in, carried through the login round trip in the OIDC state. */
export interface SignInState {
  returnTo?: string
}

// Wrap any route that needs a signed-in user with <RequireAuth>.
// There's no in-app login form: "signing in" means redirecting the whole
// browser tab to the backend's own login page, and coming back once it's
// done. signinRedirect() is what kicks that redirect off — also when a
// session ends mid-use (a 401 clears it, see SessionGuard).
export function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth()
  const location = useLocation()

  useEffect(() => {
    if (!auth.isLoading && !auth.isAuthenticated && !auth.activeNavigator) {
      const state: SignInState = { returnTo: location.pathname + location.search }
      auth.signinRedirect({ state })
    }
  }, [auth, location.pathname, location.search])

  if (auth.isLoading || (!auth.isAuthenticated && !auth.error)) {
    return <AuthStatusScreen state="busy" title="Taking you to sign in…" detail="You'll come straight back here afterwards." />
  }

  if (auth.error) {
    return (
      <AuthStatusScreen state="error" title="Couldn't check your sign-in" detail={auth.error.message}>
        <Link className="btn" to="/">
          Back to sign in
        </Link>
      </AuthStatusScreen>
    )
  }

  return <>{children}</>
}
