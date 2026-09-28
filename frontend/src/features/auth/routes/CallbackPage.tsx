import { useEffect } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { getDisplayName } from '@/features/auth/roles'
import { useAuthRole } from '@/features/auth/hooks/useAuthRole'
import { AuthStatusScreen } from '@/features/auth/components/AuthStatusScreen'

// Long enough to read "Signed in as …", short enough not to feel like a wait.
const CONFIRM_MS = 900

// This is the page the backend redirects back to after login
// (http://localhost:5173/callback — the exact redirect_uri we registered).
// react-oidc-context does the actual code-for-token exchange automatically
// as soon as this component mounts; we just wait for it to finish, confirm who signed in,
// and then route to the right landing page based on the user's real role.
export function CallbackPage() {
  const auth = useAuth()
  const navigate = useNavigate()
  const { isAdmin, landingPath } = useAuthRole()
  const signedIn = !auth.isLoading && auth.isAuthenticated

  useEffect(() => {
    if (!signedIn) return
    const timer = setTimeout(() => navigate(landingPath, { replace: true }), CONFIRM_MS)
    return () => clearTimeout(timer)
  }, [signedIn, landingPath, navigate])

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
        detail={`Taking you to ${isAdmin ? 'Space management' : 'My calendar'}…`}
      />
    )
  }

  return <AuthStatusScreen state="busy" title="Signing you in…" detail="Checking your account." />
}
