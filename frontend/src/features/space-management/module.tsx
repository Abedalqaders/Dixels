import type { ReactNode } from 'react'
import { Navigate, Route, useParams } from 'react-router-dom'
import type { TextKeys } from '@/i18n/keys'
import type { AppModule, ModulePage } from '@/app/module'
import { BuildingDoorIcon, TagIcon } from '@/components/icons'
import { Gate } from '@/features/auth/components/Gate'
import { constraintsRequirement } from '@/features/space-management/constraintsRequirement'
import { HierarchyViewers, SpaceManagementPermissions } from '@/features/space-management/permissions'

const BUILDINGS = { path: '/admin/buildings', permission: HierarchyViewers.Buildings, landing: 10 } satisfies ModulePage
const FLOORS = { path: '/admin/buildings/:buildingId/floors', permission: HierarchyViewers.Floors } satisfies ModulePage
const SPACES = { path: '/admin/buildings/:buildingId/floors/:floorId/spaces', permission: HierarchyViewers.Spaces } satisfies ModulePage
const SPACE_TYPES = { path: '/admin/space-types', permission: SpaceManagementPermissions.SpaceTypes.Default, landing: 20 } satisfies ModulePage
/** A level's constraints page needs that level's own read grant, so it's picked from :level. */
const CONSTRAINTS = {
  path: '/admin/constraints/:level/:id',
  permission: (params) => constraintsRequirement(params.level),
} satisfies ModulePage

const RULES_DENIED_DETAIL: Partial<Record<string, keyof TextKeys>> = {
  building: 'App:DeniedRulesDetailBuilding',
  floor: 'App:DeniedRulesDetailFloor',
  space: 'App:DeniedRulesDetailSpace',
}

/** A level's constraints page needs that level's read permission; an unknown level is left
 * to the page, which already says so. */
function ConstraintsRoute({ children }: { children: ReactNode }) {
  const params = useParams<{ level: string }>()
  return (
    <Gate
      name={CONSTRAINTS.permission(params)}
      frame="shell"
      deniedTitle="App:DeniedRulesTitle"
      deniedDetail={(params.level && RULES_DENIED_DETAIL[params.level]) || 'App:DeniedRulesDetailOther'}
    >
      {children}
    </Gate>
  )
}

/** Space management: the Building → Floor → Space hierarchy, its rules and closures, and the space types. */
export const spaceManagementModule: AppModule = {
  name: 'space-management',
  pages: [BUILDINGS, FLOORS, SPACES, SPACE_TYPES, CONSTRAINTS],
  permissions: SpaceManagementPermissions,
  navGroups: [{ id: 'space-management', label: 'Nav:SpaceManagement', icon: BuildingDoorIcon, section: 'administration', order: 10 }],
  nav: [
    {
      to: BUILDINGS.path,
      label: 'Nav:Hierarchy',
      icon: BuildingDoorIcon,
      // Anyone who can see some level of the tree (read-only above their level).
      requirement: BUILDINGS.permission,
      section: 'administration',
      group: 'space-management',
      order: 10,
      isActive: (pathname) => pathname === BUILDINGS.path || pathname.startsWith(`${BUILDINGS.path}/`),
    },
    {
      to: SPACE_TYPES.path,
      label: 'Nav:SpaceTypes',
      icon: TagIcon,
      requirement: SPACE_TYPES.permission,
      section: 'administration',
      group: 'space-management',
      order: 20,
    },
  ],
  routes: () => (
    <>
      <Route
        lazy={() =>
          import('@/features/space-management/routes/SpaceManagementLayout').then(({ SpaceManagementLayout }) => ({
            element: (
              // Open to anyone who can see some level of the tree: the buildings above are read-only
              // to them, and each button still needs its own permission.
              <Gate name={BUILDINGS.permission} frame="shell" deniedTitle="App:DeniedHierarchyTitle" deniedDetail="App:DeniedHierarchyDetail">
                <SpaceManagementLayout />
              </Gate>
            ),
          }))
        }
      >
        <Route
          path={BUILDINGS.path}
          lazy={() =>
            import('@/features/space-management/routes/BuildingsListPage').then(({ BuildingsListPage }) => ({ Component: BuildingsListPage }))
          }
        />
        <Route
          path={FLOORS.path}
          lazy={() =>
            import('@/features/space-management/routes/FloorsListPage').then(({ FloorsListPage }) => ({
              element: (
                <Gate name={FLOORS.permission} frame="main" deniedTitle="App:DeniedFloorsTitle" deniedDetail="App:DeniedFloorsDetail">
                  <FloorsListPage />
                </Gate>
              ),
            }))
          }
        />
        <Route
          path={SPACES.path}
          lazy={() =>
            import('@/features/space-management/routes/SpacesListPage').then(({ SpacesListPage }) => ({
              element: (
                <Gate name={SPACES.permission} frame="main" deniedTitle="App:DeniedSpacesTitle" deniedDetail="App:DeniedSpacesDetail">
                  <SpacesListPage />
                </Gate>
              ),
            }))
          }
        />
      </Route>
      {/* The old floors URL — keep bookmarks working. */}
      <Route path="/admin/floors" element={<Navigate to={BUILDINGS.path} replace />} />
      <Route
        path={SPACE_TYPES.path}
        lazy={() =>
          import('@/features/space-management/routes/SpaceTypesPage').then(({ SpaceTypesPage }) => ({
            element: (
              <Gate name={SPACE_TYPES.permission} frame="shell" deniedTitle="App:DeniedSpaceTypesTitle" deniedDetail="App:DeniedSpaceTypesDetail">
                <SpaceTypesPage />
              </Gate>
            ),
          }))
        }
      />
      <Route
        path={CONSTRAINTS.path}
        lazy={() =>
          import('@/features/space-management/routes/AdminConstraintsPage').then(({ AdminConstraintsPage }) => ({
            element: (
              <ConstraintsRoute>
                <AdminConstraintsPage />
              </ConstraintsRoute>
            ),
          }))
        }
      />
    </>
  ),
}
