import { useAuth } from 'react-oidc-context'
import { hasRole } from '@/features/auth/roles'

const ADMIN_ROLE = 'admin'

/** Whether the user holds the admin role — only for labelling them ("Administrator").
 * What they can open and do follows their permissions (usePermission), not this. */
export function useAuthRole() {
  const auth = useAuth()
  return { isAdmin: hasRole(auth.user, ADMIN_ROLE) }
}
