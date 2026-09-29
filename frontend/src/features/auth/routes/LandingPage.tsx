import { Navigate } from 'react-router-dom'
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
  const permissions = usePermissions()

  if (permissions.status === 'success') {
    const path = landingFor(permissions.granted)
    if (path) return <Navigate to={path} replace />
  }

  return (
    <AppShell>
      <div className="content">
        {permissions.status === 'loading' && <TextSkeleton label="Checking your access…" />}

        {permissions.status === 'error' && (
          <Card className="mt-6 gap-3 p-6" role="alert">
            <div className="grid gap-1">
              <p className="font-medium">
                {permissions.error instanceof ApiError && permissions.error.isNetworkError ? "Couldn't reach the server" : "Couldn't check your access"}
              </p>
              <p className="text-sm text-muted-foreground">{permissions.error.message}</p>
            </div>
            <div>
              <Button variant="outline" onClick={permissions.retry}>
                Try again
              </Button>
            </div>
          </Card>
        )}

        {permissions.status === 'success' && (
          <Card className="mt-6 gap-1 p-6" role="status">
            <p className="font-medium">Nothing's been set up for your account yet</p>
            <p className="text-sm text-muted-foreground">
              You're signed in, but you haven't been given access to any page. Ask an administrator to set up your access.
            </p>
          </Card>
        )}
      </div>
    </AppShell>
  )
}
