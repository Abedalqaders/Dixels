/**
 * The module registry hangs together: every page a module serves is declared with who may open
 * it, every menu item leads to one of those pages under the same permission, every page with a
 * permission has rows in the permission matrix, and no two modules' names, pages or query keys
 * collide. A new module that misses a step fails here, naming it.
 */
import { isValidElement } from 'react'
import { describe, expect, it } from 'vitest'
import { createRoutesFromElements, Navigate } from 'react-router-dom'
import type { RouteObject } from 'react-router-dom'
import { createQueryClient } from '@/lib/api/queryClient'
import { queryKeys } from '@/lib/api/queryKeys'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { landingFor } from '@/features/auth/landing'
import { matrixRows } from '@/test/permissionMatrix'
import type { AppModule } from './module'
import { appModules, appNav, appPages, appPermissionNames } from './modules'

const context = { queryClient: createQueryClient() }

/** Every route a module declares, flattened: its path (if any) and where it redirects to (if it does). */
function routesOf(module: AppModule) {
  const walk = (routes: RouteObject[]): { path?: string; redirectTo?: string }[] =>
    routes.flatMap((route) => {
      const element = route.element
      const redirectTo = isValidElement<{ to: string }>(element) && element.type === Navigate ? element.props.to : undefined
      return [{ path: route.path, redirectTo }, ...walk(route.children ?? [])]
    })
  return walk(
    createRoutesFromElements(
      <>
        {module.shellRoutes?.(context)}
        {module.routes?.(context)}
      </>,
    ),
  )
}

const staticPermission = (path: string) => {
  const page = appPages.find((p) => p.path === path)
  return page && typeof page.permission !== 'function' ? page.permission : undefined
}

const duplicates = (values: (string | number)[]) => values.filter((v, i) => values.indexOf(v) !== i)

describe.each(appModules)('module $name', (module) => {
  const routes = routesOf(module)
  const pagePaths = module.pages.map((p) => p.path)

  it('declares every page its routes serve (who may open it is decided, not forgotten)', () => {
    const served = routes.filter((r) => r.path && !r.redirectTo).map((r) => r.path!)
    expect(served.sort()).toEqual([...pagePaths].sort())
  })

  it("redirects only to pages it serves (old URLs keep landing where they did)", () => {
    for (const route of routes.filter((r) => r.redirectTo)) {
      expect(appPages.map((p) => p.path), `${route.path} → ${route.redirectTo}`).toContain(route.redirectTo)
    }
  })
})

describe('the registry', () => {
  it('has unique module names, page paths and landing priorities', () => {
    expect(duplicates(appModules.map((m) => m.name))).toEqual([])
    expect(duplicates(appPages.map((p) => p.path))).toEqual([])
    expect(duplicates(appPages.flatMap((p) => (p.landing === undefined ? [] : [p.landing])))).toEqual([])
  })

  it('has permission-matrix rows for every page with a permission, and none for pages that are gone', () => {
    const gated = appPages.filter((p) => p.permission !== null && typeof p.permission !== 'function').map((p) => p.path)
    const covered = matrixRows.map((r) => r.route)
    expect(gated.filter((path) => !covered.includes(path)), 'pages without rows').toEqual([])
    expect(covered.filter((path) => !gated.includes(path)), 'rows for unknown pages').toEqual([])
  })

  it('leads every menu item to a registered page, under that page’s own permission', () => {
    for (const link of appNav.links) {
      expect(staticPermission(link.to), `${link.to} is a page with a permission`).toBeDefined()
      expect(link.requirement, `${link.to}: menu and page agree`).toEqual(staticPermission(link.to))
    }
  })

  it('checks every menu item in the permission matrix ("shown exactly when the page opens")', () => {
    const rowsWithLink = matrixRows.filter((r) => r.link).map((r) => r.route)
    expect(appNav.links.map((l) => l.to).filter((to) => !rowsWithLink.includes(to))).toEqual([])
  })

  it('puts every grouped menu item in a group that exists', () => {
    const groups = appNav.groups.map((g) => g.id)
    expect(duplicates(groups)).toEqual([])
    for (const link of appNav.links.filter((l) => l.group)) expect(groups, link.to).toContain(link.group)
  })

  it('keeps the landing order: space management, space types, users, then the calendar and Find a space', () => {
    const order = appPages
      .filter((p) => p.landing !== undefined)
      .sort((a, b) => a.landing! - b.landing!)
      .map((p) => p.path)
    expect(order).toEqual(['/admin/buildings', '/admin/space-types', '/admin/users', '/my-calendar', '/find-space'])
    expect(landingFor({})).toBeNull()
  })

  it('knows every permission name exactly once, the same ones permissionNames.ts re-exports', () => {
    expect(duplicates(appPermissionNames)).toEqual([])
    const reexported = Object.values(Permissions).flatMap((group) => Object.values(group))
    expect([...appPermissionNames].sort()).toEqual([...reexported].sort())
  })
})

describe('query keys', () => {
  // Each feature's queryKeys.ts, every family in it, and the first segment of each of its keys.
  const files = import.meta.glob<Record<string, Record<string, Record<string, unknown>>>>('/src/features/*/queryKeys.ts', { eager: true })
  const families = Object.values(files).flatMap((file) => Object.values(file).flatMap((keys) => Object.entries(keys)))
  const rootsOf = (members: Record<string, unknown>) =>
    new Set(
      Object.values(members).map((member) => {
        const key = typeof member === 'function' ? (member as (...args: string[]) => unknown[])('x', 'x', 'x') : (member as unknown[])
        return key[0]
      }),
    )

  it('gives every family one root of its own, so invalidating a family by prefix never reaches another', () => {
    for (const [name, members] of families) expect([...rootsOf(members)], name).toHaveLength(1)
    const roots = families.map(([, members]) => [...rootsOf(members)][0] as string)
    expect(duplicates(roots)).toEqual([])
  })

  it('are all still reachable through lib/api/queryKeys', () => {
    expect(Object.keys(queryKeys).sort()).toEqual(families.map(([name]) => name).sort())
  })
})
