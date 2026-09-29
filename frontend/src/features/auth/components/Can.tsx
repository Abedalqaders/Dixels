import type { ReactNode } from 'react'
import { usePermission } from '@/features/auth/permissions/usePermission'
import type { PermissionRequirement } from '@/features/auth/permissions/usePermission'

/**
 * Shows `children` only to someone holding `permission` (a list means any of them), e.g.
 * <Can permission={Permissions.Buildings.Create}><button>+ Building</button></Can>.
 * An action the user may not take isn't offered, rather than failing with a 403 once tried.
 */
export function Can({ permission, children }: { permission: PermissionRequirement; children: ReactNode }) {
  return usePermission(permission) ? <>{children}</> : null
}
