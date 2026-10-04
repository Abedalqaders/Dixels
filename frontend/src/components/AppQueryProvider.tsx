import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'
import type { QueryClient } from '@tanstack/react-query'
import { createQueryClient, registerQueryClient } from '@/lib/api/queryClient'
import i18n from '@/i18n'

/**
 * Mounts the shared query cache. One instance for the app's lifetime (created once, not
 * per render), and registered so the plain "bookings changed" / "hierarchy changed"
 * signals can invalidate it without being React hooks themselves.
 */
export function AppQueryProvider({ client, children }: { client?: QueryClient; children: ReactNode }) {
  const [own] = useState(() => client ?? createQueryClient())
  const active = client ?? own

  useEffect(() => {
    registerQueryClient(active)
    return () => registerQueryClient(undefined)
  }, [active])

  // Data can carry text in the language it was fetched in (names, messages from the
  // server), so a language change refetches whatever is on screen.
  useEffect(() => {
    const refetch = () => void active.invalidateQueries()
    i18n.on('languageChanged', refetch)
    return () => i18n.off('languageChanged', refetch)
  }, [active])

  return <QueryClientProvider client={active}>{children}</QueryClientProvider>
}
