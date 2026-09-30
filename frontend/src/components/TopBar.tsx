import type { ReactNode } from 'react'
import { useMediaQuery } from '@/hooks/useMediaQuery'
import { up } from '@/lib/breakpoints'
import { ThemeToggle } from './ThemeToggle'

/**
 * The bar across the top of every page: whatever the page puts in it on the left (a
 * building picker, say), the theme toggle on the right. Below lg the toggle moves to the
 * fixed band beside the menu button instead (see Sidebar), so a bar with nothing else in
 * it isn't drawn there at all.
 */
export function TopBar({ children }: { children?: ReactNode }) {
  const docked = useMediaQuery(up('lg'), false)
  if (!docked && !children) return null

  return (
    <div className="top">
      {children}
      {docked && <ThemeToggle />}
    </div>
  )
}
