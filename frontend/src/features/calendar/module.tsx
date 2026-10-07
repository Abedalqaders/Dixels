import { Route } from 'react-router-dom'
import type { AppModule, ModulePage } from '@/app/module'
import { CalendarLinesIcon } from '@/components/icons'
import { Gate } from '@/features/auth/components/Gate'
import { BookingsPermissions } from '@/features/bookings/permissions'
import { loadOnce, myBuildingLoader } from '@/features/bookings/myBuildingLoader'

const MY_CALENDAR = { path: '/my-calendar', permission: BookingsPermissions.Bookings.Default, landing: 40 } satisfies ModulePage

/** My calendar: every booking I've made, as a Day, Week or Month. */
export const calendarModule: AppModule = {
  name: 'calendar',
  pages: [MY_CALENDAR],
  nav: [
    {
      to: MY_CALENDAR.path,
      label: 'Nav:MyCalendar',
      icon: CalendarLinesIcon,
      requirement: MY_CALENDAR.permission,
      section: 'bookings',
      order: 10,
    },
  ],
  shellRoutes: ({ queryClient }) => (
    <Route
      path={MY_CALENDAR.path}
      // The building's request starts now, beside the permissions check, not after it.
      loader={myBuildingLoader(queryClient)}
      shouldRevalidate={loadOnce}
      lazy={() =>
        import('@/features/calendar/routes/MyCalendarPage').then(({ MyCalendarPage }) => ({
          element: (
            <Gate name={MY_CALENDAR.permission} deniedTitle="App:DeniedMyCalendarTitle" deniedDetail="App:DeniedMyCalendarDetail">
              <MyCalendarPage />
            </Gate>
          ),
        }))
      }
    />
  ),
}
