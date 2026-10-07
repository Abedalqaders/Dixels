import { useState } from 'react'
import type { ComponentProps, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import type { QueryClient } from '@tanstack/react-query'
import { createBrowserRouter, createRoutesFromElements, Navigate, Route, RouterProvider, useParams } from 'react-router-dom'
import type { TextKeys } from '@/i18n/keys'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { RequirePermission } from '@/features/auth/components/RequirePermission'
import { constraintsRequirement, ROUTE_REQUIREMENTS } from './routeRequirements'
import { RouteError, RouteLoading } from './RouteFallbacks'
import { HOME_PATH } from '@/features/auth/landing'
import { CallbackPage } from '@/features/auth/routes/CallbackPage'
import { LandingPage } from '@/features/auth/routes/LandingPage'
import { SignOutPage } from '@/features/auth/routes/SignOutPage'
import { HomePage } from '@/features/auth/routes/HomePage'
import { EmployeeLayout } from '@/components/EmployeeLayout'
import { loadOnce, myBuildingLoader } from '@/features/bookings/myBuildingLoader'

// Every page is gated by the permission its API needs (see DixelsPermissions.cs), so what
// someone can open follows their ABP grants — change a grant and the page follows it.
//
// Each page's code is downloaded the first time it's opened (`lazy`), so an employee never
// downloads the admin pages, and a deploy only re-downloads the pages that changed. The
// sign-in pages and the employee shell stay in the first download: everyone needs them.

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
function ConstraintsRoute({ children }: { children: ReactNode }) {
  const { level } = useParams<{ level: string }>()
  const permission = constraintsRequirement(level)
  return (
    <Gate
      name={permission}
      frame="shell"
      deniedTitle="App:DeniedRulesTitle"
      deniedDetail={(level && RULES_DENIED_DETAIL[level]) || 'App:DeniedRulesDetailOther'}
    >
      {children}
    </Gate>
  )
}

// A data router (not a plain <BrowserRouter>): pages can hold a navigation with useBlocker
// to ask about unsaved changes, routes can load their code lazily, and a loader can start a
// page's data before the page itself renders.
function appRoutes(queryClient: QueryClient) {
  return (
    // RouteLoading while the first page's code downloads; RouteError if it can't be (a tab
    // left open across a deploy asks for files that are gone — it reloads once).
    <Route HydrateFallback={RouteLoading} ErrorBoundary={RouteError}>
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
          // The building's request starts now, beside the permissions check, not after it.
          loader={myBuildingLoader(queryClient)}
          shouldRevalidate={loadOnce}
          lazy={() =>
            import('@/features/calendar/routes/MyCalendarPage').then(({ MyCalendarPage }) => ({
              element: (
                <Gate
                  name={ROUTE_REQUIREMENTS['/my-calendar']}
                  deniedTitle="App:DeniedMyCalendarTitle"
                  deniedDetail="App:DeniedMyCalendarDetail"
                >
                  <MyCalendarPage />
                </Gate>
              ),
            }))
          }
        />
        <Route
          path="/find-space"
          loader={myBuildingLoader(queryClient)}
          shouldRevalidate={loadOnce}
          lazy={() =>
            import('@/features/bookings/routes/FindSpacePage').then(({ FindSpacePage }) => ({
              element: (
                <Gate
                  name={ROUTE_REQUIREMENTS['/find-space']}
                  deniedTitle="App:DeniedFindSpaceTitle"
                  deniedDetail="App:DeniedFindSpaceDetail"
                >
                  <FindSpacePage />
                </Gate>
              ),
            }))
          }
        />
        {/* Everyone signed in has a profile: no permission beyond that. */}
        <Route
          path="/profile"
          lazy={() =>
            import('@/features/profile/routes/ProfilePage').then(({ ProfilePage }) => ({ element: <ProfilePage section="profile" /> }))
          }
        />
        <Route
          path="/profile/security"
          lazy={() =>
            import('@/features/profile/routes/ProfilePage').then(({ ProfilePage }) => ({ element: <ProfilePage section="security" /> }))
          }
        />
      </Route>
      <Route
        lazy={() =>
          import('@/features/space-management/routes/SpaceManagementLayout').then(({ SpaceManagementLayout }) => ({
            element: (
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
            ),
          }))
        }
      >
        <Route
          path="/admin/buildings"
          lazy={() =>
            import('@/features/space-management/routes/BuildingsListPage').then(({ BuildingsListPage }) => ({ Component: BuildingsListPage }))
          }
        />
        <Route
          path="/admin/buildings/:buildingId/floors"
          lazy={() =>
            import('@/features/space-management/routes/FloorsListPage').then(({ FloorsListPage }) => ({
              element: (
                <Gate
                  name={ROUTE_REQUIREMENTS['/admin/buildings/:buildingId/floors']}
                  frame="main"
                  deniedTitle="App:DeniedFloorsTitle"
                  deniedDetail="App:DeniedFloorsDetail"
                >
                  <FloorsListPage />
                </Gate>
              ),
            }))
          }
        />
        <Route
          path="/admin/buildings/:buildingId/floors/:floorId/spaces"
          lazy={() =>
            import('@/features/space-management/routes/SpacesListPage').then(({ SpacesListPage }) => ({
              element: (
                <Gate
                  name={ROUTE_REQUIREMENTS['/admin/buildings/:buildingId/floors/:floorId/spaces']}
                  frame="main"
                  deniedTitle="App:DeniedSpacesTitle"
                  deniedDetail="App:DeniedSpacesDetail"
                >
                  <SpacesListPage />
                </Gate>
              ),
            }))
          }
        />
      </Route>
      {/* Old admin URLs that have since moved — keep bookmarks working. */}
      <Route path="/admin/floors" element={<Navigate to="/admin/buildings" replace />} />
      <Route path="/admin/employees" element={<Navigate to="/admin/users" replace />} />
      <Route
        path="/admin/space-types"
        lazy={() =>
          import('@/features/space-management/routes/SpaceTypesPage').then(({ SpaceTypesPage }) => ({
            element: (
              <Gate
                name={ROUTE_REQUIREMENTS['/admin/space-types']}
                frame="shell"
                deniedTitle="App:DeniedSpaceTypesTitle"
                deniedDetail="App:DeniedSpaceTypesDetail"
              >
                <SpaceTypesPage />
              </Gate>
            ),
          }))
        }
      />
      <Route
        path="/admin/constraints/:level/:id"
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
      <Route
        path="/admin/users"
        lazy={() =>
          import('@/features/users/routes/AdminUsersPage').then(({ AdminUsersPage }) => ({
            element: (
              <Gate
                name={ROUTE_REQUIREMENTS['/admin/users']}
                frame="shell"
                deniedTitle="App:DeniedUsersTitle"
                deniedDetail="App:DeniedUsersDetail"
              >
                <AdminUsersPage />
              </Gate>
            ),
          }))
        }
      />
    </Route>
  )
}

function App() {
  const queryClient = useQueryClient()
  // Created once, on the first render: a router made again on a later render would drop
  // where the app is. Made here rather than when this file loads because the loaders need
  // the app's query cache, which <AppQueryProvider> above has created by now.
  const [router] = useState(() => createBrowserRouter(createRoutesFromElements(appRoutes(queryClient))))
  return <RouterProvider router={router} />
}

export default App
