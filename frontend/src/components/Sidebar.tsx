import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { getDisplayName, hasRole } from '../auth/roles'
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
  const isAdmin = hasRole(auth.user, 'admin')
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
        <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round"><path d="M3 6h14M3 10h14M3 14h14" /></svg>
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
            icon={
              <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><rect x="2.5" y="3.5" width="15" height="13" rx="2" /><path d="M2.5 7.5h15M7 2v3M13 2v3" /></svg>
            }
          >
            Dashboard
          </NavItem>
          <NavItem
            to="/my-calendar"
            active={false}
            disabled
            onNavigate={closeMobile}
            icon={
              <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><rect x="2.5" y="3.5" width="15" height="13" rx="2" /><path d="M6 8h8M6 11.5h5" /></svg>
            }
          >
            My calendar
          </NavItem>
          <NavItem
            to="/find-space"
            active={false}
            disabled
            onNavigate={closeMobile}
            icon={
              <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round"><circle cx="9" cy="9" r="5.5" /><path d="M13 13l4 4" /></svg>
            }
          >
            Find a space
          </NavItem>
          <NavItem
            to="/history"
            active={false}
            disabled
            onNavigate={closeMobile}
            icon={
              <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round"><circle cx="10" cy="10" r="7.5" /><path d="M10 5.5V10l3 2" /></svg>
            }
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
            icon={
              <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M3 17V7l7-4 7 4v10z" /><path d="M8 17v-5h4v5" /></svg>
            }
          >
            Space management
          </NavItem>
          <NavItem
            to="/admin/employees"
            active={false}
            disabled
            onNavigate={closeMobile}
            icon={
              <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><circle cx="7.5" cy="6.5" r="2.8" /><path d="M2.5 16c.4-3.4 2.5-5.2 5-5.2s4.6 1.8 5 5.2" /><circle cx="14.5" cy="7.3" r="2.1" /><path d="M12.3 10.9c1.1-.4 2.3-.3 3.3.4 1.1.8 1.8 2.3 2 4.7" /></svg>
            }
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
          <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M8 3.5H5a1.5 1.5 0 0 0-1.5 1.5v10A1.5 1.5 0 0 0 5 16.5h3" /><path d="M12.5 13.5 16 10l-3.5-3.5M16 10H8" /></svg>
        </button>
      </div>
      </nav>
    </>
  )
}
