import { bookingsKeys } from '@/features/bookings/queryKeys'
import { profileKeys } from '@/features/profile/queryKeys'
import { spaceManagementKeys } from '@/features/space-management/queryKeys'
import { usersKeys } from '@/features/users/queryKeys'

/**
 * Every server-state query key. A key names *what* the data is, never who asked for it: the
 * access token is not part of any key, so a silent token renew never looks like "different
 * data" and never refetches (or resets) a page on its own.
 *
 * Keys are hierarchical arrays so a whole family can be invalidated by its prefix:
 * `queryKeys.bookings.all` covers the building, every search and every opened booking.
 *
 * Each feature keeps its own families (features/<feature>/queryKeys.ts); this gathers the
 * existing ones under the name the app has always imported. A new module's pages import its
 * own queryKeys.ts directly. Every family's first segment must be its own:
 * src/app/registry.test.ts checks no two families share one.
 */
export const queryKeys = {
  ...bookingsKeys,
  ...spaceManagementKeys,
  ...profileKeys,
  ...usersKeys,
}
