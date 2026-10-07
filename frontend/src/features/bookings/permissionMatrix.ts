import type { PermissionMatrixRow } from '@/test/permissionMatrix'
import { BookingsPermissions as P } from './permissions'

export const rows: PermissionMatrixRow[] = [
  {
    // Reads with Bookings.Default (availability service), books with Bookings.Create.
    route: '/find-space',
    link: 'Find a space',
    allows: [[P.Bookings.Default, P.Bookings.Create]],
    denies: [[P.Bookings.Create], [P.Bookings.Default]],
  },
]
