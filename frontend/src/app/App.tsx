import type { ComponentProps } from 'react'
import { useTranslation } from 'react-i18next'
import { createBrowserRouter, createRoutesFromElements, Navigate, Route, RouterProvider, useParams } from 'react-router-dom'
import type { TextKeys } from '@/i18n/keys'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { RequirePermission } from '@/features/auth/components/RequirePermission'
import { constraintsRequirement, ROUTE_REQUIREMENTS } from './routeRequirements'
import { HOME_PATH } from '@/features/auth/landing'
import { CallbackPage } from '@/features/auth/routes/CallbackPage'
import { LandingPage } from '@/features/auth/routes/LandingPage'
import { SignOutPage } from '@/features/auth/routes/SignOutPage'
import { BuildingsListPage } from '@/features/space-management/routes/BuildingsListPage'
import { FloorsListPage } from '@/features/space-management/routes/FloorsListPage'
import { SpacesListPage } from '@/features/space-management/routes/SpacesListPage'
import { SpaceManagementLayout } from '@/features/space-management/routes/SpaceManagementLayout'
import { SpaceTypesPage } from '@/features/space-management/routes/SpaceTypesPage'
import { AdminConstraintsPage } from '@/features/space-management/routes/AdminConstraintsPage'
import { AdminUsersPage } from '@/features/users/routes/AdminUsersPage'
import { HomePage } from '@/features/auth/routes/HomePage'
import { EmployeeLayout } from '@/components/EmployeeLayout'
import { FindSpacePage } from '@/features/bookings/routes/FindSpacePage'
import { MyCalendarPage } from '@/features/calendar/routes/MyCalendarPage'
import { ProfilePage } from '@/features/profile/routes/ProfilePage'

// Every page is gated by the permission its API needs (see DixelsPermissions.cs), so what
// someone can open follows their ABP grants — change a grant and the page follows it.

type GateProps = Omit<ComponentProps<typeof RequirePermission>, 'deniedTitle' | 'deniedDetail'> & {
  deniedTitle: keyof TextKeys
  deniedDetail: keyof TextKeys
}

/** RequirePermission with its "no access" texts given as keys. The route tree below is built
 * once, when the app loads, so the texts are looked up here instead — in the language
 * showing when the page opens, and again after a language switch. */
function Gate({ deniedTitle, deniedDetail, ...props }: GateProps) {
  const { t } = useTranslation()
  return <RequirePermission {...props} deniedTitle={t(deniedTitle)} deniedDetail={t(deniedDetail)} />
}

const RULES_DENIED_DETAIL: Partial<Record<string, keyof TextKeys>> = {
  building: 'App:DeniedRulesDetailBuilding',
  floor: 'App:DeniedRulesDetailFloor',
  space: 'App:DeniedRulesDetailSpace',
}

/** A level's constraints page needs that level's read permission; an unknown level is left
 * to the page, which already says so. */
function ConstraintsRoute() {
  const { level } = useParams<{ level: string }>()
  const permission = constraintsRequirement(level)
  return (
    <Gate
      name={permission}
      frame="shell"
      deniedTitle="App:DeniedRulesTitle"
      deniedDetail={(level && RULES_DENIED_DETAIL[level]) || 'App:DeniedRulesDetailOther'}
    >
      <AdminConstraintsPage />
    </Gate>
  )
}

// A data router (not a plain <BrowserRouter>): pages can hold a navigation with useBlocker
// to ask about unsaved changes. The routes are the same declarative tree as before.
export const router = createBrowserRouter(
  createRoutesFromElements(
    <>
      <Route path="/" element={<HomePage />} />
      <Route path="/callback" element={<CallbackPage />} />
      <Route path="/signing-out" element={<SignOutPage />} />
      <Route path={HOME_PATH} element={<LandingPage />} />
      <Route
        element={
          <RequireAuth>
            <EmployeeLayout />
          </RequireAuth>
        }
      >
        <Route
          path="/my-calendar"
          element={
            <Gate
              name={ROUTE_REQUIREMENTS['/my-calendar']}
              deniedTitle="App:DeniedMyCalendarTitle"
              deniedDetail="App:DeniedMyCalendarDetail"
            >
              <MyCalendarPage />
            </Gate>
          }
        />
        <Route
          path="/find-space"
          element={
            <Gate
              name={ROUTE_REQUIREMENTS['/find-space']}
              deniedTitle="App:DeniedFindSpaceTitle"
              deniedDetail="App:DeniedFindSpaceDetail"
            >
              <FindSpacePage />
            </Gate>
          }
        />
        {/* Everyone signed in has a profile: no permission beyond that. */}
        <Route path="/profile" element={<ProfilePage />} />
      </Route>
      <Route
        element={
          // Open to anyone who can see some level of the tree: the buildings above are read-only
          // to them, and each button still needs its own permission.
          <Gate
            name={ROUTE_REQUIREMENTS['/admin/buildings']}
            frame="shell"
            deniedTitle="App:DeniedHierarchyTitle"
            deniedDetail="App:DeniedHierarchyDetail"
          >
            <SpaceManagementLayout />
          </Gate>
        }
      >
        <Route path="/admin/buildings" element={<BuildingsListPage />} />
        <Route
          path="/admin/buildings/:buildingId/floors"
          element={
            <Gate
              name={ROUTE_REQUIREMENTS['/admin/buildings/:buildingId/floors']}
              frame="main"
              deniedTitle="App:DeniedFloorsTitle"
              deniedDetail="App:DeniedFloorsDetail"
            >
              <FloorsListPage />
            </Gate>
          }
        />
        <Route
          path="/admin/buildings/:buildingId/floors/:floorId/spaces"
          element={
            <Gate
              name={ROUTE_REQUIREMENTS['/admin/buildings/:buildingId/floors/:floorId/spaces']}
              frame="main"
              deniedTitle="App:DeniedSpacesTitle"
              deniedDetail="App:DeniedSpacesDetail"
            >
              <SpacesListPage />
            </Gate>
          }
        />
      </Route>
      {/* Old admin URLs that have since moved — keep bookmarks working. */}
      <Route path="/admin/floors" element={<Navigate to="/admin/buildings" replace />} />
      <Route path="/admin/employees" element={<Navigate to="/admin/users" replace />} />
      <Route
        path="/admin/space-types"
        element={
          <Gate
            name={ROUTE_REQUIREMENTS['/admin/space-types']}
            frame="shell"
            deniedTitle="App:DeniedSpaceTypesTitle"
            deniedDetail="App:DeniedSpaceTypesDetail"
          >
            <SpaceTypesPage />
          </Gate>
        }
      />
      <Route path="/admin/constraints/:level/:id" element={<ConstraintsRoute />} />
      <Route
        path="/admin/users"
        element={
          <Gate
            name={ROUTE_REQUIREMENTS['/admin/users']}
            frame="shell"
            deniedTitle="App:DeniedUsersTitle"
            deniedDetail="App:DeniedUsersDetail"
          >
            <AdminUsersPage />
          </Gate>
        }
      />
    </>,
  ),
)

function App() {
  return <RouterProvider router={router} />
}

export default App
