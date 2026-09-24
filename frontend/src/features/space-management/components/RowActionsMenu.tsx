import { useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { MoreIcon } from './actionIcons'

// A single kebab button replacing what used to be 3-4 always-visible row icons —
// decluttered per row, with the same actions moved into a dropdown. Rendered via a portal
// into document.body (position: fixed, computed from the trigger's own bounding rect)
// rather than positioned relative to the row, since the tree scrolls (.tree has
// overflow-x: auto) and an absolutely-positioned panel would get clipped by that container
// the moment a row is anywhere near its bottom edge.

export interface RowMenuAction {
  label: string
  icon: ReactNode
  onClick: () => void
  disabled?: boolean
  destructive?: boolean
}

interface RowActionsMenuProps {
  label: string
  actions: RowMenuAction[]
}

export function RowActionsMenu({ label, actions }: RowActionsMenuProps) {
  const [open, setOpen] = useState(false)
  const [position, setPosition] = useState<{ top: number; left: number } | null>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const panelRef = useRef<HTMLDivElement>(null)

  function openMenu() {
    const rect = buttonRef.current?.getBoundingClientRect()
    if (!rect) return
    setPosition({ top: rect.bottom + 4, left: rect.right })
    setOpen(true)
  }

  useEffect(() => {
    if (!open) return

    function handlePointerDown(e: PointerEvent) {
      const target = e.target as Node
      if (buttonRef.current?.contains(target) || panelRef.current?.contains(target)) return
      setOpen(false)
    }
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') setOpen(false)
    }
    function close() {
      setOpen(false)
    }

    document.addEventListener('pointerdown', handlePointerDown)
    document.addEventListener('keydown', handleKeyDown)
    window.addEventListener('scroll', close, true)
    window.addEventListener('resize', close)
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown)
      document.removeEventListener('keydown', handleKeyDown)
      window.removeEventListener('scroll', close, true)
      window.removeEventListener('resize', close)
    }
  }, [open])

  return (
    <>
      <button
        ref={buttonRef}
        type="button"
        className="rowbtn"
        title={`${label} actions`}
        aria-label={`${label} actions`}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => (open ? setOpen(false) : openMenu())}
      >
        <MoreIcon />
      </button>
      {open &&
        position &&
        createPortal(
          <div
            ref={panelRef}
            className="rowmenu-panel"
            role="menu"
            style={{ position: 'fixed', top: position.top, left: position.left, transform: 'translateX(-100%)' }}
          >
            {actions.map((a) => (
              <button
                key={a.label}
                type="button"
                role="menuitem"
                className={`rowmenu-item${a.destructive ? ' destructive' : ''}`}
                disabled={a.disabled}
                onClick={() => {
                  setOpen(false)
                  a.onClick()
                }}
              >
                {a.icon}
                {a.label}
              </button>
            ))}
          </div>,
          document.body,
        )}
    </>
  )
}
