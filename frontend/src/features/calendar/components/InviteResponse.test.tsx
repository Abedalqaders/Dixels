// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { setLanguage } from '@/i18n'
import { ApiError, InviteeResponseStatus } from '@/features/bookings/api/bookingsApi'
import { InviteResponse } from './InviteResponse'
import type { InviteResponseProps } from './InviteResponse'

function renderAnswer(props: Partial<InviteResponseProps> = {}) {
  const onRespond = props.onRespond ?? vi.fn().mockResolvedValue(undefined)
  render(<InviteResponse answer={InviteeResponseStatus.Pending} open date="2026-10-13" isSeries={false} {...props} onRespond={onRespond} />)
  return { onRespond, user: userEvent.setup() }
}

describe('InviteResponse', () => {
  it('answers one date straight away and shows the chosen answer in soft green', async () => {
    const { onRespond, user } = renderAnswer()

    await user.click(screen.getByRole('button', { name: 'Accept' }))
    expect(onRespond).toHaveBeenCalledWith(InviteeResponseStatus.Accepted, 'date')
  })

  it('marks the current answer: Accepted soft green, Declined soft red', () => {
    renderAnswer({ answer: InviteeResponseStatus.Declined })

    const declined = screen.getByRole('button', { name: 'Declined' })
    expect(declined).toHaveAttribute('aria-pressed', 'true')
    expect(declined.className).toContain('state-cancelled-soft')
    expect(screen.getByRole('button', { name: 'Accept' })).toHaveAttribute('aria-pressed', 'false')
  })

  it('on a series, asks "this date only or all upcoming dates?" — all upcoming by default', async () => {
    const { onRespond, user } = renderAnswer({ isSeries: true })

    await user.click(screen.getByRole('button', { name: 'Decline' }))
    const choice = screen.getByRole('group', { name: 'Answer for…' })
    expect(within(choice).getByRole('radio', { name: 'All upcoming dates' })).toBeChecked()
    expect(choice).toHaveTextContent('This replaces any answers you gave to single dates.')
    expect(onRespond).not.toHaveBeenCalled()

    await user.click(within(choice).getByRole('radio', { name: /This date only/ }))
    await user.click(within(choice).getByRole('button', { name: 'Decline' }))
    expect(onRespond).toHaveBeenCalledWith(InviteeResponseStatus.Declined, 'date')
    expect(screen.queryByRole('group', { name: 'Answer for…' })).not.toBeInTheDocument()
  })

  it('once the meeting has started, only shows the answer and says answers are closed', () => {
    renderAnswer({ open: false, answer: InviteeResponseStatus.Accepted })

    expect(screen.queryByRole('button', { name: /Accept|Decline/ })).not.toBeInTheDocument()
    expect(screen.getByText('Answers are closed.')).toBeInTheDocument()
    expect(screen.getAllByText('Accepted').length).toBeGreaterThan(0)
  })

  it("shows the server's message when the answer is refused", async () => {
    const onRespond = vi.fn().mockRejectedValue(new ApiError(400, { error: { code: 'Dixels:Bookings:ResponseClosed', message: 'Answers are closed: this meeting has started or was cancelled.' } }))
    const { user } = renderAnswer({ onRespond })

    await user.click(screen.getByRole('button', { name: 'Accept' }))
    expect(await screen.findByText('Answers are closed: this meeting has started or was cancelled.')).toBeInTheDocument()
  })

  it('speaks Arabic', async () => {
    await setLanguage('ar')
    renderAnswer()
    expect(screen.getByText('ردك')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'قبول' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'رفض' })).toBeInTheDocument()
  })
})
