// @vitest-environment jsdom
import { afterEach, describe, expect, it } from 'vitest'
import { stubMatchMedia } from '@/test/matchMedia'
import { signInExtras } from './signIn'

describe('signInExtras', () => {
  let restore = () => {}
  afterEach(() => {
    restore()
    localStorage.clear()
  })

  it('sends the theme the user picked', () => {
    localStorage.setItem('dixels.theme', 'dark')

    expect(signInExtras()).toEqual({ extraQueryParams: { ui_theme: 'dark' } })
  })

  it("sends the computer's setting when they never picked one", () => {
    restore = stubMatchMedia(1280)

    expect(signInExtras().extraQueryParams.ui_theme).toBe('light')
  })
})
