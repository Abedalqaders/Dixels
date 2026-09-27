import { useEffect, useId, useRef } from 'react'
import type { ReactNode } from 'react'

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

interface DialogProps {
  title: string
  subtitle?: ReactNode
  onClose: () => void
  children: ReactNode
  wide?: boolean
}

/**
 * A modal dialog on the shared `.overlay`/`.modal` styles, with what the hand-rolled
 * modals lack: it's announced as a dialog, keyboard focus stays inside it (Tab wraps),
 * Esc closes it, and focus goes back to whatever opened it.
 */
export function Dialog({ title, subtitle, onClose, children, wide }: DialogProps) {
  const titleId = useId()
  const panelRef = useRef<HTMLDivElement>(null)

  // Kept in a ref so the effect below runs once per open — a parent passing a fresh
  // arrow function each render must not re-run it (that would steal focus mid-typing).
  const onCloseRef = useRef(onClose)
  useEffect(() => {
    onCloseRef.current = onClose
  })

  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null
    const panel = panelRef.current
    const first = panel?.querySelector<HTMLElement>('[autofocus], ' + FOCUSABLE)
    first?.focus()

    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') {
        e.preventDefault()
        onCloseRef.current()
        return
      }
      if (e.key !== 'Tab' || !panel) return

      const focusable = Array.from(panel.querySelectorAll<HTMLElement>(FOCUSABLE))
      if (focusable.length === 0) return
      const firstEl = focusable[0]
      const lastEl = focusable[focusable.length - 1]

      if (e.shiftKey && document.activeElement === firstEl) {
        e.preventDefault()
        lastEl.focus()
      } else if (!e.shiftKey && document.activeElement === lastEl) {
        e.preventDefault()
        firstEl.focus()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      opener?.focus()
    }
  }, [])

  return (
    <div
      className="overlay show"
      onMouseDown={(e) => {
        // Only a press that starts on the backdrop itself closes — not a text selection
        // that began inside the panel and was released outside it.
        if (e.target === e.currentTarget) onClose()
      }}
    >
      <div
        ref={panelRef}
        className={`modal${wide ? ' wide' : ''}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
      >
        <h3 id={titleId}>{title}</h3>
        {subtitle && <p className="sub">{subtitle}</p>}
        {children}
      </div>
    </div>
  )
}
