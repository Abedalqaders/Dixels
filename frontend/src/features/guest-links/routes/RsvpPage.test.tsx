// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { setLanguage } from '@/i18n'
import { InviteeResponseStatus } from '@/features/bookings/api/bookingsApi'
import { ApiError } from '@/lib/api/httpClient'
import { answerInvitation, lookupInvitation } from '@/features/guest-links/api'
import type { GuestInvitationDto } from '@/features/guest-links/api'
import { RsvpPage } from './RsvpPage'

vi.mock('@/features/guest-links/api', () => ({
  lookupInvitation: vi.fn(),
  answerInvitation: vi.fn(),
}))

const lookup = vi.mocked(lookupInvitation)
const answer = vi.mocked(answerInvitation)

function invitation(over: Partial<GuestInvitationDto> = {}): GuestInvitationDto {
  return {
    guestName: 'Sam Lee',
    invitedBy: 'Dana Test',
    title: 'Planning',
    spaceName: 'Room 1',
    floorName: 'Level 1',
    buildingName: 'HQ',
    address: '12 King Hussein St, Amman',
    localStart: '2026-10-13T10:00:00',
    localEnd: '2026-10-13T11:00:00',
    recurrence: null,
    myResponse: InviteeResponseStatus.Pending,
    isOpen: true,
    language: 'en',
    ...over,
  }
}

// Each test its own link: which links already opened in the invite's language is kept per page load.
let links = 0

function openLink(search = '') {
  const token = `b.row${++links}.mac`
  render(
    <MemoryRouter initialEntries={[`/rsvp/${token}${search}`]}>
      <Routes>
        <Route path="/rsvp/:token" element={<RsvpPage />} />
      </Routes>
    </MemoryRouter>,
  )
  return { token, user: userEvent.setup() }
}

describe('RsvpPage', () => {
  beforeEach(async () => {
    await setLanguage('en')
    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true })
    lookup.mockReset()
    answer.mockReset()
  })

  afterEach(() => {
    Object.defineProperty(navigator, 'webdriver', { value: false, configurable: true })
  })

  it("saves the email's Accept as the page opens, then offers a change of mind", async () => {
    answer.mockImplementation((_token, value) => Promise.resolve(invitation({ myResponse: value })))
    const { token, user } = openLink('?answer=accepted')

    expect(await screen.findByRole('heading', { name: 'You accepted' })).toBeInTheDocument()
    expect(answer).toHaveBeenCalledExactlyOnceWith(token, InviteeResponseStatus.Accepted)
    expect(lookup).not.toHaveBeenCalled()
    expect(screen.getByText('Dana Test invited you')).toBeInTheDocument()
    expect(screen.getByText('Room 1 · Level 1 · HQ')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Decline' }))
    expect(await screen.findByRole('heading', { name: 'You declined' })).toBeInTheDocument()
    expect(answer).toHaveBeenLastCalledWith(token, InviteeResponseStatus.Declined)
  })

  it("saves the email's Maybe, then offers the other two answers", async () => {
    answer.mockImplementation((_token, value) => Promise.resolve(invitation({ myResponse: value })))
    const { token, user } = openLink('?answer=maybe')

    expect(await screen.findByRole('heading', { name: 'You answered Maybe' })).toBeInTheDocument()
    expect(answer).toHaveBeenCalledExactlyOnceWith(token, InviteeResponseStatus.Maybe)
    expect(screen.getByText(/Changed your mind\?/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Maybe' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Accept' }))
    expect(await screen.findByRole('heading', { name: 'You accepted' })).toBeInTheDocument()
  })

  it('never answers for a browser driven by automation (a link scanner): it only shows the invitation', async () => {
    Object.defineProperty(navigator, 'webdriver', { value: true, configurable: true })
    lookup.mockResolvedValue(invitation())
    openLink('?answer=declined')

    expect(await screen.findByRole('heading', { name: 'Will you come?' })).toBeInTheDocument()
    expect(answer).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Accept' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Maybe' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Decline' })).toBeInTheDocument()
  })

  it('without an answer in the link, only looks: the guest picks', async () => {
    lookup.mockResolvedValue(invitation())
    answer.mockResolvedValue(invitation({ myResponse: InviteeResponseStatus.Accepted }))
    const { token, user } = openLink()

    await user.click(await screen.findByRole('button', { name: 'Accept' }))
    expect(await screen.findByRole('heading', { name: 'You accepted' })).toBeInTheDocument()
    expect(answer).toHaveBeenCalledExactlyOnceWith(token, InviteeResponseStatus.Accepted)
  })

  it('a series says the answer covers every upcoming date', async () => {
    lookup.mockResolvedValue(
      invitation({ recurrence: { frequency: 0, interval: 1, weekdays: [], monthlyRepeat: 0, endDate: '2026-10-20' } }),
    )
    openLink()

    expect(await screen.findByText('Your answer covers every upcoming date.')).toBeInTheDocument()
  })

  it('an expired link and an unknown one each get a plain card, not an error', async () => {
    lookup.mockResolvedValueOnce(invitation({ isOpen: false }))
    openLink()
    expect(await screen.findByRole('heading', { name: 'This link has expired' })).toBeInTheDocument()
  })

  it('says a forged or stale link does not work', async () => {
    answer.mockRejectedValue(new ApiError(404, { error: { code: 'Dixels:Bookings:GuestLinkNotFound' } }))
    openLink('?answer=accepted')
    expect(await screen.findByRole('heading', { name: "This link doesn't work" })).toBeInTheDocument()
  })

  it("switching language keeps the guest's pick and never sends the email's answer again", async () => {
    answer.mockImplementation((_token, value) => Promise.resolve(invitation({ myResponse: value })))
    lookup.mockImplementation(() => Promise.resolve(invitation({ myResponse: InviteeResponseStatus.Declined })))
    const { user } = openLink('?answer=accepted')
    await user.click(await screen.findByRole('button', { name: 'Decline' }))
    expect(await screen.findByRole('heading', { name: 'You declined' })).toBeInTheDocument()

    // What the switcher does (LocaleRoot then re-mounts the page in the app).
    await setLanguage('ar')
    expect(await screen.findByRole('heading', { name: 'رفضت الدعوة' })).toBeInTheDocument()
    expect(answer).toHaveBeenCalledTimes(2)
  })

  it("opens in the booker's language, right to left in Arabic", async () => {
    lookup.mockResolvedValue(invitation({ language: 'ar', myResponse: InviteeResponseStatus.Accepted }))
    openLink()

    expect(await screen.findByRole('heading', { name: 'قبلت الدعوة' })).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})
