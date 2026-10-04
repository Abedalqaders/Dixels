import { Navigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { TextSkeleton } from '@/components/LoadingSkeletons'
import { AppShell } from '@/features/auth/components/AppShell'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { landingFor } from '@/features/auth/landing'
import { usePermissions } from '@/features/auth/permissions/usePermission'
import { ApiError } from '@/lib/api/httpClient'

/**
 * HOME_PATH: once the signed-in user's grants are in, sends them on to the first page they
 * can open (see landingFor). Someone granted none of them is told so here, rather than
 * landing on a page that only says "no access".
 */
export function LandingPage() {
  return (
    <RequireAuth>
      <Landing />
    </RequireAuth>
  )
}

function Landing() {
  const { t } = useTranslation()
  const permissions = usePermissions()

  if (permissions.status === 'success') {
    const path = landingFor(permissions.granted)
    if (path) return <Navigate to={path} replace />
  }

  return (
    <AppShell>
      <div className="content">
        {permissions.status === 'loading' && <TextSkeleton label={t('Access:Checking')} />}

        {permissions.status === 'error' && (
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
        )}

        {permissions.status === 'success' && (
          <Card className="mt-6 gap-1 p-6" role="status">
            <p className="font-medium">{t('Access:NothingSetUpTitle')}</p>
            <p className="text-sm text-muted-foreground">{t('Access:NothingSetUpDetail')}</p>
          </Card>
        )}
      </div>
    </AppShell>
  )
}
