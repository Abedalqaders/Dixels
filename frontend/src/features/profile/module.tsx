import { Route } from 'react-router-dom'
import type { AppModule, ModulePage } from '@/app/module'

// Everyone signed in has a profile: no permission beyond that.
const PROFILE = { path: '/profile', permission: null } satisfies ModulePage
const SECURITY = { path: '/profile/security', permission: null } satisfies ModulePage

/** My profile: name, photo, and the password (Security). Reached from the account menu. */
export const profileModule: AppModule = {
  name: 'profile',
  pages: [PROFILE, SECURITY],
  shellRoutes: () => (
    <>
      <Route
        path={PROFILE.path}
        lazy={() =>
          import('@/features/profile/routes/ProfilePage').then(({ ProfilePage }) => ({ element: <ProfilePage section="profile" /> }))
        }
      />
      <Route
        path={SECURITY.path}
        lazy={() =>
          import('@/features/profile/routes/ProfilePage').then(({ ProfilePage }) => ({ element: <ProfilePage section="security" /> }))
        }
      />
    </>
  ),
}
