import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { TextSkeleton } from '@/components/LoadingSkeletons'
import { RequireAuth } from './RequireAuth'
import { AppShell } from './AppShell'
import { HOME_PATH } from '@/features/auth/landing'
import { satisfies, usePermissions } from '@/features/auth/permissions/usePermission'
import { ApiError } from '@/lib/api/httpClient'
import type { PermissionRequirement } from '@/features/auth/permissions/usePermission'

interface RequirePermissionProps {
  /** The permission the page needs — one of Permissions.*, or a list of which any will do. */
  name: PermissionRequirement
  /** What to tell someone who hasn't got it. */
  deniedTitle?: string
  deniedDetail?: string
  /** What the page draws around itself, so these messages sit where the page would:
   * 'shell' — its own sidebar and main column (most admin pages); 'main' — a main column
   * beside a layout's sidebar (the hierarchy pages). Omit inside a layout that has both. */
  frame?: 'shell' | 'main'
  children: ReactNode
}

// Wrap a route that needs a permission with <RequirePermission name={...}>. Builds on
// <RequireAuth> for the sign-in redirect, then waits for the user's grants. It doesn't
// redirect: someone who followed a bookmark or a link here is told why they can't use the
// page, in place — with the menu still there, so they can go somewhere they can.
export function RequirePermission(props: RequirePermissionProps) {
  return (
    <RequireAuth>
      <PermissionGate {...props} />
    </RequireAuth>
  )
}

function PermissionGate({ name, deniedTitle, deniedDetail, frame, children }: RequirePermissionProps) {
  const permissions = usePermissions()

  if (permissions.status === 'success' && satisfies(permissions.granted, name)) {
    return <>{children}</>
  }

  const message = <PermissionMessage deniedTitle={deniedTitle} deniedDetail={deniedDetail} />
  if (frame === 'shell') return <AppShell>{message}</AppShell>
  if (frame === 'main') return <div className="main">{message}</div>
  return message
}

function PermissionMessage({ deniedTitle, deniedDetail }: Pick<RequirePermissionProps, 'deniedTitle' | 'deniedDetail'>) {
  const { t } = useTranslation()
  const permissions = usePermissions()

  if (permissions.status === 'loading') {
    return (
      <div className="content">
        <TextSkeleton label={t('Access:Checking')} />
      </div>
    )
  }

  if (permissions.status === 'error') {
    return (
      <div className="content">
        <Card className="mt-6 gap-3 p-6" role="alert">
          <div className="grid gap-1">
            <p className="font-medium">
              {permissions.error instanceof ApiError && permissions.error.isNetworkError ? t('Access:Unreachable') : t('Access:CheckFailed')}
            </p>
            <p className="text-sm text-muted-foreground">{permissions.error.message}</p>
          </div>
          <div>
            <Button variant="outline" onClick={permissions.retry}>
              {t('Common:TryAgain')}
            </Button>
          </div>
        </Card>
      </div>
    )
  }

  return (
    <div className="content">
      <Card className="mt-6 gap-3 p-6" role="alert">
        <div className="grid gap-1">
          <p className="font-medium">{deniedTitle ?? t('Access:DeniedTitle')}</p>
          <p className="text-sm text-muted-foreground">{deniedDetail ?? t('Access:DeniedDetail')}</p>
        </div>
        <div>
          <Button asChild>
            <Link to={HOME_PATH}>{t('Common:GoHome')}</Link>
          </Button>
        </div>
      </Card>
    </div>
  )
}
