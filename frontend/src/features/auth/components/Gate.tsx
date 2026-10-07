import type { ComponentProps } from 'react'
import { useTranslation } from 'react-i18next'
import type { TextKeys } from '@/i18n/keys'
import { RequirePermission } from './RequirePermission'

// Every page is gated by the permission its API needs (see DixelsPermissions.cs), so what
// someone can open follows their ABP grants — change a grant and the page follows it.

type GateProps = Omit<ComponentProps<typeof RequirePermission>, 'deniedTitle' | 'deniedDetail'> & {
  deniedTitle: keyof TextKeys
  deniedDetail: keyof TextKeys
}

/** RequirePermission with its "no access" texts given as keys. The route tree is built once,
 * when the app loads, so the texts are looked up here instead — in the language showing
 * when the page opens, and again after a language switch. */
export function Gate({ deniedTitle, deniedDetail, ...props }: GateProps) {
  const { t } = useTranslation()
  return <RequirePermission {...props} deniedTitle={t(deniedTitle)} deniedDetail={t(deniedDetail)} />
}
