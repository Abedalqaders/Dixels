import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import '../styles/tokens.css'
import '../styles/base.css'

/**
 * The shell for employee pages (Find a space, and My calendar next): the sidebar stays
 * mounted while the page beside it changes — the same idea as SpaceManagementLayout on
 * the admin side.
 */
export function EmployeeLayout() {
  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <Outlet />
      </div>
    </div>
  )
}
