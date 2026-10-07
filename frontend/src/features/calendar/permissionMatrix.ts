import type { PermissionMatrixRow } from '@/test/permissionMatrix'
import { BookingsPermissions as P } from '@/features/bookings/permissions'

export const rows: PermissionMatrixRow[] = [
  {
    route: '/my-calendar',
    link: 'My calendar',
    allows: [[P.Bookings.Default]],
    denies: [[P.Bookings.Create], [P.Bookings.Cancel]],
  },
]
