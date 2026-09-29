import { createContext } from 'react'

export type PermissionsState =
  | { status: 'loading' }
  | { status: 'success'; granted: Record<string, boolean> }
  | { status: 'error'; error: Error }

export type PermissionsValue = PermissionsState & { retry: () => void }

// Outside a <PermissionsProvider> nothing is granted yet — "loading" rather than "denied",
// so a guard waits instead of flashing an access-denied message.
export const PermissionsContext = createContext<PermissionsValue>({ status: 'loading', retry: () => {} })
