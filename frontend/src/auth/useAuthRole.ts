import { useAuth } from 'react-oidc-context'
import { hasRole } from './roles'

const ADMIN_ROLE = 'admin'
export const ADMIN_LANDING_PATH = '/admin/buildings'
export const EMPLOYEE_LANDING_PATH = '/dashboard'

/** Single source of truth for "is this user an admin, and where do they land." */
export function useAuthRole() {
  const auth = useAuth()
  const isAdmin = hasRole(auth.user, ADMIN_ROLE)
  return { isAdmin, landingPath: isAdmin ? ADMIN_LANDING_PATH : EMPLOYEE_LANDING_PATH }
}
