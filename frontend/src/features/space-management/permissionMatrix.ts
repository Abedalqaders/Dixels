import type { PermissionMatrixRow } from '@/test/permissionMatrix'
import { SpaceManagementPermissions as P } from './permissions'

export const rows: PermissionMatrixRow[] = [
  {
    // "See the path": whoever may see any level reads the buildings above it.
    route: '/admin/buildings',
    link: 'Hierarchy',
    allows: [[P.Buildings.Default], [P.Floors.Default], [P.Spaces.Default]],
    denies: [[P.Buildings.Edit], [P.Overrides.Default], [P.SpaceTypes.Default]],
  },
  {
    route: '/admin/buildings/:buildingId/floors',
    allows: [[P.Floors.Default], [P.Spaces.Default]],
    denies: [[P.Buildings.Default]],
  },
  {
    route: '/admin/buildings/:buildingId/floors/:floorId/spaces',
    allows: [[P.Spaces.Default]],
    denies: [[P.Floors.Default], [P.Buildings.Default]],
  },
  {
    route: '/admin/space-types',
    link: 'Space types',
    allows: [[P.SpaceTypes.Default]],
    denies: [[P.SpaceTypes.Create], [P.Spaces.Default]],
  },
]
