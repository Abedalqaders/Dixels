import { CircleAlert, CircleCheck, LoaderCircle } from 'lucide-react'
import type { PreviewState } from '../hooks/useBookingPreview'

interface VerdictPanelProps {
  state: PreviewState
  /** "Tue 29 Sep, 10:00–11:00" — shown when the slot is free. */
  slotLabel: string
  timezone: string
}

const BOX = 'flex gap-2.5 rounded-md px-4 py-3 text-sm [&>svg]:mt-0.5 [&>svg]:size-4 [&>svg]:flex-none'

/**
 * The live answer to "can I book this?". Every broken rule is listed, most fundamental
 * first; the first is emphasised because fixing it may make the others irrelevant (no
 * point adjusting the time on a room that's closed all day). Colours come from the theme:
 * the theme hue for "free", the blocked state colour for a rejection.
 */
export function VerdictPanel({ state, slotLabel, timezone }: VerdictPanelProps) {
  if (state.status === 'idle') {
    return null
  }

  if (state.status === 'checking') {
    return (
      <div className={`${BOX} bg-muted text-muted-foreground`} role="status" aria-live="polite">
        <LoaderCircle className="animate-spin" />
        Checking availability…
      </div>
    )
  }

  if (state.status === 'error') {
    return (
      <div className={`${BOX} bg-slot-closed`} role="alert">
        <CircleAlert />
        {state.message}
      </div>
    )
  }

  const { preview } = state

  if (preview.isValid) {
    return (
      <div className={`${BOX} bg-slot-open`} role="status" aria-live="polite">
        <CircleCheck className="text-brand" />
        <span>
          <strong>Available</strong> — {slotLabel} ({timezone})
        </span>
      </div>
    )
  }

  const [first, ...rest] = preview.violations

  return (
    <div className={`${BOX} bg-slot-closed`} role="alert">
      <CircleAlert />
      <div>
        <strong>{first.message}</strong>
        {rest.length > 0 && (
          <ul className="mt-2 list-disc space-y-1 pl-5">
            {rest.map((v) => (
              <li key={v.code + v.message}>{v.message}</li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
