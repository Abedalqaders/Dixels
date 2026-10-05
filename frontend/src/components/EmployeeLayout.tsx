import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { AppErrorBoundary } from './AppErrorBoundary'
import '@/styles/tokens.css'
import '@/styles/base.css'

/**
 * The shell for the pages anyone signed in may have (My calendar, Find a space, My profile):
 * the sidebar stays mounted while the page beside it changes — the same idea as
 * SpaceManagementLayout on the admin side.
 */
export function EmployeeLayout() {
  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <AppErrorBoundary>
          <Outlet />
        </AppErrorBoundary>
      </div>
    </div>
  )
}
