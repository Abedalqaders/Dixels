import { useContext } from 'react'
import { PermissionsContext } from './permissionsContext'

/**
 * One permission, several of which any one will do, or `{ allOf }` where every one is needed —
 * for a page whose API calls need more than one grant (Find a space reads with
 * Bookings.Default and books with Bookings.Create).
 */
export type PermissionRequirement = string | readonly string[] | { readonly allOf: readonly string[] }

/** The whole permissions state — for guards that must tell "still loading" from "denied". */
export function usePermissions() {
  return useContext(PermissionsContext)
}

/** Whether `granted` satisfies `required`: any one of a list, every one of an `allOf`. */
export function satisfies(granted: Record<string, boolean>, required: PermissionRequirement): boolean {
  if (typeof required === 'string') return granted[required] === true
  if ('allOf' in required) return required.allOf.length > 0 && required.allOf.every((name) => granted[name] === true)
  return required.some((name) => granted[name] === true)
}

/**
 * Whether the signed-in user holds `required` (see Permissions; a list means any of them).
 * False while the grants are still loading, so anything gated on it appears once they
 * arrive rather than flickering away. This only shapes the UI — the backend's [Authorize]
 * is what actually enforces it.
 */
export function usePermission(required: PermissionRequirement): boolean {
  const permissions = usePermissions()
  return permissions.status === 'success' && satisfies(permissions.granted, required)
}

/** A checker for many permissions at once — for lists of actions that each declare their own. */
export function useCan(): (required: PermissionRequirement | undefined) => boolean {
  const permissions = usePermissions()
  return (required) => required === undefined || (permissions.status === 'success' && satisfies(permissions.granted, required))
}
