import { describe, expect, it } from 'vitest'
import type { SpaceAvailabilityDto } from '../bookings/api/bookingsApi'
import { groupUnavailable } from './unavailableGroups'

const C = 'Dixels:Bookings:'

function room(name: string, codes: string[], maxMinutes = 120): SpaceAvailabilityDto {
  return {
    space: { id: name, name, maxDurationMinutes: { value: maxMinutes, source: 'Space' } } as never,
    floorId: 'f',
    floorName: 'Level 3',
    isAvailable: codes.length === 0,
    violations: codes.map((c) => ({ code: C + c, level: 'Space', message: c, shortMessage: `${c} reason` })),
    freeUntil: null,
    nextFreeStart: null,
    open: [],
    closed: [],
    busy: [],
  }
}

describe('groupUnavailable', () => {
  it('puts each unavailable room in one group per reason, biggest group first, skipping free rooms', () => {
    const groups = groupUnavailable([
      room('Free', []),
      room('A', ['Overlap']),
      room('B', ['OverCapacity']),
      room('C', ['Overlap']),
    ])

    expect(groups.map((g) => [g.title, g.rooms.map((r) => r.room.space.name)])).toEqual([
      ['Already booked', ['A', 'C']],
      ['Too small', ['B']],
    ])
    expect(groups[0].rooms[0].reason).toBe('Overlap reason')
  })

  it("groups by the most serious reason — the server's first", () => {
    const [group] = groupUnavailable([room('A', ['SpaceClosed', 'Overlap'])])
    expect(group.title).toBe('Closed')
  })

  it('only files a room under "too long" when shortening alone would free it, and says how short', () => {
    const groups = groupUnavailable([
      room('Only long, 1h', ['TooLong'], 60),
      room('Only long, 2h', ['TooLong'], 120),
      room('Long and booked', ['TooLong', 'Overlap'], 180),
    ])

    const tooLong = groups.find((g) => g.title === 'Shorter time limit')!
    expect(tooLong.rooms.map((r) => r.room.space.name)).toEqual(['Only long, 1h', 'Only long, 2h'])
    expect(tooLong.shortenTo).toBe(120)
    expect(groups.find((g) => g.title === 'Already booked')!.rooms.map((r) => r.room.space.name)).toEqual(['Long and booked'])
  })

  it('names an unknown rule generically', () => {
    expect(groupUnavailable([room('A', ['SomethingNew'])])[0].title).toBe('Not available')
  })
})
