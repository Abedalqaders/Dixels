import { useEffect } from 'react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { AuthStatusScreen } from './AuthStatusScreen'

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
