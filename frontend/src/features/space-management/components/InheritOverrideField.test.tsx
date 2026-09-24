// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { InheritOverrideField } from './InheritOverrideField'

describe('InheritOverrideField', () => {
  it('shows "Inherit" and calls onToggle when the switch is clicked', async () => {
    const user = userEvent.setup()
    const onToggle = vi.fn()
    render(
      <InheritOverrideField label="Operating days" isOverridden={false} onToggle={onToggle} note="Inherited from Building">
        <p>field content</p>
      </InheritOverrideField>,
    )

    expect(screen.getByText('Inherit')).toBeInTheDocument()
    await user.click(screen.getByRole('button'))
    expect(onToggle).toHaveBeenCalledTimes(1)
  })

  it('shows "Override" once toggled on', () => {
    render(
      <InheritOverrideField label="Operating days" isOverridden onToggle={() => {}} note="Overrides Building">
        <p>field content</p>
      </InheritOverrideField>,
    )

    expect(screen.getByText('Override')).toBeInTheDocument()
  })

  it('supports custom on/off labels for fields that are not really "inherited"', () => {
    render(
      <InheritOverrideField
        label="Minimum attendees"
        isOverridden
        onToggle={() => {}}
        note="Rejects bookings for fewer than 4 attendees."
        onLabel="Set"
        offLabel="Not set"
      >
        <p>field content</p>
      </InheritOverrideField>,
    )

    expect(screen.getByText('Set')).toBeInTheDocument()
    expect(screen.queryByText('Override')).not.toBeInTheDocument()
  })
})
