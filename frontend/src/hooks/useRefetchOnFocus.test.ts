// @vitest-environment jsdom
import { renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useRefetchOnFocus } from './useRefetchOnFocus'

function setVisibility(state: DocumentVisibilityState) {
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => state })
  document.dispatchEvent(new Event('visibilitychange'))
}

describe('useRefetchOnFocus', () => {
  afterEach(() => setVisibility('visible'))

  it('refetches when the tab becomes visible again, not when it is hidden', () => {
    const refetch = vi.fn()
    renderHook(() => useRefetchOnFocus(refetch))

    setVisibility('hidden')
    expect(refetch).not.toHaveBeenCalled()

    setVisibility('visible')
    expect(refetch).toHaveBeenCalledTimes(1)
  })

  it('stops listening once unmounted', () => {
    const refetch = vi.fn()
    const { unmount } = renderHook(() => useRefetchOnFocus(refetch))
    unmount()

    setVisibility('visible')
    expect(refetch).not.toHaveBeenCalled()
  })
})
