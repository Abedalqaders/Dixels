/** Users' query keys. See lib/api/queryKeys. */
export const usersKeys = {
  users: {
    all: ['users'] as const,
    list: (params: unknown) => ['users', 'list', params] as const,
    roleNames: () => ['users', 'role-names'] as const,
  },
}
