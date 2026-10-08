import { Fragment, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import type { QueryClient } from '@tanstack/react-query'
import { createBrowserRouter, createRoutesFromElements, Route, RouterProvider } from 'react-router-dom'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { RouteError, RouteLoading } from './RouteFallbacks'
import { appModules, appNav } from './modules'
import type { RouteContext } from './module'
import { HOME_PATH } from '@/features/auth/landing'
import { CallbackPage } from '@/features/auth/routes/CallbackPage'
import { LandingPage } from '@/features/auth/routes/LandingPage'
import { SignOutPage } from '@/features/auth/routes/SignOutPage'
import { HomePage } from '@/features/auth/routes/HomePage'
import { EmployeeLayout } from '@/components/EmployeeLayout'
import { NavContext } from '@/components/navContext'

// The pages come from the feature modules (modules.ts); each module's routes gate their page
// with the permission its API needs (see DixelsPermissions.cs), so what someone can open
// follows their ABP grants — change a grant and the page follows it.
//
// Each page's code is downloaded the first time it's opened (`lazy`), so an employee never
// downloads the admin pages, and a deploy only re-downloads the pages that changed. The
// sign-in pages and the employee shell stay in the first download: everyone needs them.

// A data router (not a plain <BrowserRouter>): pages can hold a navigation with useBlocker
// to ask about unsaved changes, routes can load their code lazily, and a loader can start a
// page's data before the page itself renders.
function appRoutes(queryClient: QueryClient) {
  const context: RouteContext = { queryClient }
  return (
    // RouteLoading while the first page's code downloads; RouteError if it can't be (a tab
    // left open across a deploy asks for files that are gone — it reloads once).
    <Route HydrateFallback={RouteLoading} ErrorBoundary={RouteError}>
      <Route path="/" element={<HomePage />} />
      <Route path="/callback" element={<CallbackPage />} />
      <Route path="/signing-out" element={<SignOutPage />} />
      <Route path={HOME_PATH} element={<LandingPage />} />
      {/* A guest's answer link from an invite email: public (the link is the proof), its own frame. */}
      <Route
        path="/rsvp/:token"
        lazy={() => import('@/features/guest-links/routes/RsvpPage').then(({ RsvpPage }) => ({ element: <RsvpPage /> }))}
      />
      {/* The pages anyone signed in may have: the sidebar beside the page. */}
      <Route
        element={
          <RequireAuth>
            <EmployeeLayout />
          </RequireAuth>
        }
      >
        {appModules.map((module) => (
          <Fragment key={module.name}>{module.shellRoutes?.(context)}</Fragment>
        ))}
      </Route>
      {/* The pages that draw their own frame (the admin pages). */}
      {appModules.map((module) => (
        <Fragment key={module.name}>{module.routes?.(context)}</Fragment>
      ))}
    </Route>
  )
}

function App() {
  const queryClient = useQueryClient()
  // Created once, on the first render: a router made again on a later render would drop
  // where the app is. Made here rather than when this file loads because the loaders need
  // the app's query cache, which <AppQueryProvider> above has created by now.
  const [router] = useState(() => createBrowserRouter(createRoutesFromElements(appRoutes(queryClient))))
  return (
    // The sidebar's entries, from the modules (see navContext for why it's handed down).
    <NavContext.Provider value={appNav}>
      <RouterProvider router={router} />
    </NavContext.Provider>
  )
}

export default App
