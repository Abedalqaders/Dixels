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

/**
 * A room's day at a glance: open time is light, outside hours is shaded, bookings and
 * closures are solid, and the searched window is outlined — so "free until 14:00" or
 * "booked 10–12" is visible without reading. Every row uses the same axis, so rooms can be
 * compared by eye down the list.
 */
export function DayBar({ axis, open, closed, busy, selection, label }: DayBarProps) {
  const span = Math.max(axis.to - axis.from, 1)

  const style = (startMinute: number, endMinute: number) => {
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
    <div className="daybar" role="img" aria-label={label}>
      <div className="daybar-track">
        {open.map((r, i) => (
          <span key={`o${i}`} className="seg open" style={style(r.startMinute, r.endMinute)} />
        ))}
        {closed.map((r, i) => (
          <span key={`c${i}`} className="seg closed" style={style(r.startMinute, r.endMinute)} />
        ))}
        {busy.map((r, i) => (
          <span key={`b${i}`} className={`seg busy${r.isMine ? ' mine' : ''}`} style={style(r.startMinute, r.endMinute)} />
        ))}
        <span className="seg selection" style={style(selection.startMinute, selection.endMinute)} />
      </div>
      <div className="daybar-ticks" aria-hidden="true">
        {ticks.map((m) => (
          <span key={m} style={{ left: `${((m - axis.from) / span) * 100}%` }}>
            {fromMinutes(m).slice(0, 2)}
          </span>
        ))}
      </div>
    </div>
  )
}
