import type { AppModule, ModulePage, NavGroup, NavLink } from './module'
import { bookingsModule } from '@/features/bookings/module'
import { calendarModule } from '@/features/calendar/module'
import { profileModule } from '@/features/profile/module'
import { spaceManagementModule } from '@/features/space-management/module'
import { usersModule } from '@/features/users/module'

/**
 * The app's feature modules — what it's built from (see module.ts). A new module goes in
 * its own folder (features/<feature>/module.tsx) and gets one line here; registry.test.ts
 * checks it hangs together (routes ↔ pages, menu ↔ pages, the permission matrix).
 */
export const appModules: AppModule[] = [calendarModule, bookingsModule, profileModule, spaceManagementModule, usersModule]

export const appPages: ModulePage[] = appModules.flatMap((m) => m.pages)

export const appNav: { links: NavLink[]; groups: NavGroup[] } = {
  links: appModules.flatMap((m) => m.nav ?? []),
  groups: appModules.flatMap((m) => m.navGroups ?? []),
}

/** Every permission name the modules use, for the development check against the backend's list. */
export const appPermissionNames: string[] = appModules.flatMap((m) =>
  Object.values(m.permissions ?? {}).flatMap((group) => Object.values(group)),
)
