// @vitest-environment jsdom
/**
 * The permission map: for every page and menu item, which grants must open it and which must
 * not. Each row mirrors what the backend's [Authorize] checks for the calls that page makes
 * first (see DixelsPermissions.cs and the app services), so a change to one side without the
 * other fails a row. The rows live with their features (features/<feature>/permissionMatrix.ts);
 * src/app/registry.test.ts checks every page and menu item has one.
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
import { matrixRows } from '@/test/permissionMatrix'
import { granted, WithPermissions } from '@/test/permissions'
import { TestProviders } from '@/test/providers'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))

const grants = (names: readonly string[]) => Object.fromEntries(names.map((n) => [n, true]))

function renderSidebar(names: string[]) {
  vi.mocked(useAuth).mockReturnValue({ user: { profile: { role: 'employee', preferred_username: 'x' } } } as unknown as ReturnType<
    typeof useAuth
  >)
  return render(
    <TestProviders>
      <WithPermissions value={granted(...names)}>
        <MemoryRouter>
          <Sidebar />
        </MemoryRouter>
      </WithPermissions>
    </TestProviders>,
  )
}

describe.each(matrixRows)('$route', ({ route, link, allows, denies }) => {
  const requirement = ROUTE_REQUIREMENTS[route]

  it('is a page with a permission', () => {
    expect(requirement).toBeDefined()
  })

  it.each(allows)('opens for %j', (...names) => {
    expect(satisfies(grants(names), requirement)).toBe(true)
  })

  it.each(denies)('stays shut for %j', (...names) => {
    expect(satisfies(grants(names), requirement)).toBe(false)
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
    const everySet = matrixRows.flatMap((row) => [...row.allows, ...row.denies])
    for (const names of everySet) {
      const path = landingFor(grants(names))
      if (path === null) continue
      const requirement = ROUTE_REQUIREMENTS[path]
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
