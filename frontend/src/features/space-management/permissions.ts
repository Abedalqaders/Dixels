import { PERMISSION_GROUP as GROUP } from '@/features/auth/permissions/group'

// Mirrors backend/src/Dixels.Application.Contracts/Permissions/DixelsPermissions.cs — keep the
// two in step. These are what ABP reports in `auth.grantedPolicies`, and what the backend's
// [Authorize(...)] attributes check, so a page gated here is gated by the same rule as its API.
export const SpaceManagementPermissions = {
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
} as const

const P = SpaceManagementPermissions

/**
 * Who may open each part of the hierarchy — "see the path, act only where granted". It's a
 * tree: someone given floors (or spaces) must be able to see the buildings (and floors) above
 * to reach them, read-only; every button still needs its own permission. Mirrors
 * DixelsPermissions.Readers on the backend, which lets these users read that path.
 */
export const HierarchyViewers = {
  /** The hierarchy section and its buildings list: anyone who can see any level of it. */
  Buildings: [P.Buildings.Default, P.Floors.Default, P.Spaces.Default],
  /** A building's floors: floor viewers, and space viewers on their way to the spaces. */
  Floors: [P.Floors.Default, P.Spaces.Default],
  Spaces: [P.Spaces.Default],
} as const

export type HierarchyLevel = 'building' | 'floor' | 'space'

/** A hierarchy level's permissions. Its API needs Default to read, Create to add, Edit to
 * change details or constraints, and Delete to delete. */
export function hierarchyPermissions(level: HierarchyLevel) {
  if (level === 'building') return P.Buildings
  if (level === 'floor') return P.Floors
  return P.Spaces
}
