import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'
import type { QueryClient } from '@tanstack/react-query'
import { createQueryClient, registerQueryClient } from '@/lib/api/queryClient'

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

  return <QueryClientProvider client={active}>{children}</QueryClientProvider>
}
