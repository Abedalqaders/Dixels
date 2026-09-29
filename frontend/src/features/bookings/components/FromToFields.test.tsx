// @vitest-environment jsdom
import { beforeAll, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { FromToFields } from './FromToFields'

// Radix Select calls pointer-capture and scrollIntoView, which jsdom doesn't implement.
beforeAll(() => {
  Element.prototype.hasPointerCapture ??= () => false
  Element.prototype.releasePointerCapture ??= () => {}
  Element.prototype.scrollIntoView ??= () => {}
})

function renderFields(props: Partial<Parameters<typeof FromToFields>[0]> = {}) {
  const onChange = vi.fn()
  render(
    <div>
      <FromToFields idPrefix="t" start="16:00" end="17:00" slotMinutes={15} onChange={onChange} {...props} />
    </div>,
  )
  return onChange
}

const offered = () => screen.getAllByRole('option').map((o) => o.textContent)

describe('FromToFields', () => {
  it('never offers a To earlier than From', async () => {
    const user = userEvent.setup()
    renderFields({ end: '16:30' })

    await user.click(screen.getByLabelText('To'))

    // From is 16:00, so To starts at 16:15.
    expect(offered()[0]).toBe('16:15')
    expect(offered()).not.toContain('16:00')
  })

  it('does not offer past times for From on today', async () => {
    const user = userEvent.setup()
    renderFields({ minStart: 14 * 60 + 5 })

    await user.click(screen.getByLabelText('From'))

    expect(offered()[0]).toBe('14:15 · now')
    expect(offered()).not.toContain('14:00')
  })

  it("stops To at the room's closing time and maximum length", async () => {
    const user = userEvent.setup()
    renderFields({ maxLength: 120, latestEnd: 20 * 60 })

    await user.click(screen.getByLabelText('To'))

    // 16:00 + 2h max = 18:00, earlier than the 20:00 close.
    expect(offered().at(-1)).toBe('18:00')
  })

  it('offers midnight as the last end on a 24h day', async () => {
    const user = userEvent.setup()
    renderFields()

    await user.click(screen.getByLabelText('To'))

    expect(offered().at(-1)).toBe('24:00 (midnight)')
  })

  it('keeps the length when From moves', async () => {
    const user = userEvent.setup()
    const onChange = renderFields()

    await user.click(screen.getByLabelText('From'))
    await user.click(screen.getByRole('option', { name: '09:00' }))

    expect(onChange).toHaveBeenCalledWith({ start: '09:00', end: '10:00' })
  })

  it('sets To from the list', async () => {
    const user = userEvent.setup()
    const onChange = renderFields()

    await user.click(screen.getByLabelText('To'))
    await user.click(screen.getByRole('option', { name: '18:00' }))

    expect(onChange).toHaveBeenCalledWith({ start: '16:00', end: '18:00' })
  })
})
