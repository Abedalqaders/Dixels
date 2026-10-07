// @vitest-environment jsdom
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createSeries, previewBooking, previewSeries } from '@/features/bookings/api/bookingsApi'
import type { BookableBuildingDto, BookableSpaceDto, BookingDto, OccurrencePreviewDto, SeriesPreviewDto } from '@/features/bookings/api/bookingsApi'
import { TestProviders } from '@/test/providers'
import { BookingForm } from './BookingForm'

vi.mock('@/features/bookings/api/bookingsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/bookings/api/bookingsApi')>()
  return {
    ...actual,
    previewBooking: vi.fn(() => new Promise(() => {})),
    createBooking: vi.fn(),
    previewSeries: vi.fn(),
    createSeries: vi.fn(),
    // The room's days stay loading: the plain time limits apply.
    getSpaceDays: vi.fn(() => new Promise(() => {})),
  }
})

// Radix Select calls pointer-capture and scrollIntoView, which jsdom doesn't implement.
beforeAll(() => {
  Element.prototype.hasPointerCapture ??= () => false
  Element.prototype.releasePointerCapture ??= () => {}
  Element.prototype.scrollIntoView ??= () => {}
})

const space: BookableSpaceDto = {
  id: 'space-1',
  name: 'Room 1',
  spaceTypeId: 'type-1',
  spaceTypeName: 'Meeting room',
  iconKey: 0,
  capacity: 8,
  minAttendees: null,
  days: { value: [0, 1, 2, 3, 4], source: 'Building' },
  hours: { value: { isOpen24Hours: true, open: '00:00', close: '00:00' }, source: 'Building' },
  maxDurationMinutes: { value: 120, source: 'Building' },
}

const building: BookableBuildingDto = {
  id: 'b-1',
  name: 'Test HQ',
  timezone: 'UTC',
  maxHorizonDays: 30,
  maxSeriesHorizonDays: 90,
  isRemoved: false,
  minLeadMinutes: 0,
  slotMinutes: 15,
  ownOverlapPolicy: 1,
  days: [0, 1, 2, 3, 4, 5, 6],
  hours: { isOpen24Hours: false, open: '07:00', close: '20:00' },
  floors: [{ id: 'f-1', name: 'Level 1', floorNumber: 1, spaces: [space] }],
}

const occurrence = (date: string, patch: Partial<OccurrencePreviewDto> = {}): OccurrencePreviewDto => ({
  date,
  localStart: `${date}T10:00:00`,
  localEnd: `${date}T11:00:00`,
  isValid: true,
  violations: [],
  warnings: [],
  ...patch,
})

const threeDates: SeriesPreviewDto = {
  seriesViolations: [],
  invitees: [],
  occurrences: [
    occurrence('2026-10-06'),
    occurrence('2026-10-13', {
      isValid: false,
      violations: [{ code: 'Dixels:Bookings:SpaceClosed', level: 'Building', message: 'Closed.', shortMessage: 'Closed until 14 Oct (Holiday)' }],
    }),
    occurrence('2026-10-20'),
  ],
  bookableCount: 2,
  timezone: 'UTC',
}

function renderForm(onBooked = vi.fn()) {
  render(
    <BookingForm
      token="t"
      building={building}
      space={space}
      floorName="Level 1"
      initialSlot={{ date: '2026-10-06', start: '10:00', end: '11:00' }}
      onClose={vi.fn()}
      onBooked={onBooked}
    />,
    { wrapper: TestProviders },
  )
  return onBooked
}

async function pickRepeat(user: ReturnType<typeof userEvent.setup>, label: string | RegExp) {
  await user.click(screen.getByLabelText('Repeat'))
  await user.click(screen.getByRole('option', { name: label }))
}

