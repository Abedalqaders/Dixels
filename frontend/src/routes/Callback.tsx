import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { hasRole } from '../auth/roles'

// This is the page the backend redirects back to after login
// (http://localhost:5173/callback — the exact redirect_uri we registered).
// react-oidc-context does the actual code-for-token exchange automatically
// as soon as this component mounts; we just wait for it to finish and then
// route to the right landing page based on the user's real role.
export function Callback() {
  const auth = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (!auth.isLoading && auth.isAuthenticated) {
      // Matches the mock: admins land on Space management, employees land
      // on their own dashboard.
      const destination = hasRole(auth.user, 'admin') ? '/admin/buildings' : '/dashboard'
      navigate(destination, { replace: true })
    }
  }, [auth.isLoading, auth.isAuthenticated, auth.user, navigate])

  if (auth.error) {
    return <p>Sign-in failed: {auth.error.message}</p>
  }

  return <p>Signing you in…</p>
}
