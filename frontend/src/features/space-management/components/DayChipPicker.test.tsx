// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DayChipPicker } from './DayChipPicker'
import { OperatingDays } from '../domain/operatingDays'

describe('DayChipPicker', () => {
  it('disables days outside the parent set (proactive grey-out, not reactive rejection)', () => {
    const parent = OperatingDays.fromDayNames(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'])
    render(<DayChipPicker value={OperatingDays.None} parent={parent} disabled={false} onChange={() => {}} />)

    expect(screen.getByRole('button', { name: 'Sat' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Sun' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Mon' })).not.toBeDisabled()
  })

  it('toggles an allowed day on click', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<DayChipPicker value={OperatingDays.None} parent={OperatingDays.Everyday} disabled={false} onChange={onChange} />)

    await user.click(screen.getByRole('button', { name: 'Mon' }))

    expect(onChange).toHaveBeenCalledTimes(1)
    const next = onChange.mock.calls[0][0] as OperatingDays
    expect(next.contains('Monday')).toBe(true)
  })

  it('does nothing when a disallowed (disabled) day is clicked', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    const parent = OperatingDays.fromDayNames(['Monday'])
    render(<DayChipPicker value={OperatingDays.None} parent={parent} disabled={false} onChange={onChange} />)

    await user.click(screen.getByRole('button', { name: 'Sat' }))

    expect(onChange).not.toHaveBeenCalled()
  })

  it('disables every chip when the field is in Inherit mode', () => {
    render(<DayChipPicker value={OperatingDays.Everyday} parent={OperatingDays.Everyday} disabled onChange={() => {}} />)

    expect(screen.getByRole('button', { name: 'Mon' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Sun' })).toBeDisabled()
  })
})
