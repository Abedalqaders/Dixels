import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { getDisplayName } from '@/features/auth/roles'
import { useAuthRole } from '@/features/auth/hooks/useAuthRole'
import { Flows, HierarchyViewers, Permissions } from '@/features/auth/permissions/permissionNames'
import { usePermission } from '@/features/auth/permissions/usePermission'
import { BuildingDoorIcon, CalendarLinesIcon, MenuIcon, PeopleIcon, SearchIcon, SignOutIcon, TagIcon } from './icons'
import logo from '@/assets/logo.png'

type NavItemProps = {
  to: string
  active: boolean
  disabled?: boolean
  children: React.ReactNode
  icon: React.ReactNode
  onNavigate: () => void
}

// Pages that don't exist yet render as an inert row (same look, no
// navigation) instead of a dead link into an unbuilt route.
function NavItem({ to, active, disabled, children, icon, onNavigate }: NavItemProps) {
  const className = `nav${active ? ' on' : ''}`
  if (disabled) {
    return (
      <span className={className} style={{ cursor: 'default', opacity: 0.6 }}>
        {icon}
        {children}
      </span>
    )
  }
  return (
    <Link className={className} to={to} onClick={onNavigate}>
      {icon}
      {children}
    </Link>
  )
}

function ChevronDownIcon() {
  return (
    <svg className="navchev" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M5.5 8 10 12.5 14.5 8" />
    </svg>
  )
}

export function Sidebar() {
  const auth = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const { isAdmin } = useAuthRole()
  // Every item follows the user's ABP grants, not their role — the same permission its page
  // (and API) needs, so taking a grant away takes the item with it.
  const canViewBookings = usePermission(Permissions.Bookings.Default)
  // Find a space needs both the read and the create grant (see Flows).
  const canFindSpace = usePermission(Flows.FindSpace)
  // Anyone who can see some level of the tree gets Hierarchy (read-only above their level).
  const canHierarchy = usePermission(HierarchyViewers.Buildings)
  const canSpaceTypes = usePermission(Permissions.SpaceTypes.Default)
  const canUsers = usePermission(Permissions.Identity.Users)
  const showBookings = canViewBookings || canFindSpace
  const showSpaceManagement = canHierarchy || canSpaceTypes
  const showAdministration = showSpaceManagement || canUsers
  // Section labels only earn their place when there are two sections to tell apart —
  // an employee's menu stays a plain list.
  const labelSections = showBookings && showAdministration
  const displayName = getDisplayName(auth.user)
  // Off-canvas drawer state - only visually relevant below the 860px
  // breakpoint in base.css; harmless (and unused by any visible control) on
  // wider screens where the sidebar is always shown.
  const [mobileOpen, setMobileOpen] = useState(false)
  const closeMobile = () => setMobileOpen(false)

  // Hierarchy/Space types collapse under one "Space management" toggle. `null` means
  // "not explicitly toggled yet" — it then follows whether the current route is one of the
  // two, so opening a space-management page always shows it expanded without a click.
  // Once the admin explicitly opens/closes it, that choice sticks regardless of route.
  const [spaceManagementManualOpen, setSpaceManagementManualOpen] = useState<boolean | null>(null)
  const isSpaceManagementRoute =
    location.pathname === '/admin/buildings' ||
    location.pathname.startsWith('/admin/buildings/') ||
    location.pathname === '/admin/space-types'
  const spaceManagementOpen = spaceManagementManualOpen ?? isSpaceManagementRoute

  const initials = displayName
    .split(/[\s._-]+/)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase()

  return (
    <>
      <button className="menubtn" aria-label="Toggle menu" onClick={() => setMobileOpen((v) => !v)}>
        <MenuIcon />
      </button>
      <div className={`scrim${mobileOpen ? ' show' : ''}`} onClick={closeMobile} />
      <nav className={`side${mobileOpen ? ' open' : ''}`}>
      <div className="brand">
        <img className="logo-img" src={logo} alt="Dixels" />
      </div>

      {labelSections && <div className="grp">Bookings</div>}
      {canViewBookings && (
        <NavItem
          to="/my-calendar"
          active={location.pathname === '/my-calendar'}
          onNavigate={closeMobile}
          icon={<CalendarLinesIcon />}
        >
          My calendar
        </NavItem>
      )}
      {canFindSpace && (
        <NavItem
          to="/find-space"
          active={location.pathname === '/find-space'}
          onNavigate={closeMobile}
          icon={<SearchIcon />}
        >
          Find a space
        </NavItem>
      )}

      {labelSections && showAdministration && <div className="grp">Administration</div>}
      {showSpaceManagement && (
        <>
          <button
            type="button"
            className={`nav navtoggle${isSpaceManagementRoute ? ' on' : ''}`}
            aria-expanded={spaceManagementOpen}
            onClick={() => setSpaceManagementManualOpen(!spaceManagementOpen)}
          >
            <BuildingDoorIcon />
            Space management
            <ChevronDownIcon />
          </button>
          <div className={`navsubwrap${spaceManagementOpen ? ' open' : ''}`}>
            <div className="navsubinner">
              <div className="navsub">
                {canHierarchy && (
                  <NavItem
                    to="/admin/buildings"
                    active={location.pathname === '/admin/buildings' || location.pathname.startsWith('/admin/buildings/')}
                    onNavigate={closeMobile}
                    icon={<BuildingDoorIcon />}
                  >
                    Hierarchy
                  </NavItem>
                )}
                {canSpaceTypes && (
                  <NavItem
                    to="/admin/space-types"
                    active={location.pathname === '/admin/space-types'}
                    onNavigate={closeMobile}
                    icon={<TagIcon />}
                  >
                    Space types
                  </NavItem>
                )}
              </div>
            </div>
          </div>
        </>
      )}
      {canUsers && (
        <NavItem
          to="/admin/users"
          active={location.pathname === '/admin/users'}
          onNavigate={closeMobile}
          icon={<PeopleIcon />}
        >
          Users
        </NavItem>
      )}

      <div className="spacer"></div>
      <div className="acct">
        <span className="av">{initials}</span>
        <span>
          <span className="nm">{displayName}</span>
          <br />
          <span className="rl">{isAdmin ? 'Administrator' : 'Employee'}</span>
        </span>
        <button
          className="out"
          title="Sign out"
          onClick={() => navigate('/signing-out')}
        >
          <SignOutIcon />
        </button>
      </div>
      </nav>
    </>
  )
}
