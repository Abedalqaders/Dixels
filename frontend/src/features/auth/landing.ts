import { appPages } from '@/app/modules'
import { satisfies } from '@/features/auth/permissions/usePermission'
import type { PermissionRequirement } from '@/features/auth/permissions/usePermission'

export { HOME_PATH } from './homePath'

interface LandingPage {
  permission: PermissionRequirement
  path: string
}

// In order of preference (each module page's `landing`): whoever can run space management
// lands there (as admins always have), everyone else on their own calendar. Picked by grant,
// not role, so taking a permission away never leaves someone landing on a page they can't open.
// Read when called, not when this file loads: the registry may still be loading then.
function landingPages(): LandingPage[] {
  return appPages
    .flatMap((page) =>
      page.landing === undefined || page.permission === null || typeof page.permission === 'function'
        ? []
        : [{ path: page.path, permission: page.permission, landing: page.landing }],
    )
    .sort((a, b) => a.landing - b.landing)
}

/** The first page these grants open, or null when they open none of them. */
export function landingFor(granted: Record<string, boolean>): string | null {
  return landingPages().find((page) => satisfies(granted, page.permission))?.path ?? null
}
