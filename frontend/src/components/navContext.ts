import { createContext, useContext } from 'react'
import type { NavGroup, NavLink } from '@/app/module'

export interface Nav {
  links: NavLink[]
  groups: NavGroup[]
}

/**
 * The sidebar's entries, from the module registry. Handed down rather than imported by the
 * sidebar: a module's routes use <Gate>, whose "no access" frame draws the sidebar, so the
 * sidebar importing the registry would be an import loop.
 */
export const NavContext = createContext<Nav | null>(null)

export function useNav(): Nav {
  const nav = useContext(NavContext)
  // Without it the sidebar would quietly show no links at all — fail loudly instead.
  if (!nav) throw new Error('The sidebar needs <NavContext.Provider> (App.tsx gives it the module registry’s nav).')
  return nav
}
