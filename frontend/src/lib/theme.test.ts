// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'
import { readSavedTheme, setTheme, subscribeToTheme, THEME_KEY } from './theme'

afterEach(() => {
  vi.restoreAllMocks()
  localStorage.clear()
  delete document.documentElement.dataset.theme
})

describe('theme', () => {
  it('has no saved theme until the user picks one', () => {
    expect(readSavedTheme()).toBeNull()
  })

  it('switches the page and remembers the pick', () => {
    setTheme('dark')

    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(localStorage.getItem(THEME_KEY)).toBe('dark')
    expect(readSavedTheme()).toBe('dark')
  })

  it('ignores anything in storage that is not a theme', () => {
    localStorage.setItem(THEME_KEY, 'purple')
    expect(readSavedTheme()).toBeNull()
  })

  it('still switches the page when storage is blocked', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })

    expect(() => setTheme('dark')).not.toThrow()
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(readSavedTheme()).toBeNull()
  })

  it('tells subscribers about a change, and stops once they unsubscribe', () => {
    const listener = vi.fn()
    const unsubscribe = subscribeToTheme(listener)

    setTheme('light')
    unsubscribe()
    setTheme('dark')

    expect(listener).toHaveBeenCalledTimes(1)
  })
})
