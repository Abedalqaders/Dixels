// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { renderHook } from '@testing-library/react'
import { useUnsavedChangesWarning } from './useUnsavedChangesWarning'

function dispatchBeforeUnload(): Event {
  const event = new Event('beforeunload', { cancelable: true })
  window.dispatchEvent(event)
  return event
}

describe('useUnsavedChangesWarning', () => {
  it('does not prevent unload when there are no unsaved changes', () => {
    renderHook(() => useUnsavedChangesWarning(false))

    expect(dispatchBeforeUnload().defaultPrevented).toBe(false)
  })

  it('prevents unload while there are unsaved changes', () => {
    renderHook(() => useUnsavedChangesWarning(true))

    expect(dispatchBeforeUnload().defaultPrevented).toBe(true)
  })

  it('stops blocking once the draft is no longer dirty', () => {
    const { rerender } = renderHook(({ isDirty }) => useUnsavedChangesWarning(isDirty), {
      initialProps: { isDirty: true },
    })

    rerender({ isDirty: false })

    expect(dispatchBeforeUnload().defaultPrevented).toBe(false)
  })

  it('removes its listener on unmount, even while dirty', () => {
    const { unmount } = renderHook(() => useUnsavedChangesWarning(true))

    unmount()

    expect(dispatchBeforeUnload().defaultPrevented).toBe(false)
  })
})
