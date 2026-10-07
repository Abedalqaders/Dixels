import type { ComponentType, ReactNode } from 'react'
import type { QueryClient } from '@tanstack/react-query'
import type { Params } from 'react-router-dom'
import type { TextKeys } from '@/i18n/keys'
import type { PermissionRequirement } from '@/features/auth/permissions/usePermission'

/**
 * A feature module: its pages, who may open them, its menu entries, its permissions and its
 * routes, in one place (features/<feature>/module.tsx). The app is built from the list in
 * modules.ts — App.tsx takes the routes, the sidebar the menu, the home page the landing
 * order — so adding a module is its own folder plus one line there.
 */
export interface AppModule {
  /** Unique; names the module in test failures. */
  name: string
  /** Every page its routes serve. registry.test.ts checks routes and pages match both ways. */
  pages: ModulePage[]
  nav?: NavLink[]
  navGroups?: NavGroup[]
  /** Its permission groups (features/<feature>/permissions.ts), checked against the backend's
   * own list in development. */
  permissions?: Record<string, Record<string, string>>
  /** Routes inside the employee shell: signed in, with the sidebar beside the page. */
  shellRoutes?: (context: RouteContext) => ReactNode
  /** Routes that draw their own frame (the admin pages). */
  routes?: (context: RouteContext) => ReactNode
}

/** What a module's routes get from the app. */
export interface RouteContext {
  /** For loaders that start a page's data early (see myBuildingLoader). */
  queryClient: QueryClient
}

export interface ModulePage {
  /** The route path, exactly as the route declares it. */
  path: string
  /**
   * What opening it needs: a permission (the page's <Gate>), a function of the URL's params
   * when the page picks it from there (a level's rules page), or null for anyone signed in.
   * The same value its <Gate> is given, so the two can't drift.
   */
  permission: PermissionRequirement | ((params: Params) => PermissionRequirement) | null
  /** Where it ranks as someone's home page after sign-in (lower first): the first one their
   * grants open. Leave out for pages nobody lands on. */
  landing?: number
}

export type NavSection = 'bookings' | 'administration'

/** The sidebar's sections, in order. Labelled only when more than one has items. */
export const NAV_SECTIONS: { id: NavSection; label: keyof TextKeys }[] = [
  { id: 'bookings', label: 'Nav:Bookings' },
  { id: 'administration', label: 'Nav:Administration' },
]

/** A sidebar link. Shown exactly when its page opens: `requirement` is that page's permission. */
export interface NavLink {
  to: string
  label: keyof TextKeys
  icon: ComponentType
  requirement: PermissionRequirement
  section: NavSection
  /** Position within its section (or group), lower first. */
  order: number
  /** A NavGroup id: the link sits in that group's fold-out. */
  group?: string
  /** When it shows as the current page. Default: the path is exactly `to`. */
  isActive?: (pathname: string) => boolean
}

/** A fold-out of links under one toggle ("Space management"). Shown when any of its links
 * is; open and highlighted while one of them is the current page, until toggled by hand. */
export interface NavGroup {
  id: string
  label: keyof TextKeys
  icon: ComponentType
  section: NavSection
  order: number
}
