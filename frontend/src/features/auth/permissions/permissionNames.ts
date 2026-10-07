import { BookingsFlows, BookingsPermissions } from '@/features/bookings/permissions'
import { SpaceManagementPermissions } from '@/features/space-management/permissions'
import { UsersPermissions } from '@/features/users/permissions'

// Each feature keeps its own permissions (features/<feature>/permissions.ts, mirroring
// DixelsPermissions.cs). This file gathers the existing ones under the names the app has
// always imported, so those imports never had to change. A new module needn't be added
// here: its pages import its own permissions.ts, and the "unknown to the backend" check
// reads the module registry (src/app/modules.ts).

export const Permissions = {
  ...SpaceManagementPermissions,
  ...BookingsPermissions,
  ...UsersPermissions,
} as const

export const Flows = { ...BookingsFlows } as const

export { HierarchyViewers, hierarchyPermissions } from '@/features/space-management/permissions'
export type { HierarchyLevel } from '@/features/space-management/permissions'
