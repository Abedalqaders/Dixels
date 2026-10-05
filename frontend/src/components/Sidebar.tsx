import { useEffect, useRef, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { PanelLeftCloseIcon, PanelLeftOpenIcon } from 'lucide-react'
import { Flows, HierarchyViewers, Permissions } from '@/features/auth/permissions/permissionNames'
import { usePermission } from '@/features/auth/permissions/usePermission'
import { useMediaQuery } from '@/hooks/useMediaQuery'
import { up } from '@/lib/breakpoints'
import { languageInfo } from '@/i18n'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import { BuildingDoorIcon, CalendarLinesIcon, MenuIcon, PeopleIcon, SearchIcon, TagIcon } from './icons'
import { AccountMenu } from './AccountMenu'
import { LanguageSwitcher } from './LanguageSwitcher'
import { ThemeToggle } from './ThemeToggle'
import logo from '@/assets/logo.png'

// Collapsed or not, remembered per browser. Read on the first render, so a collapsed
// sidebar never flashes open on a reload.
const COLLAPSED_KEY = 'dixels.sidebar'

function readCollapsed(): boolean {
  try {
    return globalThis.localStorage?.getItem(COLLAPSED_KEY) === 'collapsed'
  } catch {
    return false
  }
}

function saveCollapsed(collapsed: boolean) {
  try {
    if (collapsed) globalThis.localStorage?.setItem(COLLAPSED_KEY, 'collapsed')
    else globalThis.localStorage?.removeItem(COLLAPSED_KEY)
  } catch {
    // Storage blocked (private mode): it still collapses, just not after a reload.
  }
}

/**
 * In the collapsed rail only icons show, so each one names itself in a tooltip — on hover
 * and on keyboard focus — on the side facing the page. The label itself stays in the link
 * (visually hidden), so screen readers and tests still find it by name.
 */
function RailTip({ show, label, children }: { show: boolean; label: React.ReactNode; children: React.ReactElement }) {
  if (!show) return children
  return (
    <Tooltip>
      <TooltipTrigger asChild>{children}</TooltipTrigger>
      <TooltipContent side={languageInfo().dir === 'rtl' ? 'left' : 'right'} sideOffset={8}>
        {label}
      </TooltipContent>
    </Tooltip>
  )
}

type NavItemProps = {
  to: string
  active: boolean
  disabled?: boolean
  collapsed: boolean
  children: React.ReactNode
  icon: React.ReactNode
  onNavigate: () => void
}

// Pages that don't exist yet render as an inert row (same look, no
// navigation) instead of a dead link into an unbuilt route.
function NavItem({ to, active, disabled, collapsed, children, icon, onNavigate }: NavItemProps) {
  const className = `nav${active ? ' on' : ''}`
  if (disabled) {
    return (
      <RailTip show={collapsed} label={children}>
        <span className={className} style={{ cursor: 'default', opacity: 0.6 }}>
          {icon}
          <span className="navlabel">{children}</span>
        </span>
      </RailTip>
    )
  }
  return (
    <RailTip show={collapsed} label={children}>
      <Link className={className} to={to} onClick={onNavigate}>
        {icon}
        <span className="navlabel">{children}</span>
      </Link>
    </RailTip>
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
  const { t } = useTranslation()
  const location = useLocation()
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
  // Below lg (src/lib/breakpoints.ts) the sidebar is a drawer behind the menu button.
  const [mobileOpen, setMobileOpen] = useState(false)
  const closeMobile = () => setMobileOpen(false)
  const menuButtonRef = useRef<HTMLButtonElement>(null)
  const drawerRef = useRef<HTMLElement>(null)

  // Closed by any navigation (a link, Back, a redirect) and by widening past lg, where the
  // sidebar is docked again. Decided during render, not in an effect: nothing to flash.
  const docked = useMediaQuery(up('lg'), false)
  const [seenPath, setSeenPath] = useState(location.pathname)
  if (seenPath !== location.pathname) {
    setSeenPath(location.pathname)
    if (mobileOpen) setMobileOpen(false)
  }
  if (docked && mobileOpen) setMobileOpen(false)

  // Docked, the sidebar can shrink to an icon rail, giving the page the width back. The
  // drawer below lg is always full width; it opens and closes instead.
  const [collapsedChoice, setCollapsedChoice] = useState(readCollapsed)
  const collapsed = docked && collapsedChoice
  function toggleCollapsed() {
    saveCollapsed(!collapsedChoice)
    setCollapsedChoice(!collapsedChoice)
  }

  // While open it behaves like a dialog: Escape closes it, the page behind doesn't scroll,
  // focus starts on the first item and goes back to the menu button when it closes.
  useEffect(() => {
    if (!mobileOpen) return
    const drawer = drawerRef.current
    const menuButton = menuButtonRef.current
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    drawer?.querySelector<HTMLElement>('a, button')?.focus()

    function onKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') setMobileOpen(false)
    }
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('keydown', onKeyDown)
      document.body.style.overflow = previousOverflow
      // Only take focus back if it was in the drawer (or lost) — not from a page that
      // already moved it somewhere on purpose.
      const active = document.activeElement
      if (!active || active === document.body || drawer?.contains(active)) menuButton?.focus()
    }
  }, [mobileOpen])

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

  return (
    <>
      <button
        ref={menuButtonRef}
        type="button"
        className="menubtn"
        aria-label={mobileOpen ? t('Nav:CloseMenu') : t('Nav:OpenMenu')}
        aria-expanded={mobileOpen}
        aria-controls="app-sidebar"
        onClick={() => setMobileOpen((v) => !v)}
      >
        <MenuIcon />
      </button>
      {/* Docked, the language and theme switches sit in each page's TopBar; below lg they
          take the other end of the band the menu button is in. */}
      {!docked && (
        <div className="corner">
          <LanguageSwitcher className="topbtn" compact align="end" />
          <ThemeToggle />
        </div>
      )}
      <div className={`scrim${mobileOpen ? ' show' : ''}`} onClick={closeMobile} aria-hidden="true" />
      <TooltipProvider delayDuration={300}>
      <nav
        id="app-sidebar"
        ref={drawerRef}
        aria-label={t('Nav:Main')}
        className={`side${mobileOpen ? ' open' : ''}${collapsed ? ' collapsed' : ''}`}
      >
      <div className="brand">
        <img className="logo-img" src={logo} alt="Dixels" />
      </div>

      {labelSections && <div className="grp">{t('Nav:Bookings')}</div>}
      {canViewBookings && (
        <NavItem
          to="/my-calendar"
          active={location.pathname === '/my-calendar'}
          collapsed={collapsed}
          onNavigate={closeMobile}
          icon={<CalendarLinesIcon />}
        >
          {t('Nav:MyCalendar')}
        </NavItem>
      )}
      {canFindSpace && (
        <NavItem
          to="/find-space"
          active={location.pathname === '/find-space'}
          collapsed={collapsed}
          onNavigate={closeMobile}
          icon={<SearchIcon />}
        >
          {t('Nav:FindSpace')}
        </NavItem>
      )}

      {labelSections && showAdministration && <div className="grp">{t('Nav:Administration')}</div>}
      {showSpaceManagement && (
        <>
          <button
            type="button"
            className={`nav navtoggle${isSpaceManagementRoute ? ' on' : ''}`}
            aria-expanded={spaceManagementOpen}
            onClick={() => setSpaceManagementManualOpen(!spaceManagementOpen)}
          >
            <BuildingDoorIcon />
            {t('Nav:SpaceManagement')}
            <ChevronDownIcon />
          </button>
          <div className={`navsubwrap${spaceManagementOpen ? ' open' : ''}`}>
            <div className="navsubinner">
              <div className="navsub">
                {canHierarchy && (
                  <NavItem
                    to="/admin/buildings"
                    active={location.pathname === '/admin/buildings' || location.pathname.startsWith('/admin/buildings/')}
                    collapsed={collapsed}
          onNavigate={closeMobile}
                    icon={<BuildingDoorIcon />}
                  >
                    {t('Nav:Hierarchy')}
                  </NavItem>
                )}
                {canSpaceTypes && (
                  <NavItem
                    to="/admin/space-types"
                    active={location.pathname === '/admin/space-types'}
                    collapsed={collapsed}
          onNavigate={closeMobile}
                    icon={<TagIcon />}
                  >
                    {t('Nav:SpaceTypes')}
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
          collapsed={collapsed}
          onNavigate={closeMobile}
          icon={<PeopleIcon />}
        >
          {t('Nav:Users')}
        </NavItem>
      )}

      <div className="spacer"></div>
      {docked && (
        <RailTip show={collapsed} label={t('Nav:ExpandMenu')}>
          <button type="button" className="nav navcollapse" onClick={toggleCollapsed}>
            {collapsed ? <PanelLeftOpenIcon aria-hidden /> : <PanelLeftCloseIcon aria-hidden />}
            <span className="navlabel">{collapsed ? t('Nav:ExpandMenu') : t('Nav:CollapseMenu')}</span>
          </button>
        </RailTip>
      )}
      <AccountMenu />
      </nav>
      </TooltipProvider>
    </>
  )
}
