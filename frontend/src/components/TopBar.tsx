import type { ReactNode } from 'react'
import { useMediaQuery } from '@/hooks/useMediaQuery'
import { up } from '@/lib/breakpoints'
import { Breadcrumbs } from './Breadcrumbs'
import type { Crumb } from './Breadcrumbs'
import { LanguageSwitcher } from './LanguageSwitcher'
import { ThemeToggle } from './ThemeToggle'

/**
 * The bar across the top of every page: the breadcrumbs, then whatever the page puts in it
 * (a building picker, say), and the language and theme switches at the far end. Below lg
 * the two switches move to the fixed band beside the menu button instead (see Sidebar), so
 * a bar with nothing else in it isn't drawn there at all.
 */
export function TopBar({ crumbs = [], children }: { crumbs?: Crumb[]; children?: ReactNode }) {
  const docked = useMediaQuery(up('lg'), false)
  if (!docked && !children && crumbs.length === 0) return null

  return (
    <div className="top">
      <Breadcrumbs items={crumbs} />
      {children}
      {docked && (
        <div className="topend">
          <LanguageSwitcher className="topbtn" align="end" />
          <ThemeToggle />
        </div>
      )}
    </div>
  )
}
