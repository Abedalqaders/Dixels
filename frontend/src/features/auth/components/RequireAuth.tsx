import { useEffect } from 'react'
import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { useTranslation } from 'react-i18next'
import { AuthStatusScreen } from './AuthStatusScreen'
import { signInExtras } from '@/features/auth/signIn'

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
  const { t } = useTranslation()
  const auth = useAuth()
  const location = useLocation()

  useEffect(() => {
    if (!auth.isLoading && !auth.isAuthenticated && !auth.activeNavigator) {
      const state: SignInState = { returnTo: location.pathname + location.search }
      auth.signinRedirect({ state, ...signInExtras() })
    }
  }, [auth, location.pathname, location.search])

  if (auth.isLoading || (!auth.isAuthenticated && !auth.error)) {
    return <AuthStatusScreen state="busy" title={t('Auth:Redirecting')} detail={t('Auth:RedirectingComeBack')} />
  }

  if (auth.error) {
    return (
      <AuthStatusScreen state="error" title={t('Auth:CheckFailed')} detail={auth.error.message}>
        <Link className="btn" to="/">
          {t('Common:BackToSignIn')}
        </Link>
      </AuthStatusScreen>
    )
  }

  return <>{children}</>
}
