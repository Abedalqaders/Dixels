import type { User } from 'oidc-client-ts'

// OpenIddict maps the "roles" scope onto a `role` claim. With one role it's
// a single string; with more than one, oidc-client-ts gives back an array -
// normalize both shapes here so callers don't have to care.
export function getRoles(user: User | null | undefined): string[] {
  const role = user?.profile.role
  if (!role) return []
  return Array.isArray(role) ? role : [role]
}

export function hasRole(user: User | null | undefined, roleName: string): boolean {
  return getRoles(user).includes(roleName)
}

// ABP's id_token doesn't include a `name` claim unless the user has a real
// display name set - our seeded accounts only have `preferred_username`
// (e.g. "admin"). Fall back through what's actually there instead of
// showing the raw `sub` GUID.
export function getDisplayName(user: User | null | undefined): string {
  return user?.profile.name ?? user?.profile.preferred_username ?? user?.profile.sub ?? 'there'
}
