// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { HoursRangeInput } from './HoursRangeInput'
import { OperatingWindow } from '../domain/operatingWindow'

describe('HoursRangeInput', () => {
  it('disables the "Open 24 hours" checkbox when the parent is not open 24 hours', () => {
    const parent = new OperatingWindow('07:00', '20:00')
    render(<HoursRangeInput value={parent} parent={parent} disabled={false} onChange={() => {}} />)

    expect(screen.getByRole('checkbox', { name: /open 24 hours/i })).toBeDisabled()
  })

  it('enables the checkbox and switches to FullDay when the parent is open 24 hours', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    const value = new OperatingWindow('07:00', '20:00')
    render(<HoursRangeInput value={value} parent={OperatingWindow.FullDay} disabled={false} onChange={onChange} />)

    const checkbox = screen.getByRole('checkbox', { name: /open 24 hours/i })
    expect(checkbox).not.toBeDisabled()

    await user.click(checkbox)

    expect(onChange).toHaveBeenCalledWith(OperatingWindow.FullDay)
  })

  it('constrains the time inputs to the parent range when the parent does not wrap', () => {
    const parent = new OperatingWindow('07:00', '20:00')
    render(<HoursRangeInput value={parent} parent={parent} disabled={false} onChange={() => {}} />)

    const [openInput, closeInput] = screen.getAllByDisplayValue(/07:00|20:00/)
    expect(openInput).toHaveAttribute('min', '07:00')
    expect(closeInput).toHaveAttribute('max', '20:00')
  })
})
