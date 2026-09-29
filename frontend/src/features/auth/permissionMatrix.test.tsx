// @vitest-environment jsdom
/**
 * The permission map in one place: for every page and menu item, which grants must open it
 * and which must not. Each row mirrors what the backend's [Authorize] checks for the calls
 * that page makes first (see DixelsPermissions.cs and the app services), so a reviewer can
 * read the whole contract here, and a change to one side without the other fails a row.
 */
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { constraintsRequirement, ROUTE_REQUIREMENTS } from '@/app/routeRequirements'
import { Sidebar } from '@/components/Sidebar'
import { landingFor } from '@/features/auth/landing'
import { Permissions as P } from '@/features/auth/permissions/permissionNames'
import { satisfies } from '@/features/auth/permissions/usePermission'
import { granted, WithPermissions } from '@/test/permissions'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))

const grants = (names: readonly string[]) => Object.fromEntries(names.map((n) => [n, true]))

interface Row {
  /** A page path from ROUTE_REQUIREMENTS. */
  route: keyof typeof ROUTE_REQUIREMENTS
  /** The sidebar link that leads there, if there is one. */
  link?: string
  /** Grant sets that must open it — each is a real role someone could be given. */
  allows: string[][]
  /** Grant sets that must not: one grant short of what the page's API needs. */
  denies: string[][]
}

const MATRIX: Row[] = [
  {
    route: '/my-calendar',
    link: 'My calendar',
    allows: [[P.Bookings.Default]],
    denies: [[P.Bookings.Create], [P.Bookings.Cancel]],
  },
  {
    // Reads with Bookings.Default (availability service), books with Bookings.Create.
    route: '/find-space',
    link: 'Find a space',
    allows: [[P.Bookings.Default, P.Bookings.Create]],
    denies: [[P.Bookings.Create], [P.Bookings.Default]],
  },
  {
    // "See the path": whoever may see any level reads the buildings above it.
    route: '/admin/buildings',
    link: 'Hierarchy',
    allows: [[P.Buildings.Default], [P.Floors.Default], [P.Spaces.Default]],
    denies: [[P.Buildings.Edit], [P.Overrides.Default], [P.SpaceTypes.Default]],
  },
  {
    route: '/admin/buildings/:buildingId/floors',
    allows: [[P.Floors.Default], [P.Spaces.Default]],
    denies: [[P.Buildings.Default]],
  },
  {
    route: '/admin/buildings/:buildingId/floors/:floorId/spaces',
    allows: [[P.Spaces.Default]],
    denies: [[P.Floors.Default], [P.Buildings.Default]],
  },
  {
    route: '/admin/space-types',
    link: 'Space types',
    allows: [[P.SpaceTypes.Default]],
    denies: [[P.SpaceTypes.Create], [P.Spaces.Default]],
  },
  {
    route: '/admin/users',
    link: 'Users',
    allows: [[P.Identity.Users]],
    denies: [[P.Identity.UsersUpdate], [P.Bookings.Default]],
  },
]

function renderSidebar(names: string[]) {
  vi.mocked(useAuth).mockReturnValue({ user: { profile: { role: 'employee', preferred_username: 'x' } } } as unknown as ReturnType<
    typeof useAuth
  >)
  return render(
    <WithPermissions value={granted(...names)}>
      <MemoryRouter>
        <Sidebar />
      </MemoryRouter>
    </WithPermissions>,
  )
}

describe.each(MATRIX)('$route', ({ route, link, allows, denies }) => {
  it.each(allows)('opens for %j', (...names) => {
    expect(satisfies(grants(names), ROUTE_REQUIREMENTS[route])).toBe(true)
  })

  it.each(denies)('stays shut for %j', (...names) => {
    expect(satisfies(grants(names), ROUTE_REQUIREMENTS[route])).toBe(false)
  })

  if (link) {
    it('has a menu item exactly when the page opens', () => {
      for (const names of allows) {
        const { unmount } = renderSidebar(names)
        expect(screen.queryByRole('link', { name: link }), `${link} for ${names.join(', ')}`).toBeInTheDocument()
        unmount()
      }
      for (const names of denies) {
        const { unmount } = renderSidebar(names)
        expect(screen.queryByRole('link', { name: link }), `${link} for ${names.join(', ')}`).not.toBeInTheDocument()
        unmount()
      }
    })
  }
})

describe('landing', () => {
  it('only ever lands on a page the same grants open', () => {
    const everySet = MATRIX.flatMap((row) => [...row.allows, ...row.denies])
    for (const names of everySet) {
      const path = landingFor(grants(names))
      if (path === null) continue
      const requirement = ROUTE_REQUIREMENTS[path as keyof typeof ROUTE_REQUIREMENTS]
      expect(requirement, `landing path ${path} is gated`).toBeDefined()
      expect(satisfies(grants(names), requirement), `${names.join(', ')} → ${path}`).toBe(true)
    }
  })
})

describe('constraints page', () => {
  it("needs the read grant of the level it opens, and only that level's", () => {
    expect(satisfies(grants([P.Floors.Default]), constraintsRequirement('floor'))).toBe(true)
    expect(satisfies(grants([P.Buildings.Default]), constraintsRequirement('floor'))).toBe(false)
    expect(satisfies(grants([P.Spaces.Default]), constraintsRequirement('space'))).toBe(true)
    expect(satisfies(grants([P.Floors.Edit]), constraintsRequirement('floor'))).toBe(false)
  })
})
