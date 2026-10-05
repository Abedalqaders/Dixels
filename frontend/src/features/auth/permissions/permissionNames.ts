// Mirrors backend/src/Dixels.Application.Contracts/Permissions/DixelsPermissions.cs — keep the
// two in step. These are what ABP reports in `auth.grantedPolicies`, and what the backend's
// [Authorize(...)] attributes check, so a page gated here is gated by the same rule as its API.
const GROUP = 'Dixels'

export const Permissions = {
  Buildings: {
    Default: `${GROUP}.Buildings`,
    Create: `${GROUP}.Buildings.Create`,
    Edit: `${GROUP}.Buildings.Edit`,
    Delete: `${GROUP}.Buildings.Delete`,
  },
  Floors: {
    Default: `${GROUP}.Floors`,
    Create: `${GROUP}.Floors.Create`,
    Edit: `${GROUP}.Floors.Edit`,
    Delete: `${GROUP}.Floors.Delete`,
  },
  Spaces: {
    Default: `${GROUP}.Spaces`,
    Create: `${GROUP}.Spaces.Create`,
    Edit: `${GROUP}.Spaces.Edit`,
    Delete: `${GROUP}.Spaces.Delete`,
  },
  Overrides: {
    Default: `${GROUP}.Overrides`,
    Create: `${GROUP}.Overrides.Create`,
    Delete: `${GROUP}.Overrides.Delete`,
  },
  SpaceTypes: {
    Default: `${GROUP}.SpaceTypes`,
    Create: `${GROUP}.SpaceTypes.Create`,
    Edit: `${GROUP}.SpaceTypes.Edit`,
    Delete: `${GROUP}.SpaceTypes.Delete`,
  },
  Bookings: {
    Default: `${GROUP}.Bookings`,
    Create: `${GROUP}.Bookings.Create`,
    Cancel: `${GROUP}.Bookings.Cancel`,
    ManageAll: `${GROUP}.Bookings.ManageAll`,
  },
  // ABP's own: listing users and assigning their building go through ABP's user service.
  Identity: {
    Users: 'AbpIdentity.Users',
    UsersUpdate: 'AbpIdentity.Users.Update',
  },
} as const

/**
 * Who may open each part of the hierarchy — "see the path, act only where granted". It's a
 * tree: someone given floors (or spaces) must be able to see the buildings (and floors) above
 * to reach them, read-only; every button still needs its own permission. Mirrors
 * DixelsPermissions.Readers on the backend, which lets these users read that path.
 */
export const HierarchyViewers = {
  /** The hierarchy section and its buildings list: anyone who can see any level of it. */
  Buildings: [Permissions.Buildings.Default, Permissions.Floors.Default, Permissions.Spaces.Default],
  /** A building's floors: floor viewers, and space viewers on their way to the spaces. */
  Floors: [Permissions.Floors.Default, Permissions.Spaces.Default],
  Spaces: [Permissions.Spaces.Default],
} as const

export type HierarchyLevel = 'building' | 'floor' | 'space'

/** A hierarchy level's permissions. Its API needs Default to read, Create to add, Edit to
 * change details or constraints, and Delete to delete. */
export function hierarchyPermissions(level: HierarchyLevel) {
  if (level === 'building') return Permissions.Buildings
  if (level === 'floor') return Permissions.Floors
  return Permissions.Spaces
}

/**
 * Pages whose API needs more than one grant. Find a space reads the building and searches
 * with Bookings.Default (the availability service's class-level check) and books with
 * Bookings.Create — someone holding only Create would open the page and get a 403.
 */
export const Flows = {
  FindSpace: { allOf: [Permissions.Bookings.Default, Permissions.Bookings.Create] },
} as const
