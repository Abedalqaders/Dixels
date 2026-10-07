import { Navigate, Route } from 'react-router-dom'
import type { AppModule, ModulePage } from '@/app/module'
import { PeopleIcon } from '@/components/icons'
import { Gate } from '@/features/auth/components/Gate'
import { UsersPermissions } from '@/features/users/permissions'

const USERS = { path: '/admin/users', permission: UsersPermissions.Identity.Users, landing: 30 } satisfies ModulePage

/** Users: who works in which building, and their roles. */
export const usersModule: AppModule = {
  name: 'users',
  pages: [USERS],
  permissions: UsersPermissions,
  nav: [
    {
      to: USERS.path,
      label: 'Nav:Users',
      icon: PeopleIcon,
      requirement: USERS.permission,
      section: 'administration',
      order: 20,
    },
  ],
  routes: () => (
    <>
      {/* The page's old URL — keep bookmarks working. */}
      <Route path="/admin/employees" element={<Navigate to={USERS.path} replace />} />
      <Route
        path={USERS.path}
        lazy={() =>
          import('@/features/users/routes/AdminUsersPage').then(({ AdminUsersPage }) => ({
            element: (
              <Gate name={USERS.permission} frame="shell" deniedTitle="App:DeniedUsersTitle" deniedDetail="App:DeniedUsersDetail">
                <AdminUsersPage />
              </Gate>
            ),
          }))
        }
      />
    </>
  ),
}
