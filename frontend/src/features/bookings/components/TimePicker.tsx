import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { fromMinutes, nextSlot } from '@/lib/time/buildingTime'
import type { HhMm } from '@/lib/time/buildingTime'

interface TimePickerProps {
  id: string
  value: HhMm
  slotMinutes: number
  /** Earliest time offered (minutes from midnight). Anything earlier isn't listed at all. */
  min?: number
  /** Latest time offered. 1440 offers "24:00" (midnight) — for an end time. */
  max?: number
  /** Label the first time "now" — for a start time on today's date. */
  showNow?: boolean
  /** The id of a message under the field about a problem with the time; marks the field invalid. */
  errorId?: string
  onChange: (time: HhMm) => void
}

const DAY_MINUTES = 24 * 60

function label(time: HhMm): string {
  return time === '24:00' ? '24:00 (midnight)' : time
}

/**
 * A plain shadcn Select of every allowed slot between `min` and `max` — past times, or an
 * end before the start, simply aren't in the list. Typing a digit jumps through the list.
 */
export function TimePicker({ id, value, slotMinutes, min = 0, max = DAY_MINUTES - slotMinutes, showNow, errorId, onChange }: TimePickerProps) {
  const allowed: HhMm[] = []
  for (let t = nextSlot(min, slotMinutes); t <= max; t += slotMinutes) allowed.push(fromMinutes(t))

  return (
    // A value outside the list (e.g. a start that has just slipped into the past) shows as
    // the placeholder instead of a blank box, until a listed time is picked.
    <Select value={allowed.includes(value) ? value : ''} onValueChange={(v) => onChange(v as HhMm)} disabled={allowed.length === 0}>
      <SelectTrigger id={id} className="w-full font-mono" aria-invalid={errorId ? true : undefined} aria-describedby={errorId}>
        <SelectValue placeholder={allowed.length === 0 ? 'No times left' : label(value)} />
      </SelectTrigger>
      <SelectContent className="max-h-72">
        {allowed.map((t, i) => (
          <SelectItem key={t} value={t} className="font-mono">
            {label(t)}
            {showNow && i === 0 && ' · now'}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
