import type { ReactNode } from 'react'
import { Sidebar } from '@/components/Sidebar'
import { TopBar } from '@/components/TopBar'
import '@/styles/tokens.css'
import '@/styles/base.css'

/** The sidebar and a main column — for the auth screens shown in place of a page that
 * draws its own shell (the admin pages), so the menu is still there around them. */
export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <TopBar />
        {children}
      </div>
    </div>
  )
}
