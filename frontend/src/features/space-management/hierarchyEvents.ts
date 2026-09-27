import { useEffect, useRef } from 'react'

// The explorer tree and the list pages fetch independently, so a page that changes the
// Building/Floor hierarchy (add, rename, delete, restore) announces it here and the tree
// reloads what it has on screen. A plain window event keeps the pages from needing a
// shared store just for this one signal.
const EVENT = 'dixels:space-hierarchy-changed'

export function notifyHierarchyChanged() {
  window.dispatchEvent(new Event(EVENT))
}

export function useHierarchyChanged(callback: () => void) {
  const callbackRef = useRef(callback)
  useEffect(() => {
    callbackRef.current = callback
  })

  useEffect(() => {
    const handler = () => callbackRef.current()
    window.addEventListener(EVENT, handler)
    return () => window.removeEventListener(EVENT, handler)
  }, [])
}
