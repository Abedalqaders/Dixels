import { beforeEach, describe, expect, it, vi } from 'vitest'
import { isStaleChunkError, reloadForNewVersion, RELOAD_WINDOW_MS, resetStaleChunkState } from './staleChunk'

function memoryStorage() {
  const items = new Map<string, string>()
  return { getItem: (k: string) => items.get(k) ?? null, setItem: (k: string, v: string) => void items.set(k, v) }
}

describe('isStaleChunkError', () => {
  it.each([
    'Failed to fetch dynamically imported module: https://app/assets/AdminUsersPage-abc.js',
    'error loading dynamically imported module: https://app/assets/x.js',
    'Importing a module script failed.',
    'Unable to preload CSS for /assets/admin-abc.css',
  ])('knows %s', (message) => {
    expect(isStaleChunkError(new TypeError(message))).toBe(true)
  })

  it('leaves other errors alone', () => {
    expect(isStaleChunkError(new Error('Cannot read properties of undefined'))).toBe(false)
  })
})

describe('reloadForNewVersion', () => {
  beforeEach(() => resetStaleChunkState())

  it('reloads the first time', () => {
    const reload = vi.fn()
    expect(reloadForNewVersion({ now: 1_000_000, storage: memoryStorage(), reload })).toBe(true)
    expect(reload).toHaveBeenCalledOnce()
  })

  it("doesn't reload again right after a reload (the new version failed too): no loop", () => {
    const storage = memoryStorage()
    reloadForNewVersion({ now: 1_000_000, storage, reload: vi.fn() })
    resetStaleChunkState() // what the reload itself does: a fresh page

    const reload = vi.fn()
    expect(reloadForNewVersion({ now: 1_000_000 + RELOAD_WINDOW_MS - 1, storage, reload })).toBe(false)
    expect(reload).not.toHaveBeenCalled()
  })

  it('reloads again for a later deploy', () => {
    const storage = memoryStorage()
    reloadForNewVersion({ now: 1_000_000, storage, reload: vi.fn() })
    resetStaleChunkState()

    const reload = vi.fn()
    expect(reloadForNewVersion({ now: 1_000_000 + RELOAD_WINDOW_MS, storage, reload })).toBe(true)
    expect(reload).toHaveBeenCalledOnce()
  })

  it("doesn't reload when there's no storage to remember it in", () => {
    const blocked = {
      getItem: () => {
        throw new Error('blocked')
      },
      setItem: () => {},
    }
    const reload = vi.fn()
    expect(reloadForNewVersion({ now: 1, storage: blocked, reload })).toBe(false)
    expect(reload).not.toHaveBeenCalled()
  })

  it('a second failure while the reload is under way reloads nothing more', () => {
    const reload = vi.fn()
    const storage = memoryStorage()
    reloadForNewVersion({ now: 1, storage, reload })
    expect(reloadForNewVersion({ now: 2, storage, reload })).toBe(true)
    expect(reload).toHaveBeenCalledOnce()
  })
})
