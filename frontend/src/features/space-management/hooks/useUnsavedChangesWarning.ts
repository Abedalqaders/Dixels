import { useEffect } from 'react'

/**
 * Warns before the tab closes, refreshes, or navigates via a typed URL while `isDirty`.
 * In-app navigation (clicking another link) is the data router's job: the page pairs this
 * with `useBlocker`, which holds the navigation and asks first.
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
