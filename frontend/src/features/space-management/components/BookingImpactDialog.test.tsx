// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReservationImpactDto } from '@/lib/api/reservationImpact'
import { setLanguage } from '@/i18n'
import { BookingImpactDialog } from './BookingImpactDialog'

const impact: ReservationImpactDto = {
  count: 2,
  items: [
    {
      kind: 'booking',
      id: 'b1',
      title: 'Planning',
      heldBy: 'Jordan Reed',
      placeName: 'Room 1',
      placeDetail: 'Level 2',
      localStart: '2026-09-30T17:00:00',
      localEnd: '2026-09-30T18:00:00',
      reasons: ['Open 09:00–17:00 only'],
    },
    {
      kind: 'booking',
      id: 'b2',
      title: 'Retro',
      heldBy: 'Amira Hassan',
      placeName: 'Room 2',
      placeDetail: 'Level 2',
      localStart: '2026-10-01T08:00:00',
      localEnd: '2026-10-01T09:00:00',
      reasons: ['Open 09:00–17:00 only'],
    },
  ],
}

describe('BookingImpactDialog', () => {
  it('lists who, when, where and why, and lets the admin keep or cancel them', async () => {
    const user = userEvent.setup()
    const onChoose = vi.fn()
    render(<BookingImpactDialog mode="change" impact={impact} onChoose={onChoose} />)

    const dialog = screen.getByRole('alertdialog', { name: 'This change affects 2 upcoming bookings' })
    const rows = within(dialog).getAllByRole('listitem')
    expect(rows[0]).toHaveTextContent('Wed 30 Sep · 17:00–18:00')
    expect(rows[0]).toHaveTextContent('Room 1 · Level 2')
    expect(rows[0]).toHaveTextContent('Jordan Reed · Planning')
    expect(rows[0]).toHaveTextContent('Open 09:00–17:00 only')

    await user.click(within(dialog).getByRole('button', { name: 'Keep them and save' }))
    expect(onChoose).toHaveBeenLastCalledWith('keep')
  })

  it('offers to cancel them instead', async () => {
    const user = userEvent.setup()
    const onChoose = vi.fn()
    render(<BookingImpactDialog mode="closure" impact={impact} onChoose={onChoose} />)

    expect(screen.getByRole('alertdialog', { name: 'This closure falls on 2 upcoming bookings' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Cancel 2 and add the closure' }))
    expect(onChoose).toHaveBeenLastCalledWith('cancel')
  })

  it('for a delete, only confirms — the bookings go with the room', async () => {
    const user = userEvent.setup()
    const onChoose = vi.fn()
    render(<BookingImpactDialog mode="delete" subject="Level 2" impact={impact} onChoose={onChoose} />)

    expect(screen.getByRole('alertdialog', { name: 'Deleting “Level 2” cancels 2 upcoming bookings' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Keep them/ })).not.toBeInTheDocument()
    expect(screen.getByText(/Restoring later won't bring them back/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Go back' }))
    expect(onChoose).toHaveBeenLastCalledWith(null)
  })

  it('warns that assigned employees will be left without a building, even with no bookings', async () => {
    const user = userEvent.setup()
    const onChoose = vi.fn()
    render(<BookingImpactDialog mode="delete" subject="Riverside HQ" impact={{ count: 0, items: [], assignedEmployees: 8 }} onChoose={onChoose} />)

    expect(screen.getByRole('alertdialog', { name: 'Delete “Riverside HQ”?' })).toBeInTheDocument()
    expect(screen.getByRole('note')).toHaveTextContent("8 employees are assigned to Riverside HQ. They won't be able to book")
    expect(screen.queryByRole('list', { name: 'Affected bookings' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Delete' }))
    expect(onChoose).toHaveBeenLastCalledWith('cancel')
  })

  it('when moving someone, only confirms — their bookings in the old building are cancelled', async () => {
    const user = userEvent.setup()
    const onChoose = vi.fn()
    render(<BookingImpactDialog mode="reassign" subject="Jordan Reed" impact={impact} onChoose={onChoose} />)

    expect(screen.getByRole('alertdialog', { name: 'Moving Jordan Reed cancels 2 upcoming bookings' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /keep/i })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Move and cancel 2' }))
    expect(onChoose).toHaveBeenLastCalledWith('cancel')
  })

  it('lists a long preview a page at a time and adds the next page on "Show more"', async () => {
    await setLanguage('en')
    const user = userEvent.setup()
    let resolve: (page: ReservationImpactDto) => void = () => {}
    const loadMore = vi.fn((skip: number) => {
      void skip
      return new Promise<ReservationImpactDto>((r) => (resolve = r))
    })
    const onChoose = vi.fn()
    render(<BookingImpactDialog mode="change" impact={{ ...impact, count: 3 }} loadMore={loadMore} onChoose={onChoose} />)

    // The count is all of them; the list is what has been sent so far.
    expect(screen.getByRole('alertdialog', { name: 'This change affects 3 upcoming bookings' })).toBeInTheDocument()
    expect(screen.getAllByRole('listitem')).toHaveLength(2)
    expect(screen.getByText('Showing 2 of 3')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Show more' }))
    expect(loadMore).toHaveBeenCalledWith(2)
    expect(screen.getByRole('button', { name: 'Loading…' })).toBeDisabled()

    resolve({
      count: 3,
      items: [{ ...impact.items[1], id: 'b3', placeName: 'Room 3', localStart: '2026-10-02T08:00:00', localEnd: '2026-10-02T09:00:00' }],
    })
    expect(await screen.findByText('Showing 3 of 3')).toBeInTheDocument()
    const rows = screen.getAllByRole('listitem')
    expect(rows).toHaveLength(3)
    expect(rows[2]).toHaveTextContent('Room 3 · Level 2')
    // All shown: nothing more to ask for.
    expect(screen.queryByRole('button', { name: 'Show more' })).not.toBeInTheDocument()

    // Keep or cancel is still about every one of them.
    await user.click(screen.getByRole('button', { name: 'Cancel 3 and save' }))
    expect(onChoose).toHaveBeenLastCalledWith('cancel')
  })

  it('says so when the next page fails, and lets the admin try again', async () => {
    await setLanguage('en')
    const user = userEvent.setup()
    const loadMore = vi.fn().mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce({ count: 3, items: [{ ...impact.items[1], id: 'b3' }] })
    render(<BookingImpactDialog mode="delete" subject="Level 2" impact={{ ...impact, count: 3 }} loadMore={loadMore} onChoose={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Show more' }))
    expect(await screen.findByRole('alert')).toHaveTextContent("Couldn't load more. Try again.")

    await user.click(screen.getByRole('button', { name: 'Show more' }))
    expect(await screen.findByText('Showing 3 of 3')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('groups a big total the way the reader writes numbers', async () => {
    await setLanguage('en')
    render(<BookingImpactDialog mode="delete" subject="Riverside HQ" impact={{ ...impact, count: 1240 }} loadMore={vi.fn()} onChoose={vi.fn()} />)

    expect(screen.getByText('Showing 2 of 1,240')).toBeInTheDocument()
  })

  it('shows no paging line when everything fits on one page', async () => {
    await setLanguage('en')
    render(<BookingImpactDialog mode="change" impact={impact} loadMore={vi.fn()} onChoose={vi.fn()} />)

    expect(screen.queryByText(/Showing/)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Show more' })).not.toBeInTheDocument()
  })

  it('counts in Arabic with the right plural form', async () => {
    await setLanguage('ar')
    const { rerender } = render(<BookingImpactDialog mode="change" impact={impact} onChoose={vi.fn()} />)

    // Two is its own form in Arabic (the dual), not "2 bookings".
    expect(screen.getByRole('alertdialog', { name: 'يؤثر هذا التغيير في حجزين قادمين' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'الإبقاء عليها والحفظ' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'إلغاء الحجوزات (2) والحفظ' })).toBeInTheDocument()
    expect(screen.getAllByRole('listitem')[0]).toHaveTextContent('Room 1 · Level 2')

    rerender(<BookingImpactDialog mode="change" impact={{ ...impact, count: 11 }} onChoose={vi.fn()} />)
    expect(screen.getByRole('alertdialog', { name: 'يؤثر هذا التغيير في 11 حجزًا قادمًا' })).toBeInTheDocument()
  })
})
