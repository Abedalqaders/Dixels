import { useEffect } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { getDisplayName } from '@/features/auth/roles'
import { HOME_PATH } from '@/features/auth/landing'
import { AuthStatusScreen } from '@/features/auth/components/AuthStatusScreen'
import type { SignInState } from '@/features/auth/components/RequireAuth'

// Long enough to read "Signed in as …", short enough not to feel like a wait.
const CONFIRM_MS = 900

/** The page they were on before signing in — only a same-site path, never another origin. */
function returnPathOf(state: unknown): string | undefined {
  const returnTo = (state as SignInState | undefined)?.returnTo
  return returnTo?.startsWith('/') && !returnTo.startsWith('//') && !returnTo.startsWith('/callback') ? returnTo : undefined
}

// This is the page the backend redirects back to after login
// (http://localhost:5173/callback — the exact redirect_uri we registered).
// react-oidc-context does the actual code-for-token exchange automatically
// as soon as this component mounts; we just wait for it to finish, confirm who signed in,
// and then route to their home page — HOME_PATH picks it from what they're granted.
export function CallbackPage() {
  const auth = useAuth()
  const navigate = useNavigate()
  const signedIn = !auth.isLoading && auth.isAuthenticated
  const returnTo = returnPathOf(auth.user?.state)

  useEffect(() => {
    if (!signedIn) return
    const timer = setTimeout(() => navigate(returnTo ?? HOME_PATH, { replace: true }), CONFIRM_MS)
    return () => clearTimeout(timer)
  }, [signedIn, returnTo, navigate])

  if (auth.error) {
    return (
      <AuthStatusScreen state="error" title="Couldn't sign you in" detail={auth.error.message}>
        <Link className="btn" to="/">
          Back to sign in
        </Link>
      </AuthStatusScreen>
    )
  }

  if (signedIn) {
    return (
      <AuthStatusScreen
        state="done"
        title={`Signed in as ${getDisplayName(auth.user)}`}
        detail={returnTo ? 'Taking you back…' : 'Taking you to your home page…'}
      />
    )
  }

  return <AuthStatusScreen state="busy" title="Signing you in…" detail="Checking your account." />
}
