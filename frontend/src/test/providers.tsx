import { useState } from 'react'
import type { ReactNode } from 'react'
import { appNav } from '@/app/modules'
import { AppQueryProvider } from '@/components/AppQueryProvider'
import { NavContext } from '@/components/navContext'
import { Toaster } from '@/components/Toast'
import { createQueryClient } from '@/lib/api/queryClient'

/** What a page gets from main.tsx and App.tsx, for one test render: a fresh, non-retrying
 * query cache (nothing leaks between tests), the toaster the page's confirmations show up in,
 * and the modules' sidebar entries. */
export function TestProviders({ children }: { children: ReactNode }) {
  const [client] = useState(() => createQueryClient({ retry: false }))
  return (
    <AppQueryProvider client={client}>
      <NavContext.Provider value={appNav}>{children}</NavContext.Provider>
      <Toaster />
    </AppQueryProvider>
  )
}
