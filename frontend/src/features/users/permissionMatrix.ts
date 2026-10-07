import type { PermissionMatrixRow } from '@/test/permissionMatrix'
import { BookingsPermissions } from '@/features/bookings/permissions'
import { UsersPermissions as P } from './permissions'

export const rows: PermissionMatrixRow[] = [
  {
    route: '/admin/users',
    link: 'Users',
    allows: [[P.Identity.Users]],
    denies: [[P.Identity.UsersUpdate], [BookingsPermissions.Bookings.Default]],
  },
]
