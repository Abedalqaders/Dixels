import { createBrowserRouter, createRoutesFromElements, Navigate, Route, RouterProvider, useParams } from 'react-router-dom'
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

// Every page is gated by the permission its API needs (see DixelsPermissions.cs), so what
// someone can open follows their ABP grants — change a grant and the page follows it.

/** A level's constraints page needs that level's read permission; an unknown level is left
 * to the page, which already says so. */
function ConstraintsRoute() {
  const { level } = useParams<{ level: string }>()
  const known = level === 'building' || level === 'floor' || level === 'space'
  const permission = constraintsRequirement(level)
  return (
    <RequirePermission
      name={permission}
      frame="shell"
      deniedTitle="You can't view these rules"
      deniedDetail={`Viewing ${known ? `${level}s` : 'this level'} is needed to open its rules. Ask an administrator to add it to your role.`}
    >
      <AdminConstraintsPage />
    </RequirePermission>
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
            <RequirePermission
              name={ROUTE_REQUIREMENTS['/my-calendar']}
              deniedTitle="You don't have access to My calendar"
              deniedDetail="Your account doesn't have permission to view bookings. Ask an administrator if you think it should."
            >
              <MyCalendarPage />
            </RequirePermission>
          }
        />
        <Route
          path="/find-space"
          element={
            <RequirePermission
              name={ROUTE_REQUIREMENTS['/find-space']}
              deniedTitle="You can't book spaces"
              deniedDetail="Your account doesn't have permission to create bookings. Ask an administrator if you think it should."
            >
              <FindSpacePage />
            </RequirePermission>
          }
        />
      </Route>
      <Route
        element={
          // Open to anyone who can see some level of the tree: the buildings above are read-only
          // to them, and each button still needs its own permission.
          <RequirePermission
            name={ROUTE_REQUIREMENTS['/admin/buildings']}
            frame="shell"
            deniedTitle="You don't have access to the hierarchy"
            deniedDetail="Viewing buildings, floors or spaces is needed to open it. Ask an administrator to add Buildings, Floors or Spaces to your role."
          >
            <SpaceManagementLayout />
          </RequirePermission>
        }
      >
        <Route path="/admin/buildings" element={<BuildingsListPage />} />
        <Route
          path="/admin/buildings/:buildingId/floors"
          element={
            <RequirePermission
              name={ROUTE_REQUIREMENTS['/admin/buildings/:buildingId/floors']}
              frame="main"
              deniedTitle="You can't see this building's floors"
              deniedDetail="Viewing floors (or the spaces on them) is needed to open this page. Ask an administrator to add Floors or Spaces to your role."
            >
              <FloorsListPage />
            </RequirePermission>
          }
        />
        <Route
          path="/admin/buildings/:buildingId/floors/:floorId/spaces"
          element={
            <RequirePermission
              name={ROUTE_REQUIREMENTS['/admin/buildings/:buildingId/floors/:floorId/spaces']}
              frame="main"
              deniedTitle="You can't see this floor's spaces"
              deniedDetail="Viewing spaces is needed to open this page. Ask an administrator to add Spaces to your role."
            >
              <SpacesListPage />
            </RequirePermission>
          }
        />
      </Route>
      {/* Old admin URLs that have since moved — keep bookmarks working. */}
      <Route path="/admin/floors" element={<Navigate to="/admin/buildings" replace />} />
      <Route path="/admin/employees" element={<Navigate to="/admin/users" replace />} />
      <Route
        path="/admin/space-types"
        element={
          <RequirePermission
            name={ROUTE_REQUIREMENTS['/admin/space-types']}
            frame="shell"
            deniedTitle="You can't manage space types"
            deniedDetail="Viewing space types is needed to open this page. Ask an administrator to add Space types to your role."
          >
            <SpaceTypesPage />
          </RequirePermission>
        }
      />
      <Route path="/admin/constraints/:level/:id" element={<ConstraintsRoute />} />
      <Route
        path="/admin/users"
        element={
          <RequirePermission
            name={ROUTE_REQUIREMENTS['/admin/users']}
            frame="shell"
            deniedTitle="You can't manage users"
            deniedDetail="Viewing users is needed to open this page. Ask an administrator to add Identity management → User management to your role."
          >
            <AdminUsersPage />
          </RequirePermission>
        }
      />
    </>,
  ),
)

function App() {
  return <RouterProvider router={router} />
}

export default App
