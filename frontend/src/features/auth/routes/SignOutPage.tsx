import { useEffect, useRef } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { AuthStatusScreen } from '@/features/auth/components/AuthStatusScreen'

const SHOW_MS = 700

/**
 * /signing-out — what the sidebar's sign-out button opens. Shows "Signing you out…" while
 * the browser is sent to the backend's logout page, which then returns to the home page
 * (post_logout_redirect_uri).
 */
export function SignOutPage() {
  const auth = useAuth()
  // StrictMode runs effects twice in development; one logout redirect is enough.
  const started = useRef(false)

  useEffect(() => {
    if (started.current || !auth.isAuthenticated) return
    started.current = true
    // A beat on screen first: the logout redirect is otherwise so fast this page is never
    // seen. Not cleared on cleanup — the ref above already stops a second run, and clearing
    // here would cancel the only timer when StrictMode re-runs the effect.
    setTimeout(() => void auth.signoutRedirect(), SHOW_MS)
  }, [auth])

  // Already signed out (a bookmark, the back button): nothing to do here.
  if (!auth.isLoading && !auth.isAuthenticated && !started.current) {
    return <Navigate to="/" replace />
  }

  return <AuthStatusScreen state="busy" title="Signing you out…" detail="Ending your session securely." />
}
