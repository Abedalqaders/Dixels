import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { getDisplayName } from '../auth/roles'
import { useAuthRole } from '../auth/useAuthRole'
import { BuildingDoorIcon, CalendarIcon, CalendarLinesIcon, ClockIcon, MenuIcon, PeopleIcon, SearchIcon, SignOutIcon } from './icons'
import logo from '../assets/logo.png'

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

export function Sidebar() {
  const auth = useAuth()
  const location = useLocation()
  const { isAdmin } = useAuthRole()
  const displayName = getDisplayName(auth.user)
  // Off-canvas drawer state - only visually relevant below the 860px
  // breakpoint in base.css; harmless (and unused by any visible control) on
  // wider screens where the sidebar is always shown.
  const [mobileOpen, setMobileOpen] = useState(false)
  const closeMobile = () => setMobileOpen(false)

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

      {!isAdmin && (
        <>
          <NavItem
            to="/dashboard"
            active={location.pathname === '/dashboard'}
            onNavigate={closeMobile}
            icon={<CalendarIcon />}
          >
            Dashboard
          </NavItem>
          <NavItem
            to="/my-calendar"
            active={false}
            disabled
            onNavigate={closeMobile}
            icon={<CalendarLinesIcon />}
          >
            My calendar
          </NavItem>
          <NavItem
            to="/find-space"
            active={false}
            disabled
            onNavigate={closeMobile}
            icon={<SearchIcon />}
          >
            Find a space
          </NavItem>
          <NavItem
            to="/history"
            active={false}
            disabled
            onNavigate={closeMobile}
            icon={<ClockIcon />}
          >
            History
          </NavItem>
        </>
      )}

      {isAdmin && (
        <>
          <NavItem
            to="/admin/buildings"
            active={location.pathname === '/admin/buildings'}
            onNavigate={closeMobile}
            icon={<BuildingDoorIcon />}
          >
            Space management
          </NavItem>
          <NavItem
            to="/admin/employees"
            active={false}
            disabled
            onNavigate={closeMobile}
            icon={<PeopleIcon />}
          >
            Employees
          </NavItem>
        </>
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
          onClick={() => auth.signoutRedirect()}
        >
          <SignOutIcon />
        </button>
      </div>
      </nav>
    </>
  )
}
