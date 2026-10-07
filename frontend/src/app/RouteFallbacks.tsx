import { useTranslation } from 'react-i18next'
import { useRouteError } from 'react-router-dom'
import { ErrorMessage } from '@/components/AppErrorBoundary'
import { AuthStatusScreen } from '@/features/auth/components/AuthStatusScreen'
import { isReloadingForNewVersion, isStaleChunkError, reloadForNewVersion } from '@/lib/staleChunk'

/** While the first page's code downloads (pages load on first visit — see App.tsx). */
export function RouteLoading() {
  const { t } = useTranslation()
  return <AuthStatusScreen state="busy" title={t('Nav:BreadcrumbLoading')} />
}

/**
 * Anything a route throws that its page's own boundary didn't catch — chiefly a page's code
 * failing to download. After a deploy that's an old file name; reloading gets the new one.
 */
export function RouteError() {
  const error = useRouteError()
  if (isReloadingForNewVersion() || (isStaleChunkError(error) && reloadForNewVersion())) return <RouteLoading />
  return <ErrorMessage error={error instanceof Error ? error : new Error(String(error))} scope="app" />
}
