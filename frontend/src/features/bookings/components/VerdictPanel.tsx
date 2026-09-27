import type { PreviewState } from '../hooks/useBookingPreview'

interface VerdictPanelProps {
  state: PreviewState
  /** "Tue 29 Sep, 10:00–11:00" — shown when the slot is free. */
  slotLabel: string
  timezone: string
}

/**
 * The live answer to "can I book this?". Every broken rule is listed, most fundamental
 * first; the first is emphasised because fixing it may make the others irrelevant (no
 * point adjusting the time on a room that's closed all day).
 */
export function VerdictPanel({ state, slotLabel, timezone }: VerdictPanelProps) {
  if (state.status === 'idle') {
    return null
  }

  if (state.status === 'checking') {
    return (
      <div className="verdict checking" role="status" aria-live="polite">
        Checking availability…
      </div>
    )
  }

  if (state.status === 'error') {
    return (
      <div className="verdict bad" role="alert">
        {state.message}
      </div>
    )
  }

  const { preview } = state

  if (preview.isValid) {
    return (
      <div className="verdict ok" role="status" aria-live="polite">
        <strong>Available</strong> — {slotLabel} ({timezone})
      </div>
    )
  }

  const [first, ...rest] = preview.violations

  return (
    <div className="verdict bad" role="alert">
      <strong>{first.message}</strong>
      {rest.length > 0 && (
        <ul>
          {rest.map((v) => (
            <li key={v.code + v.message}>{v.message}</li>
          ))}
        </ul>
      )}
    </div>
  )
}
