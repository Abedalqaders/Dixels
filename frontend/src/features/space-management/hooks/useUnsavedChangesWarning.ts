import { useEffect } from 'react'

/**
 * Warns before the tab closes, refreshes, or navigates via a typed URL while `isDirty`.
 * Doesn't cover in-app navigation (clicking another link) — that needs a data router's
 * `useBlocker`, which this app doesn't use (see `App.tsx`'s plain `<BrowserRouter>`); the
 * page's own back-link handles that narrower case itself with a plain confirm() instead.
 */
export function useUnsavedChangesWarning(isDirty: boolean): void {
  useEffect(() => {
    if (!isDirty) return

    function handleBeforeUnload(e: BeforeUnloadEvent) {
      e.preventDefault()
      e.returnValue = ''
    }

    window.addEventListener('beforeunload', handleBeforeUnload)
    return () => window.removeEventListener('beforeunload', handleBeforeUnload)
  }, [isDirty])
}
