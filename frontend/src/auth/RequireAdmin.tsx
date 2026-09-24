import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { RequireAuth } from './RequireAuth'
import { EMPLOYEE_LANDING_PATH, useAuthRole } from './useAuthRole'

// Wrap an admin-only route with <RequireAdmin>. Builds on <RequireAuth> for
// the sign-in redirect, then sends a signed-in non-admin to their own
// landing page instead of showing an error — matching how useAuthRole
// already decides where each role lands after login.
export function RequireAdmin({ children }: { children: ReactNode }) {
  return (
    <RequireAuth>
      <AdminOnly>{children}</AdminOnly>
    </RequireAuth>
  )
}

function AdminOnly({ children }: { children: ReactNode }) {
  const { isAdmin } = useAuthRole()

  if (!isAdmin) {
    return <Navigate to={EMPLOYEE_LANDING_PATH} replace />
  }

  return <>{children}</>
}
