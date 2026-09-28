import { useAuth } from 'react-oidc-context'
import { hasRole } from '@/features/auth/roles'

const ADMIN_ROLE = 'admin'
export const ADMIN_LANDING_PATH = '/admin/buildings'
// My calendar is the employee home: their own bookings first, Find a space one click away.
// The Dashboard and History pages are off the menu.
export const EMPLOYEE_LANDING_PATH = '/my-calendar'

/** Single source of truth for "is this user an admin, and where do they land." */
export function useAuthRole() {
  const auth = useAuth()
  const isAdmin = hasRole(auth.user, ADMIN_ROLE)
  return { isAdmin, landingPath: isAdmin ? ADMIN_LANDING_PATH : EMPLOYEE_LANDING_PATH }
}
