import { describe, expect, it } from 'vitest'
import { pageSlots } from './Pager'

describe('pageSlots', () => {
  it('shows every page when there are only a few', () => {
    expect(pageSlots(0, 1)).toEqual([0])
    expect(pageSlots(1, 5)).toEqual([0, 1, 2, 3, 4])
  })

  it('collapses skipped runs into gaps around the current page', () => {
    expect(pageSlots(20, 40)).toEqual([0, 'gap', 19, 20, 21, 'gap', 39])
  })

  it('shows the lone skipped page instead of a gap', () => {
    expect(pageSlots(3, 40)).toEqual([0, 1, 2, 3, 4, 'gap', 39])
  })

  it('handles the first and last pages', () => {
    expect(pageSlots(0, 40)).toEqual([0, 1, 'gap', 39])
    expect(pageSlots(39, 40)).toEqual([0, 'gap', 38, 39])
  })
})
