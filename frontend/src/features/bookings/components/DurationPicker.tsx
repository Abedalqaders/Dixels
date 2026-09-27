import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { formatDuration } from '../format'

interface DurationPickerProps {
  id: string
  /** Minutes. */
  value: number
  slotMinutes: number
  /** Longest allowed — the room's maximum and/or what's left of the day. Longer choices are disabled. */
  max: number
  /**
   * The length that would run exactly to closing time, offered as "Until HH:mm" — the
   * common case for desks (book the rest of the day in one tap). Omitted when unknown.
   */
  untilClosing?: { minutes: number; label: string }
  onChange: (minutes: number) => void
}

const UNTIL = 'until'

const QUICK = [30, 60, 90, 120]

/**
 * "For how long" as one tap for the common lengths (30 min, 1h, 1½h, 2h), with every other
 * length on the slot grid under "More". Lengths over the limit are shown but disabled, so
 * it's clear why a 3h booking isn't offered rather than it silently missing.
 */
export function DurationPicker({ id, value, slotMinutes, max, untilClosing, onChange }: DurationPickerProps) {
  const quick = QUICK.filter((m) => m % slotMinutes === 0)
  const more: number[] = []
  for (let m = slotMinutes; m <= Math.max(max, slotMinutes); m += slotMinutes) {
    if (!quick.includes(m)) more.push(m)
  }
  const until = untilClosing && untilClosing.minutes > 0 ? untilClosing : null
  const isUntil = until !== null && value === until.minutes
  const isQuick = quick.includes(value) && !isUntil

  return (
    <div className="flex flex-wrap items-center gap-2">
      <ToggleGroup
        id={id}
        type="single"
        variant="outline"
        size="sm"
        aria-label="Duration"
        value={isUntil ? UNTIL : isQuick ? String(value) : ''}
        onValueChange={(v) => {
          if (v === UNTIL && until) onChange(until.minutes)
          else if (v) onChange(Number(v))
        }}
      >
        {quick.map((m) => (
          <ToggleGroupItem
            key={m}
            value={String(m)}
            disabled={m > max}
            title={m > max ? `Longer than the ${formatDuration(max)} allowed here` : undefined}
            className="px-3 font-mono"
          >
            {m === 90 ? '1½h' : formatDuration(m)}
          </ToggleGroupItem>
        ))}
        {until && (
          <ToggleGroupItem
            value={UNTIL}
            disabled={until.minutes > max}
            title={until.minutes > max ? `Longer than the ${formatDuration(max)} allowed here` : undefined}
            className="px-3"
          >
            {until.label}
          </ToggleGroupItem>
        )}
      </ToggleGroup>

      {more.length > 0 && (
        <Select value={isQuick || isUntil ? '' : String(value)} onValueChange={(v) => onChange(Number(v))}>
          <SelectTrigger size="sm" className="w-28 font-mono" aria-label="Other duration">
            <SelectValue placeholder="More" />
          </SelectTrigger>
          <SelectContent className="max-h-72">
            {more.map((m) => (
              <SelectItem key={m} value={String(m)} className="font-mono">
                {formatDuration(m)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      )}
    </div>
  )
}
