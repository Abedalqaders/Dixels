import { Fragment, useEffect, useRef, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { PanelLeftCloseIcon, PanelLeftOpenIcon } from 'lucide-react'
import { NAV_SECTIONS } from '@/app/module'
import type { NavGroup, NavLink } from '@/app/module'
import { useCan } from '@/features/auth/permissions/usePermission'
import type { PermissionRequirement } from '@/features/auth/permissions/usePermission'
import { useMediaQuery } from '@/hooks/useMediaQuery'
import { up } from '@/lib/breakpoints'
import { languageInfo } from '@/i18n'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import { MenuIcon } from './icons'
import { useNav } from './navContext'
import type { Nav } from './navContext'
import { AccountMenu } from './AccountMenu'
import { LanguageSwitcher } from './LanguageSwitcher'
import { ThemeToggle } from './ThemeToggle'
import { Logo } from '@/components/Logo'

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

type SectionEntry = { kind: 'link'; link: NavLink; order: number } | { kind: 'group'; group: NavGroup; links: NavLink[]; order: number }

const byOrder = (a: { order: number }, b: { order: number }) => a.order - b.order

/** The sections that have something this user may open, each with its links and groups in
 * order. A group shows when at least one of its links does. */
function navSections(nav: Nav, can: (required: PermissionRequirement) => boolean) {
  const allowed = nav.links.filter((link) => can(link.requirement))
  return NAV_SECTIONS.map((section) => {
    const links: SectionEntry[] = allowed
      .filter((link) => link.section === section.id && !link.group)
      .map((link) => ({ kind: 'link', link, order: link.order }))
    const groups: SectionEntry[] = nav.groups
      .filter((group) => group.section === section.id)
      .map((group) => ({ kind: 'group' as const, group, links: allowed.filter((link) => link.group === group.id).sort(byOrder), order: group.order }))
      .filter((entry) => entry.links.length > 0)
    return { ...section, entries: [...links, ...groups].sort(byOrder) }
  }).filter((section) => section.entries.length > 0)
}

export function Sidebar() {
  const { t } = useTranslation()
  const location = useLocation()
  // The entries come from the feature modules (each module.tsx's nav). Every item follows the
  // user's ABP grants, not their role — the same permission its page (and API) needs, so
  // taking a grant away takes the item with it.
  const can = useCan()
  const nav = useNav()
  const sections = navSections(nav, can)
  // Section labels only earn their place when there are two sections to tell apart —
  // an employee's menu stays a plain list.
  const labelSections = sections.length > 1
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

  // A group's links (Hierarchy/Space types under "Space management") fold out under one
  // toggle. No entry means "not explicitly toggled yet" — it then follows whether the current
  // route is one of the group's pages, so opening one always shows it expanded without a
  // click. Once the admin explicitly opens/closes it, that choice sticks regardless of route.
  const [groupsManualOpen, setGroupsManualOpen] = useState<Record<string, boolean>>({})
  const isActive = (link: NavLink) => (link.isActive ? link.isActive(location.pathname) : location.pathname === link.to)

  function renderLink(link: NavLink) {
    const Icon = link.icon
    return (
      <NavItem key={link.to} to={link.to} active={isActive(link)} collapsed={collapsed} onNavigate={closeMobile} icon={<Icon />}>
        {t(link.label)}
      </NavItem>
    )
  }

  function renderGroup(group: NavGroup, links: NavLink[]) {
    const Icon = group.icon
    // Any of its pages, allowed or not: the same rule as the toggle always had.
    const isGroupRoute = nav.links.some((link) => link.group === group.id && isActive(link))
    const open = groupsManualOpen[group.id] ?? isGroupRoute
    return (
      <Fragment key={group.id}>
        <button
          type="button"
          className={`nav navtoggle${isGroupRoute ? ' on' : ''}`}
          aria-expanded={open}
          onClick={() => setGroupsManualOpen((choices) => ({ ...choices, [group.id]: !open }))}
        >
          <Icon />
          {t(group.label)}
          <ChevronDownIcon />
        </button>
        <div className={`navsubwrap${open ? ' open' : ''}`}>
          <div className="navsubinner">
            <div className="navsub">{links.map(renderLink)}</div>
          </div>
        </div>
      </Fragment>
    )
  }

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
        <Logo className="logo-img" />
      </div>

      {sections.map((section) => (
        <Fragment key={section.id}>
          {labelSections && <div className="grp">{t(section.label)}</div>}
          {section.entries.map((entry) => (entry.kind === 'link' ? renderLink(entry.link) : renderGroup(entry.group, entry.links)))}
        </Fragment>
      ))}

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
