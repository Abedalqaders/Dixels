// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { getMyBookableBuilding, searchAvailability } from '../api/bookingsApi'
import type { BookableBuildingDto, BookableSpaceDto, SpaceAvailabilityDto } from '../api/bookingsApi'
import { FindSpacePage } from './FindSpacePage'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))
vi.mock('../api/bookingsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/bookingsApi')>()
  return { ...actual, getMyBookableBuilding: vi.fn(), searchAvailability: vi.fn(), previewBooking: vi.fn(() => new Promise(() => {})) }
})

function space(id: string, name: string, capacity: number): BookableSpaceDto {
  return {
    id,
    name,
    spaceTypeId: 't1',
    spaceTypeName: 'Meeting room',
    iconKey: 0,
    capacity,
    minAttendees: null,
    days: { value: [0, 1, 2, 3, 4, 5, 6], source: 'Building' },
    hours: { value: { isOpen24Hours: false, open: '07:00', close: '20:00' }, source: 'Building' },
    maxDurationMinutes: { value: 120, source: 'Building' },
  }
}

const room201 = space('s1', 'Meeting Room 201', 12)
const desk12 = space('s2', 'Desk 12', 1)
const podA = space('s3', 'Focus Pod A', 1)

const building: BookableBuildingDto = {
  id: 'b1',
  name: 'Riverside HQ',
  timezone: 'UTC',
  maxHorizonDays: 30,
  minLeadMinutes: 0,
  slotMinutes: 15,
  floors: [{ id: 'f1', name: 'Level 1', floorNumber: 1, spaces: [room201, desk12, podA] }],
}

function result(s: BookableSpaceDto, patch: Partial<SpaceAvailabilityDto>): SpaceAvailabilityDto {
  return {
    space: s,
    floorId: 'f1',
    floorName: 'Level 1',
    isAvailable: true,
    violations: [],
    freeUntil: null,
    nextFreeStart: null,
    open: [{ startMinute: 7 * 60, endMinute: 20 * 60, isMine: false }],
    closed: [],
    busy: [],
    ...patch,
  }
}

const violation = (shortMessage: string) => ({ code: 'x', level: null, message: shortMessage + '.', shortMessage })

function renderPage(url = '/find-space?date=2026-10-01&from=10:00&to=11:00&people=4') {
  render(
    <MemoryRouter initialEntries={[url]}>
      <FindSpacePage />
    </MemoryRouter>,
  )
}

describe('FindSpacePage', () => {
  beforeEach(() => {
    vi.mocked(useAuth).mockReturnValue({ user: { access_token: 't' } } as unknown as ReturnType<typeof useAuth>)
    vi.mocked(getMyBookableBuilding).mockResolvedValue(building)
    vi.mocked(searchAvailability).mockReset()
    vi.mocked(searchAvailability).mockResolvedValue({
      buildingId: 'b1',
      buildingName: 'Riverside HQ',
      timezone: 'UTC',
      localStart: '2026-10-01T10:00:00',
      localEnd: '2026-10-01T11:00:00',
      spaces: [
        result(room201, { freeUntil: '14:00' }),
        result(desk12, {
          isAvailable: false,
          violations: [violation('Already booked at that time')],
          nextFreeStart: '12:00',
          busy: [{ startMinute: 9 * 60 + 30, endMinute: 12 * 60, isMine: false }],
        }),
        result(podA, { isAvailable: false, violations: [violation('Seats 1 — you need 4')] }),
      ],
    })
  })

  it('searches for the window in the URL and lists the free rooms with how long they stay free', async () => {
    renderPage()

    expect(await screen.findByRole('heading', { name: /1 space free · Thu 1 Oct at 10:00 for 1h/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Book Meeting Room 201' })).toBeInTheDocument()
    expect(screen.getAllByText('Free until 14:00').length).toBeGreaterThan(0)

    expect(searchAvailability).toHaveBeenCalledWith('t', expect.objectContaining({
      localStart: '2026-10-01T10:00:00',
      localEnd: '2026-10-01T11:00:00',
      attendees: 4,
    }))
  })

  it('folds the unavailable rooms underneath with their reason and next free time', async () => {
    renderPage()

    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: /2 not available at this time/ }))

    expect(screen.getByText(/Already booked at that time · free from 12:00/)).toBeInTheDocument()
    expect(screen.getByText('Seats 1 — you need 4')).toBeInTheDocument()
  })

  it('moves the search to a room\'s next free time, keeping the length', async () => {
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: /2 not available at this time/ }))
    await user.click(screen.getByRole('button', { name: 'Try 12:00 for Desk 12' }))

    await waitFor(() =>
      expect(searchAvailability).toHaveBeenLastCalledWith('t', expect.objectContaining({
        localStart: '2026-10-01T12:00:00',
        localEnd: '2026-10-01T13:00:00',
      })),
    )
  })

  it('opens the booking form pre-filled with the searched time and head count', async () => {
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Book Meeting Room 201' }))

    const dialog = screen.getByRole('dialog', { name: 'Book Meeting Room 201' })
    expect(within(dialog).getByLabelText('Start')).toHaveTextContent('10:00')
    expect(within(dialog).getByLabelText('Duration')).toHaveTextContent('1h · ends 11:00')
    expect(within(dialog).getByLabelText('Attendees')).toHaveValue(4)
  })
})
