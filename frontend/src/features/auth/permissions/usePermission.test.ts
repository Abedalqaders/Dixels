import { describe, expect, it } from 'vitest'
import { satisfies } from './usePermission'

const grants = (...names: string[]) => Object.fromEntries(names.map((n) => [n, true]))

describe('satisfies', () => {
  it('one name: that grant', () => {
    expect(satisfies(grants('A'), 'A')).toBe(true)
    expect(satisfies(grants('B'), 'A')).toBe(false)
  })

  it('a list: any one of them', () => {
    expect(satisfies(grants('B'), ['A', 'B'])).toBe(true)
    expect(satisfies(grants('C'), ['A', 'B'])).toBe(false)
    expect(satisfies(grants('A'), [])).toBe(false)
  })

  it('allOf: every one of them, and never an empty list', () => {
    expect(satisfies(grants('A', 'B'), { allOf: ['A', 'B'] })).toBe(true)
    expect(satisfies(grants('A'), { allOf: ['A', 'B'] })).toBe(false)
    expect(satisfies(grants('A', 'B'), { allOf: [] })).toBe(false)
  })

  it('ignores grants that are explicitly false', () => {
    expect(satisfies({ A: false, B: true }, { allOf: ['A', 'B'] })).toBe(false)
    expect(satisfies({ A: false }, 'A')).toBe(false)
  })
})
