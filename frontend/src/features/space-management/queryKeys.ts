/** Space management's query keys: the hierarchy and the space types. See lib/api/queryKeys. */
export const spaceManagementKeys = {
  hierarchy: {
    all: ['hierarchy'] as const,
    buildings: (params: unknown) => ['hierarchy', 'buildings', params] as const,
    floors: (buildingId: string, params: unknown) => ['hierarchy', 'floors', buildingId, params] as const,
    spaces: (floorId: string, params: unknown) => ['hierarchy', 'spaces', floorId, params] as const,
    constraints: (level: string, id: string, withClosures: boolean) =>
      ['hierarchy', 'constraints', level, id, withClosures] as const,
    closures: (level: string, id: string, params: unknown) => ['hierarchy', 'closures', level, id, params] as const,
  },
  spaceTypes: {
    all: ['space-types'] as const,
    list: (params: unknown) => ['space-types', 'list', params] as const,
  },
}
