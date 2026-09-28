import { Outlet } from 'react-router-dom'
import { Sidebar } from '@/components/Sidebar'
import { SpaceExplorer } from '@/features/space-management/components/SpaceExplorer'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/admin.css'

// Shared shell for the Buildings/Floors/Spaces pages: app nav, the Building → Floor
// explorer, then the page itself. Being a parent route is what keeps the explorer mounted
// across those pages, so its expanded buildings and loaded floors survive navigation
// instead of refetching on every click.
export function SpaceManagementLayout() {
  return (
    <div className="app">
      <Sidebar />
      <SpaceExplorer />
      <Outlet />
    </div>
  )
}
