import { PERMISSION_GROUP as GROUP } from '@/features/auth/permissions/group'

// Mirrors backend/src/Dixels.Application.Contracts/Permissions/DixelsPermissions.cs — keep the
// two in step. These are what ABP reports in `auth.grantedPolicies`, and what the backend's
// [Authorize(...)] attributes check, so a page gated here is gated by the same rule as its API.
export const BookingsPermissions = {
  Bookings: {
    Default: `${GROUP}.Bookings`,
    Create: `${GROUP}.Bookings.Create`,
    Cancel: `${GROUP}.Bookings.Cancel`,
    ManageAll: `${GROUP}.Bookings.ManageAll`,
  },
} as const

/**
 * Pages whose API needs more than one grant. Find a space reads the building and searches
 * with Bookings.Default (the availability service's class-level check) and books with
 * Bookings.Create — someone holding only Create would open the page and get a 403.
 */
export const BookingsFlows = {
  FindSpace: { allOf: [BookingsPermissions.Bookings.Default, BookingsPermissions.Bookings.Create] },
} as const
