import { describe, expect, it } from 'vitest'
import { buildDurationLimits, dragHint, roomsThatFit } from './durationLimits'

// Only maxDurationMinutes matters here.
const building = (maxes: number[][]) =>
  ({
    floors: maxes.map((floor, i) => ({
      id: `f${i}`,
      spaces: floor.map((value, j) => ({ id: `${i}-${j}`, maxDurationMinutes: { value, source: 'Building' } })),
    })),
  }) as never

// 17 rooms across two floors: 5 × 1h, 6 × 2h, 6 × 3h.
const limits = buildDurationLimits(building([[60, 60, 60, 120, 120, 120, 180, 180, 180], [60, 60, 120, 120, 120, 180, 180, 180]]))

describe('buildDurationLimits', () => {
  it('sorts every room across floors and knows the longest', () => {
    expect(limits.sorted).toHaveLength(17)
    expect(limits.sorted[0]).toBe(60)
    expect(limits.longest).toBe(180)
  })

  it('has no cap for a building with no rooms', () => {
    expect(buildDurationLimits(building([]))).toEqual({ sorted: [], longest: Infinity })
  })
})

describe('roomsThatFit', () => {
  it('counts the rooms whose max is at least the length, boundaries included', () => {
    expect(roomsThatFit(limits, 30)).toBe(17)
    expect(roomsThatFit(limits, 60)).toBe(17)
    expect(roomsThatFit(limits, 75)).toBe(12)
    expect(roomsThatFit(limits, 120)).toBe(12)
    expect(roomsThatFit(limits, 150)).toBe(6)
    expect(roomsThatFit(limits, 240)).toBe(0)
  })
})

describe('dragHint', () => {
  it('says nothing below the cap, even when some rooms are ruled out (the panel explains that)', () => {
    expect(dragHint(60, limits)).toEqual({ capped: false, suffix: '', announcement: '' })
    expect(dragHint(150, limits)).toEqual({ capped: false, suffix: '', announcement: '' })
  })

  it('marks the cap at the longest any room allows', () => {
    const hint = dragHint(180, limits)
    expect(hint.capped).toBe(true)
    expect(hint.suffix).toBe('3h max')
    expect(hint.announcement).toBe('Capped at 3h, the longest any room allows — 6 of 17 rooms.')
  })

  it('stays quiet with no rooms to compare against', () => {
    expect(dragHint(600, buildDurationLimits(building([])))).toEqual({ capped: false, suffix: '', announcement: '' })
  })
})
