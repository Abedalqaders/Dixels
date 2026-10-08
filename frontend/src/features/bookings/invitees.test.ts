import { describe, expect, it } from 'vitest'
import { headCount, resolvedInvitees, toInviteeDtos } from './invitees'
import type { Invitee } from './invitees'

const sara: Invitee = { userId: 'u-sara', name: 'Sara Ali', email: 'sara@dixels.io', isExternal: false }
const guest: Invitee = { userId: null, name: 'Omar', email: 'omar@acme.com', isExternal: true }

describe('headCount', () => {
  it('is you plus everyone invited', () => {
    expect(headCount(0)).toBe(1)
    expect(headCount(3)).toBe(4)
  })
})

describe('toInviteeDtos', () => {
  it('sends a colleague by id and a guest by email and name', () => {
    expect(toInviteeDtos([sara, guest])).toEqual([{ userId: 'u-sara' }, { email: 'omar@acme.com', name: 'Omar' }])
  })

  it('sends no name for a guest without one', () => {
    expect(toInviteeDtos([{ ...guest, name: '  ' }])).toEqual([{ email: 'omar@acme.com', name: null }])
  })
})

describe('resolvedInvitees', () => {
  it("turns a guest's email that belongs to a colleague into that colleague", () => {
    const typed: Invitee = { userId: null, name: '', email: 'Sara@Dixels.io ', isExternal: true }
    const next = resolvedInvitees([typed], [{ userId: 'u-sara', name: 'Sara Ali', email: 'sara@dixels.io', isExternal: false, responseStatus: 0, isBusy: false, busyDates: 0, maybeBusyDates: 0, busyTimes: [] }])
    expect(next).toEqual([sara])
  })

  it('returns the same list when nothing changed, so the form stops adjusting', () => {
    const current = [sara, guest]
    const next = resolvedInvitees(current, [
      { userId: 'u-sara', name: 'Sara Ali', email: 'sara@dixels.io', isExternal: false, responseStatus: 0, isBusy: false, busyDates: 0, maybeBusyDates: 0, busyTimes: [] },
      { userId: null, name: 'Omar', email: 'omar@acme.com', isExternal: true, responseStatus: 0, isBusy: false, busyDates: 0, maybeBusyDates: 0, busyTimes: [] },
    ])
    expect(next).toBe(current)
  })

  it('ignores an answer about a different list', () => {
    const current = [guest]
    expect(resolvedInvitees(current, [])).toBe(current)
    expect(resolvedInvitees(current, [{ userId: 'u-x', name: 'X', email: 'x@dixels.io', isExternal: false, responseStatus: 0, isBusy: false, busyDates: 0, maybeBusyDates: 0, busyTimes: [] }])).toBe(current)
    expect(resolvedInvitees(current, undefined)).toBe(current)
  })
})
