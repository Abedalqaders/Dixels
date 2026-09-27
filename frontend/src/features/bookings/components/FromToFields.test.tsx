// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { FromToFields } from './FromToFields'

function renderFields(props: Partial<Parameters<typeof FromToFields>[0]> = {}) {
  const onChange = vi.fn()
  render(
    <div>
      <FromToFields idPrefix="t" start="16:00" end="17:00" slotMinutes={15} onChange={onChange} {...props} />
    </div>,
  )
  return onChange
}

const hourButtons = () =>
  screen.getAllByRole('button').filter((b) => /^\d{2}$/.test(b.textContent ?? '')).map((b) => b.textContent)

describe('FromToFields', () => {
  it('never offers a To earlier than From', async () => {
    const user = userEvent.setup()
    renderFields({ end: '16:30' })

    await user.click(screen.getByLabelText('To'))

    // From is 16:00, so To starts at 16:15: no hour before 16, and no 16:00, is offered.
    expect(hourButtons()[0]).toBe('16')
    expect(screen.queryByRole('button', { name: ':00' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: ':15' })).toBeInTheDocument()
  })

  it('does not offer past times for From on today', async () => {
    const user = userEvent.setup()
    renderFields({ minStart: 14 * 60 + 5 })

    await user.click(screen.getByLabelText('From'))

    expect(hourButtons()[0]).toBe('14')
    expect(hourButtons()).not.toContain('13')
    expect(screen.getByRole('button', { name: 'Now · 14:15' })).toBeInTheDocument()
  })

  it("stops To at the room's closing time and maximum length", async () => {
    const user = userEvent.setup()
    renderFields({ maxLength: 120, latestEnd: 20 * 60 })

    await user.click(screen.getByLabelText('To'))

    // 16:00 + 2h max = 18:00, earlier than the 20:00 close.
    expect(hourButtons().at(-1)).toBe('18')
  })

  it('keeps the length when From moves', async () => {
    const user = userEvent.setup()
    const onChange = renderFields()

    await user.click(screen.getByLabelText('From'))
    await user.click(screen.getByRole('button', { name: '09' }))

    expect(onChange).toHaveBeenCalledWith({ start: '09:00', end: '10:00' })
  })

  it('sets To from the grid', async () => {
    const user = userEvent.setup()
    const onChange = renderFields()

    await user.click(screen.getByLabelText('To'))
    await user.click(screen.getByRole('button', { name: '18' }))

    expect(onChange).toHaveBeenCalledWith({ start: '16:00', end: '18:00' })
  })
})
