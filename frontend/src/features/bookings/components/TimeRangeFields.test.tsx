// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TimeRangeFields } from './TimeRangeFields'

function renderFields(props: Partial<Parameters<typeof TimeRangeFields>[0]> = {}) {
  const onChange = vi.fn()
  render(
    <div>
      <TimeRangeFields idPrefix="t" start="10:30" end="11:30" slotMinutes={15} onChange={onChange} {...props} />
    </div>,
  )
  return onChange
}

describe('TimeRangeFields', () => {
  it('shows the derived end time next to the duration', () => {
    renderFields()

    expect(screen.getByText('10:30–11:30')).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: '1h' })).toHaveAttribute('aria-checked', 'true')
  })

  it('keeps the minute and the length when a new hour is picked', async () => {
    const user = userEvent.setup()
    const onChange = renderFields()

    await user.click(screen.getByLabelText('Start'))
    await user.click(screen.getByRole('button', { name: '14' }))

    expect(onChange).toHaveBeenCalledWith({ start: '14:30', end: '15:30' })
  })

  it('sets the end from a duration chip', async () => {
    const user = userEvent.setup()
    const onChange = renderFields()

    await user.click(screen.getByRole('radio', { name: '2h' }))

    expect(onChange).toHaveBeenCalledWith({ start: '10:30', end: '12:30' })
  })

  it("disables lengths over the room's maximum", () => {
    renderFields({ maxDuration: 60 })

    expect(screen.getByRole('radio', { name: '1h' })).toBeEnabled()
    expect(screen.getByRole('radio', { name: '2h' })).toBeDisabled()
  })

  it('dims start times that are already too soon today', async () => {
    const user = userEvent.setup()
    renderFields({ minStartMinute: 12 * 60 })

    await user.click(screen.getByLabelText('Start'))

    expect(screen.getByRole('button', { name: '11' })).toBeDisabled()
    expect(screen.getByRole('button', { name: '12' })).toBeEnabled()
  })

  it('books the rest of the day in one tap with "Until closing"', async () => {
    const user = userEvent.setup()
    const onChange = renderFields({ closingMinute: 20 * 60 })

    await user.click(screen.getByRole('radio', { name: 'Until 20:00' }))

    expect(onChange).toHaveBeenCalledWith({ start: '10:30', end: '20:00' })
  })

  it('disables "Until closing" when that would be longer than allowed', () => {
    renderFields({ closingMinute: 20 * 60, maxDuration: 120 })

    expect(screen.getByRole('radio', { name: 'Until 20:00' })).toBeDisabled()
  })

  it('offers "Now" on today, jumping to the earliest bookable slot', async () => {
    const user = userEvent.setup()
    const onChange = renderFields({ minStartMinute: 9 * 60 + 7 })

    await user.click(screen.getByLabelText('Start'))
    await user.click(screen.getByRole('button', { name: 'Now · 09:15' }))

    expect(onChange).toHaveBeenCalledWith({ start: '09:15', end: '10:15' })
  })

  it('does not offer "Now" on another day', async () => {
    const user = userEvent.setup()
    renderFields()

    await user.click(screen.getByLabelText('Start'))

    expect(screen.queryByRole('button', { name: /^Now/ })).not.toBeInTheDocument()
  })
})

