import { useCallback, useEffect, useState } from 'react'

type ToastState = { message: string; kind: 'success' | 'error' } | null

/** A backstop, not the primary feedback mechanism — most invalid input is prevented
 * proactively by the pickers themselves (grey-out, real controls). This just surfaces
 * whatever still reaches the backend, using its actual message. */
export function useToast() {
  const [toast, setToast] = useState<ToastState>(null)

  const showToast = useCallback((message: string, kind: 'success' | 'error' = 'success') => {
    setToast({ message, kind })
  }, [])

  useEffect(() => {
    if (!toast) return
    const timer = setTimeout(() => setToast(null), 4000)
    return () => clearTimeout(timer)
  }, [toast])

  return { toast, showToast, dismissToast: () => setToast(null) }
}

export function Toast({ toast }: { toast: ToastState }) {
  if (!toast) return null

  return (
    <div className={`toast show${toast.kind === 'error' ? ' error' : ''}`} role="status">
      {toast.message}
    </div>
  )
}
