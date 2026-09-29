import { useSyncExternalStore } from 'react'
import { XIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

export type ToastKind = 'success' | 'error'

export interface ToastItem {
  id: number
  message: string
  kind: ToastKind
}

/** How long a toast stays without being touched. Errors get longer: they need reading. */
const DURATION_MS: Record<ToastKind, number> = { success: 4000, error: 8000 }

// One app-wide list: any page or dialog raises a toast, the single <Toaster /> at the root
// shows them, stacked, newest at the bottom. A module store rather than React context so
// that showing one needs no provider and survives the page that raised it unmounting
// (e.g. a "Booked" toast raised just before navigating away).
let items: ToastItem[] = []
const listeners = new Set<() => void>()
const timers = new Map<number, ReturnType<typeof setTimeout>>()
let nextId = 1

function publish() {
  for (const listener of listeners) listener()
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

function snapshot() {
  return items
}

function armTimer(item: ToastItem) {
  clearTimeout(timers.get(item.id))
  timers.set(
    item.id,
    setTimeout(() => dismissToast(item.id), DURATION_MS[item.kind]),
  )
}

export function showToast(message: string, kind: ToastKind = 'success'): number {
  const item: ToastItem = { id: nextId++, message, kind }
  items = [...items, item]
  armTimer(item)
  publish()
  return item.id
}

export function dismissToast(id: number): void {
  clearTimeout(timers.get(id))
  timers.delete(id)
  if (!items.some((t) => t.id === id)) return
  items = items.filter((t) => t.id !== id)
  publish()
}

/** For tests: nothing carries over from one test to the next. */
export function clearToasts(): void {
  for (const timer of timers.values()) clearTimeout(timer)
  timers.clear()
  items = []
  publish()
}

/** A backstop, not the primary feedback mechanism — most invalid input is prevented
 * proactively by the pickers themselves (grey-out, real controls). This surfaces
 * whatever still reaches the backend, using its actual message, and confirms actions. */
export function useToast() {
  return { showToast, dismissToast }
}

/** Mounted once at the root (and in test wrappers). Bottom end corner, stacked, dismissible,
 * paused while the pointer is over a toast so it can be read; errors are announced
 * assertively, confirmations politely. */
export function Toaster() {
  const toasts = useSyncExternalStore(subscribe, snapshot, snapshot)
  if (toasts.length === 0) return null

  return (
    <div className="pointer-events-none fixed bottom-5 end-5 z-[60] flex w-[min(24rem,calc(100vw-2rem))] flex-col gap-2">
      {toasts.map((toast) => (
        <div
          key={toast.id}
          role={toast.kind === 'error' ? 'alert' : 'status'}
          className={cn(
            'toast show pointer-events-auto static flex items-start gap-2 opacity-100 transition-none',
            toast.kind === 'error' && 'error',
          )}
          onMouseEnter={() => clearTimeout(timers.get(toast.id))}
          onMouseLeave={() => armTimer(toast)}
        >
          <span className="flex-1">{toast.message}</span>
          <button
            type="button"
            aria-label="Dismiss"
            className="-me-1 rounded p-0.5 opacity-70 hover:opacity-100"
            onClick={() => dismissToast(toast.id)}
          >
            <XIcon aria-hidden="true" />
          </button>
        </div>
      ))}
    </div>
  )
}
