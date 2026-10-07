/**
 * Each page's code is its own file, named after a hash of its contents, and a deploy replaces
 * them all. A tab opened before the deploy still asks for the old names — gone — the first
 * time it opens a page it hasn't visited yet. Reloading fetches the new version, which has
 * the page.
 *
 * Reloads at most once per RELOAD_WINDOW_MS (remembered in sessionStorage, which survives
 * the reload): if the new version fails the same way, the error shows instead of reloading
 * in a loop.
 */
const RELOAD_KEY = 'dixels.staleChunkReload'
export const RELOAD_WINDOW_MS = 10_000

let reloading = false

/** How each browser words "that module file couldn't be fetched". */
const STALE_CHUNK_MESSAGES = [
  'Failed to fetch dynamically imported module', // Chrome, Edge
  'error loading dynamically imported module', // Firefox
  'Importing a module script failed', // Safari
  'Unable to preload CSS', // Vite, a page's stylesheet
]

export function isStaleChunkError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error)
  return STALE_CHUNK_MESSAGES.some((m) => message.includes(m))
}

interface ReloadDeps {
  now?: number
  storage?: Pick<Storage, 'getItem' | 'setItem'>
  reload?: () => void
}

/** Reloads the page for the new version, unless it already did so moments ago. True when it reloads. */
export function reloadForNewVersion({
  now = Date.now(),
  storage = globalThis.sessionStorage,
  reload = () => globalThis.location.reload(),
}: ReloadDeps = {}): boolean {
  if (reloading) return true
  try {
    const last = Number(storage.getItem(RELOAD_KEY))
    if (last && now - last < RELOAD_WINDOW_MS) return false
    storage.setItem(RELOAD_KEY, String(now))
  } catch {
    // No storage (blocked): nothing would stop a loop, so show the error instead.
    return false
  }
  reloading = true
  reload()
  return true
}

/** A reload for the new version is under way: show "loading", not an error, until it happens. */
export function isReloadingForNewVersion(): boolean {
  return reloading
}

/** Tests only: forget a reload this module started. */
export function resetStaleChunkState(): void {
  reloading = false
}
