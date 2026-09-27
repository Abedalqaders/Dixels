import { cn } from '@/lib/utils'
import { fromMinutes } from '../../../lib/time/buildingTime'
import type { DayRangeDto } from '../api/bookingsApi'
import type { DayAxis } from '../dayAxis'

interface DayBarProps {
  axis: DayAxis
  open: DayRangeDto[]
  closed: DayRangeDto[]
  busy: DayRangeDto[]
  /** The searched window, outlined on the bar. */
  selection: { startMinute: number; endMinute: number }
  /** A short spoken summary — the bar itself is purely visual. */
  label: string
}

// Outside opening hours: a faint hatch in the theme's neutral tones.
const OUTSIDE_HOURS = {
  backgroundImage:
    'repeating-linear-gradient(135deg, var(--surface-sunken) 0 4px, var(--border-subtle) 4px 6px)',
}

/**
 * A room's day at a glance: open time tinted with the theme's own hue, outside hours
 * hatched, other people's bookings neutral, yours in the theme hue, closures in the
 * "blocked" state colour, and the searched window outlined. Every colour comes from the
 * theme tokens (see ui.css `--color-slot-*`), so it follows light and dark. All rows share
 * one axis, so rooms can be compared by eye down the list.
 */
export function DayBar({ axis, open, closed, busy, selection, label }: DayBarProps) {
  const span = Math.max(axis.to - axis.from, 1)

  const place = (startMinute: number, endMinute: number) => {
    const start = Math.max(startMinute, axis.from)
    const end = Math.min(endMinute, axis.to)
    return {
      left: `${((start - axis.from) / span) * 100}%`,
      width: `${(Math.max(end - start, 0) / span) * 100}%`,
    }
  }

  const ticks: number[] = []
  for (let m = Math.ceil(axis.from / 120) * 120; m <= axis.to; m += 120) ticks.push(m)

  return (
    <div className="min-w-0" role="img" aria-label={label}>
      <div className="relative h-3.5 overflow-hidden rounded-full" style={OUTSIDE_HOURS}>
        {open.map((r, i) => (
          <span key={`o${i}`} className="absolute inset-y-0 bg-slot-open" style={place(r.startMinute, r.endMinute)} />
        ))}
        {closed.map((r, i) => (
          <span key={`c${i}`} className="absolute inset-y-0 bg-slot-closed" style={place(r.startMinute, r.endMinute)} />
        ))}
        {busy.map((r, i) => (
          <span
            key={`b${i}`}
            className={cn('absolute inset-y-0', r.isMine ? 'bg-slot-mine' : 'bg-slot-busy')}
            style={place(r.startMinute, r.endMinute)}
          />
        ))}
        <span
          className="absolute inset-y-0 rounded-sm border-2 border-solid border-foreground"
          style={place(selection.startMinute, selection.endMinute)}
        />
      </div>
      <div className="relative mt-0.5 h-3.5 font-mono text-[10px] text-muted-foreground" aria-hidden="true">
        {ticks.map((m) => (
          <span key={m} className="absolute -translate-x-1/2" style={{ left: `${((m - axis.from) / span) * 100}%` }}>
            {fromMinutes(m).slice(0, 2)}
          </span>
        ))}
      </div>
    </div>
  )
}
