// ABP's own: listing users and assigning their building go through ABP's user service.
export const UsersPermissions = {
  Identity: {
    Users: 'AbpIdentity.Users',
    UsersUpdate: 'AbpIdentity.Users.Update',
  },
} as const
