/**
 * Every server-state query key, in one place. A key names *what* the data is, never who
 * asked for it: the access token is not part of any key, so a silent token renew never
 * looks like "different data" and never refetches (or resets) a page on its own.
 *
 * Keys are hierarchical arrays so a whole family can be invalidated by its prefix:
 * `queryKeys.bookings.all` covers the building, every search and every opened booking.
 */
export const queryKeys = {
  bookings: {
    all: ['bookings'] as const,
    myBuilding: () => ['bookings', 'my-building'] as const,
    search: (input: unknown) => ['bookings', 'search', input] as const,
    spaceDays: (spaceId: string, from: string, to: string) => ['bookings', 'space-days', spaceId, from, to] as const,
    detail: (id: string | null) => ['bookings', 'detail', id] as const,
  },
  hierarchy: {
    all: ['hierarchy'] as const,
    buildings: (params: unknown) => ['hierarchy', 'buildings', params] as const,
    floors: (buildingId: string, params: unknown) => ['hierarchy', 'floors', buildingId, params] as const,
    spaces: (floorId: string, params: unknown) => ['hierarchy', 'spaces', floorId, params] as const,
    constraints: (level: string, id: string, withClosures: boolean) =>
      ['hierarchy', 'constraints', level, id, withClosures] as const,
  },
  spaceTypes: {
    all: ['space-types'] as const,
    list: (params: unknown) => ['space-types', 'list', params] as const,
  },
  profile: {
    me: () => ['profile', 'me'] as const,
  },
  users: {
    all: ['users'] as const,
    list: (params: unknown) => ['users', 'list', params] as const,
    roleNames: () => ['users', 'role-names'] as const,
  },
}
