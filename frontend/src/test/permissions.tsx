import type { ReactNode } from 'react'
import { PermissionsContext } from '@/features/auth/permissions/permissionsContext'
import type { PermissionsValue } from '@/features/auth/permissions/permissionsContext'

/** A permissions state with exactly these grants loaded — for rendering a page as a given user. */
export function granted(...names: string[]): PermissionsValue {
  return { status: 'success', granted: Object.fromEntries(names.map((n) => [n, true])), retry: () => {} }
}

/** Renders `children` as a user holding `value` (see granted), without fetching anything. */
export function WithPermissions({ value, children }: { value: PermissionsValue; children: ReactNode }) {
  return <PermissionsContext.Provider value={value}>{children}</PermissionsContext.Provider>
}
