import { useEffect, useRef } from 'react'
import { getQueryClient } from '@/lib/api/queryClient'
import { queryKeys } from '@/lib/api/queryKeys'

// A page that changes the Building/Floor/Space hierarchy (add, rename, delete, restore)
// announces it here: every hierarchy query in the shared cache is invalidated, and the
// explorer tree — which pages its own branches — reloads what it has on screen.
const EVENT = 'dixels:space-hierarchy-changed'

export function notifyHierarchyChanged() {
  void getQueryClient()?.invalidateQueries({ queryKey: queryKeys.hierarchy.all })
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
