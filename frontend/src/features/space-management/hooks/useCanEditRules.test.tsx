// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { renderHook } from '@testing-library/react'
import type { ReactNode } from 'react'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { granted, WithPermissions } from '@/test/permissions'
import { useCanEditRules } from './useCanEditRules'

function canEdit(level: 'building' | 'floor' | 'space', ...grants: string[]) {
  const wrapper = ({ children }: { children: ReactNode }) => <WithPermissions value={granted(...grants)}>{children}</WithPermissions>
  return renderHook(() => useCanEditRules(level), { wrapper }).result.current
}

describe('useCanEditRules', () => {
  it("the level's Edit permission is enough on its own", () => {
    expect(canEdit('floor', Permissions.Floors.Edit)).toBe(true)
    expect(canEdit('floor', Permissions.Buildings.Edit)).toBe(false)
  })

  it('closure permissions count only when the closures section is visible (Overrides.Default)', () => {
    expect(canEdit('building', Permissions.Overrides.Create)).toBe(false)
    expect(canEdit('building', Permissions.Overrides.Default)).toBe(false)
    expect(canEdit('building', Permissions.Overrides.Default, Permissions.Overrides.Create)).toBe(true)
    expect(canEdit('space', Permissions.Overrides.Default, Permissions.Overrides.Delete)).toBe(true)
  })
})
