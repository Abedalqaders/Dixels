// @vitest-environment jsdom
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { getMyBookableBuilding, searchAvailability } from '@/features/bookings/api/bookingsApi'
import type { BookableBuildingDto, BookableSpaceDto, SpaceAvailabilityDto } from '@/features/bookings/api/bookingsApi'
import { TestProviders } from '@/test/providers'
import { setLanguage } from '@/i18n'
import { granted, WithPermissions } from '@/test/permissions'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import { FindSpacePage } from './FindSpacePage'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))
vi.mock('@/features/bookings/api/bookingsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/bookings/api/bookingsApi')>()
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
  maxSeriesHorizonDays: 90,
  isRemoved: false,
  minLeadMinutes: 0,
  slotMinutes: 15,
  ownOverlapPolicy: 1,
  days: [0, 1, 2, 3, 4, 5, 6],
  hours: { isOpen24Hours: false, open: '07:00', close: '20:00' },
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

const BOOKER = [Permissions.Bookings.Default, Permissions.Bookings.Create]

function renderPage(url = '/find-space?date=2026-10-01&from=10:00&to=11:00&people=4', grants: string[] = BOOKER) {
  render(
    <TestProviders>
      <WithPermissions value={granted(...grants)}>
        <MemoryRouter initialEntries={[url]}>
          <FindSpacePage />
        </MemoryRouter>
      </WithPermissions>
    </TestProviders>,
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
      warnings: [],
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

  it('shows availability but offers no way to book: no Book button, the bars are not pickers', async () => {
    renderPage(undefined, [Permissions.Bookings.Default])

    expect(await screen.findByRole('heading', { name: /1 space free/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Book Meeting Room 201' })).not.toBeInTheDocument()
    expect(screen.getByText(/Availability only/)).toBeInTheDocument()
    // Nothing on the page reacts to a press when booking isn't allowed: no dialog opens.
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('searches for the window in the URL and lists the free rooms with how long they stay free', async () => {
    renderPage()

    expect(await screen.findByRole('heading', { name: /1 space free · Thu 1 Oct, 10:00–11:00/ })).toBeInTheDocument()
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

  it('opens the booking form pre-filled with the searched time; the head count is the booker plus their guests', async () => {
    const user = userEvent.setup()
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'Book Meeting Room 201' }))

    const dialog = screen.getByRole('dialog', { name: 'Book Meeting Room 201' })
    expect(within(dialog).getByLabelText('From')).toHaveTextContent('10:00')
    expect(within(dialog).getByLabelText('To')).toHaveTextContent('11:00')
    // "4 people" picked which rooms fit; the booking counts whoever is invited.
    expect(within(dialog).getByRole('group', { name: 'People' })).toHaveTextContent('1 person · just you')
  })

  it('reads in Arabic, with Arabic plural forms and day names', async () => {
    await setLanguage('ar')
    const user = userEvent.setup()
    renderPage()

    expect(await screen.findByRole('heading', { name: /^مساحة واحدة متاحة · الخميس 1 أكتوبر، ⁦10:00–11:00⁩$/ })).toBeInTheDocument()
    expect(screen.getByText(/يمكن الحجز قبل 30 يومًا كحد أقصى/)).toBeInTheDocument()
    expect(screen.getAllByText('متاحة حتى 14:00').length).toBeGreaterThan(0)
    await user.click(screen.getByRole('button', { name: 'مساحتان غير متاحتين في هذا الوقت' }))
    expect(screen.getByRole('button', { name: 'جرّب 12:00 في Desk 12' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'احجز Meeting Room 201' }))
    const dialog = screen.getByRole('dialog', { name: 'حجز Meeting Room 201' })
    expect(within(dialog).getByRole('group', { name: 'الأشخاص' })).toHaveTextContent('شخص واحد · أنت فقط')
    expect(within(dialog).getByText(/^مفتوحة ⁦07:00–20:00⁩، كل يوم · حتى ساعتين · 12 مقعدًا$/)).toBeInTheDocument()
  })

  describe('floor filter', () => {
    const room301 = space('s4', 'Meeting Room 301', 8)

    // Radix Select calls pointer-capture and scrollIntoView, which jsdom doesn't implement.
    beforeAll(() => {
      Element.prototype.hasPointerCapture ??= () => false
      Element.prototype.releasePointerCapture ??= () => {}
      Element.prototype.scrollIntoView ??= () => {}
    })

    beforeEach(() => {
      vi.mocked(getMyBookableBuilding).mockResolvedValue({
        ...building,
        floors: [
          { id: 'f1', name: 'Level 1', floorNumber: 1, spaces: [room201, desk12, podA] },
          { id: 'f3', name: 'Level 3', floorNumber: 3, spaces: [room301] },
        ],
      })
      vi.mocked(searchAvailability).mockResolvedValue({
        buildingId: 'b1',
        buildingName: 'Riverside HQ',
        timezone: 'UTC',
        localStart: '2026-10-01T10:00:00',
        localEnd: '2026-10-01T11:00:00',
        warnings: [],
        spaces: [
          result(room201, { freeUntil: '14:00' }),
          result(room301, { floorId: 'f3', floorName: 'Level 3', freeUntil: '12:00' }),
          result(desk12, { isAvailable: false, violations: [violation('Already booked at that time')] }),
        ],
      })
    })

    it('offers every floor with how many rooms it has free', async () => {
      const user = userEvent.setup()
      renderPage()

      const floor = await screen.findByRole('combobox', { name: 'Floor' })
      expect(floor).toHaveTextContent('All floors · 2 free')

      await user.click(floor)
      expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual([
        'All floors · 2 free',
        'Level 1 · 1 free',
        'Level 3 · 1 free',
      ])
    })

    it('narrows the list to the picked floor without searching again', async () => {
      const user = userEvent.setup()
      renderPage()

      await user.click(await screen.findByRole('combobox', { name: 'Floor' }))
      await user.click(screen.getByRole('option', { name: 'Level 3 · 1 free' }))

      expect(screen.getByRole('combobox', { name: 'Floor' })).toHaveTextContent('Level 3 · 1 free')
      expect(screen.getByRole('heading', { name: /1 space free/ })).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Book Meeting Room 301' })).toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Book Meeting Room 201' })).not.toBeInTheDocument()
      expect(searchAvailability).toHaveBeenCalledTimes(1)
      expect(searchAvailability).toHaveBeenCalledWith('t', expect.not.objectContaining({ floorId: expect.anything() }))
    })

    it('starts on the floor in the URL', async () => {
      renderPage('/find-space?date=2026-10-01&from=10:00&to=11:00&people=4&floor=f3')

      expect(await screen.findByRole('combobox', { name: 'Floor' })).toHaveTextContent('Level 3 · 1 free')
      expect(screen.queryByRole('button', { name: 'Book Meeting Room 201' })).not.toBeInTheDocument()
    })

    it('is left out for a one-floor building', async () => {
      vi.mocked(getMyBookableBuilding).mockResolvedValue(building)
      renderPage()

      await screen.findByRole('button', { name: 'Book Meeting Room 201' })
      expect(screen.queryByRole('combobox', { name: 'Floor' })).not.toBeInTheDocument()
    })
  })

  it("tells the employee their building was removed, instead of offering a search", async () => {
    vi.mocked(getMyBookableBuilding).mockResolvedValue({ ...building, isRemoved: true, floors: [] })
    renderPage()

    expect(await screen.findByText('Riverside HQ is no longer available.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'My calendar' })).toHaveAttribute('href', '/my-calendar')
    expect(screen.queryByRole('search')).not.toBeInTheDocument()
    expect(searchAvailability).not.toHaveBeenCalled()
  })
})
