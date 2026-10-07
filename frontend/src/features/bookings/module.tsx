import { Route } from 'react-router-dom'
import type { AppModule, ModulePage } from '@/app/module'
import { SearchIcon } from '@/components/icons'
import { Gate } from '@/features/auth/components/Gate'
import { BookingsFlows, BookingsPermissions } from '@/features/bookings/permissions'
import { loadOnce, myBuildingLoader } from '@/features/bookings/myBuildingLoader'

const FIND_SPACE = { path: '/find-space', permission: BookingsFlows.FindSpace, landing: 50 } satisfies ModulePage

/** Find a space: what can I book for this time? */
export const bookingsModule: AppModule = {
  name: 'bookings',
  pages: [FIND_SPACE],
  permissions: BookingsPermissions,
  nav: [
    {
      to: FIND_SPACE.path,
      label: 'Nav:FindSpace',
      icon: SearchIcon,
      requirement: FIND_SPACE.permission,
      section: 'bookings',
      order: 20,
    },
  ],
  shellRoutes: ({ queryClient }) => (
    <Route
      path={FIND_SPACE.path}
      loader={myBuildingLoader(queryClient)}
      shouldRevalidate={loadOnce}
      lazy={() =>
        import('@/features/bookings/routes/FindSpacePage').then(({ FindSpacePage }) => ({
          element: (
            <Gate name={FIND_SPACE.permission} deniedTitle="App:DeniedFindSpaceTitle" deniedDetail="App:DeniedFindSpaceDetail">
              <FindSpacePage />
            </Gate>
          ),
        }))
      }
    />
  ),
}
