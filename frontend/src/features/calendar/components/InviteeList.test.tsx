// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { InviteeResponseStatus } from '@/features/bookings/api/bookingsApi'
import type { BookingInviteeDto } from '@/features/bookings/api/bookingsApi'
import { InviteeList, countAnswers } from './InviteeList'

const guest = (name: string, responseStatus: InviteeResponseStatus): BookingInviteeDto => ({
  userId: `u-${name}`,
  name,
  email: '',
  isExternal: false,
  responseStatus,
  isBusy: false,
  busyDates: 0,
  maybeBusyDates: 0,
  busyTimes: [],
})

describe('InviteeList', () => {
  const guests = [
    guest('Rana', InviteeResponseStatus.Accepted),
    guest('Omar', InviteeResponseStatus.Maybe),
    guest('Sara', InviteeResponseStatus.Declined),
    guest('Lina', InviteeResponseStatus.Pending),
  ]

  it('counts a Maybe on its own', () => {
    expect(countAnswers(guests)).toEqual({ accepted: 1, maybe: 1, declined: 1, waiting: 1 })
  })

  it('marks each answer, Maybe as an amber "~", and names Maybe in the tally only when someone said it', () => {
    const { rerender } = render(<InviteeList invitees={guests} />)
    expect(screen.getByText('Accepted 1 · Maybe 1 · Declined 1 · Waiting 1')).toBeInTheDocument()
    const maybe = screen.getByRole('img', { name: 'Maybe' })
    expect(maybe).toHaveTextContent('~')
    expect(maybe.className).toContain('state-expired-soft')

    rerender(<InviteeList invitees={guests.filter((g) => g.responseStatus !== InviteeResponseStatus.Maybe)} />)
    expect(screen.getByText('Accepted 1 · Declined 1 · Waiting 1')).toBeInTheDocument()
  })
})