describe('BookingForm — repeat', () => {
  beforeEach(() => {
    vi.mocked(previewSeries).mockReset()
    vi.mocked(createSeries).mockReset()
    vi.mocked(previewBooking).mockClear()
  })

  it('offers Teams-style quick choices worded from the date', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.click(screen.getByLabelText('Repeat'))

    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Does not repeat',
      'Every workday (Sun–Thu)',
      'Daily',
      'Weekly on Tuesday',
      'Monthly on day 6',
      'Custom…',
    ])
  })

  it('checks every date, books the free ones, and leaves out the ones unticked', async () => {
    vi.mocked(previewSeries).mockResolvedValue(threeDates)
    const first = { id: 'b1', spaceName: 'Room 1', localStart: '2026-10-06T10:00:00', localEnd: '2026-10-06T11:00:00' } as BookingDto
    vi.mocked(createSeries).mockResolvedValue({ seriesId: 's1', bookings: [first] })
    const user = userEvent.setup()
    const onBooked = renderForm()

    await pickRepeat(user, 'Weekly on Tuesday')

    // Four weeks by default, ending on the last of them — Tue 27 Oct, not a day after it.
    expect(screen.getByLabelText('Number of weeks')).toHaveValue(4)
    expect(screen.getByText(/^Occurs every Tuesday until Tue 27 Oct/)).toBeInTheDocument()
    const dates = await screen.findByRole('region', { name: 'Dates' })
    expect(within(dates).getByText('2 of 3 dates are free')).toBeInTheDocument()
    expect(within(dates).getByLabelText(/Tue 13 Oct Closed until 14 Oct/)).toBeDisabled()
    expect(previewSeries).toHaveBeenCalledWith('t', expect.objectContaining({
      recurrence: { frequency: 1, interval: 1, weekdays: [2], monthlyRepeat: 0, endDate: '2026-10-27' },
    }))

    // Untick the last free date: one left to book.
    await user.click(within(dates).getByLabelText(/Tue 20 Oct free/))
    await user.click(screen.getByRole('button', { name: 'Book 1 of 3' }))

    await waitFor(() => expect(onBooked).toHaveBeenCalledWith(first, 1))
    const sent = vi.mocked(createSeries).mock.calls[0][1]
    expect(sent.skipDates).toEqual(['2026-10-13', '2026-10-20'])
    expect(sent.idempotencyKey).toMatch(/^[0-9a-f-]{36}$/)
  })

  it('shows a problem every date shares once, and books nothing', async () => {
    vi.mocked(previewSeries).mockResolvedValue({
      ...threeDates,
      seriesViolations: [{ code: 'Dixels:Bookings:OverCapacity', level: 'Space', message: 'This space seats 8, but you asked for 10.', shortMessage: '' }],
      bookableCount: 0,
    })
    const user = userEvent.setup()
    renderForm()

    await pickRepeat(user, 'Daily')

    expect(await screen.findByText('This space seats 8, but you asked for 10.')).toBeInTheDocument()
    expect(screen.getByText(/applies to every date/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^Book 0 of/ })).toBeDisabled()
  })

  it('custom: weekly with no day picked is pointed out, not guessed', async () => {
    const user = userEvent.setup()
    renderForm()

    await pickRepeat(user, 'Custom…')
    const dialog = screen.getByRole('dialog', { name: 'Custom recurrence' })
    // Weekly on the date's weekday to start with — untick it.
    await user.click(within(dialog).getByRole('button', { name: 'Tuesday' }))

    expect(within(dialog).getByRole('alert')).toHaveTextContent('Pick at least one day.')
    expect(within(dialog).getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('custom: every 2 weeks on Sunday and Tuesday reads like Teams', async () => {
    vi.mocked(previewSeries).mockResolvedValue(threeDates)
    const user = userEvent.setup()
    renderForm()

    await pickRepeat(user, 'Custom…')
    const dialog = screen.getByRole('dialog', { name: 'Custom recurrence' })
    const interval = within(dialog).getByLabelText('Repeat every')
    await user.clear(interval)
    await user.type(interval, '2')
    await user.click(within(dialog).getByRole('button', { name: 'Sunday' }))
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(screen.getByText(/^Occurs every 2 weeks on Sunday and Tuesday until Mon 2 Nov/)).toBeInTheDocument()
    await waitFor(() =>
      expect(previewSeries).toHaveBeenLastCalledWith('t', expect.objectContaining({
        recurrence: expect.objectContaining({ frequency: 1, interval: 2, weekdays: [0, 2] }),
      })),
    )
  })
})
