import { useState } from 'react'
import type { ReactNode } from 'react'
import { AppQueryProvider } from '@/components/AppQueryProvider'
import { Toaster } from '@/components/Toast'
import { createQueryClient } from '@/lib/api/queryClient'

/** What a page gets from main.tsx, for one test render: a fresh, non-retrying query cache
 * (nothing leaks between tests) and the toaster the page's confirmations show up in. */
export function TestProviders({ children }: { children: ReactNode }) {
  const [client] = useState(() => createQueryClient({ retry: false }))
  return (
    <AppQueryProvider client={client}>
      {children}
      <Toaster />
    </AppQueryProvider>
  )
}
