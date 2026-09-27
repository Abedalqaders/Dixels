import { useState } from 'react'
import { Timer } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'
import { formatDuration } from '../format'

interface DurationPickerProps {
  id: string
  /** Minutes. */
  value: number
  slotMinutes: number
  /** Longest allowed — the room's maximum and/or what's left of the day. Longer choices are disabled. */
  max: number
  /** "HH:mm" the booking ends at with the current length — shown on the field itself. */
  endsAt: string
  /**
   * The length that would run exactly to closing time, offered as "Until HH:mm" — the
   * common case for desks (book the rest of the day in one tap). Omitted when unknown.
   */
  untilClosing?: { minutes: number; label: string }
  onChange: (minutes: number) => void
}

const QUICK = [30, 60, 90, 120]
const UNTIL = 'until'

const label = (minutes: number) => (minutes === 90 ? '1½h' : formatDuration(minutes))

/**
 * "For how long", as one field the same size as every other filter ("1h · ends 11:00").
 * It opens a small panel: the common lengths and "Until closing" as one-tap chips, then
 * every other length on the slot grid. Lengths over the limit are shown but disabled, so
 * it's clear why a 3h booking isn't offered rather than it silently missing.
 */
export function DurationPicker({ id, value, slotMinutes, max, endsAt, untilClosing, onChange }: DurationPickerProps) {
  const [open, setOpen] = useState(false)

  const quick = QUICK.filter((m) => m % slotMinutes === 0)
  const others: number[] = []
  for (let m = slotMinutes; m <= Math.max(max, slotMinutes); m += slotMinutes) {
    if (!quick.includes(m)) others.push(m)
  }

  const until = untilClosing && untilClosing.minutes > 0 ? untilClosing : null
  const isUntil = until !== null && value === until.minutes

  function pick(minutes: number) {
    onChange(minutes)
    setOpen(false)
  }

  const tooLong = (m: number) => (m > max ? `Longer than the ${formatDuration(max)} allowed here` : undefined)

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button id={id} variant="outline" className="w-full justify-start font-mono font-normal">
          <Timer className="text-muted-foreground" />
          <span className="truncate">
            {isUntil ? until.label : label(value)}
            <span className="text-muted-foreground"> · ends {endsAt}</span>
          </span>
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-72 p-3" align="start">
        <ToggleGroup
          type="single"
          variant="outline"
          size="sm"
          aria-label="Duration"
          className="w-full flex-wrap"
          value={isUntil ? UNTIL : quick.includes(value) ? String(value) : ''}
          onValueChange={(v) => {
            if (v === UNTIL && until) pick(until.minutes)
            else if (v) pick(Number(v))
          }}
        >
          {quick.map((m) => (
            <ToggleGroupItem key={m} value={String(m)} disabled={m > max} title={tooLong(m)} className="flex-1 font-mono">
              {label(m)}
            </ToggleGroupItem>
          ))}
        </ToggleGroup>

        {until && (
          <Button
            type="button"
            variant={isUntil ? 'default' : 'secondary'}
            size="sm"
            className="mt-2 w-full"
            aria-pressed={isUntil}
            disabled={until.minutes > max}
            title={tooLong(until.minutes)}
            onClick={() => pick(until.minutes)}
          >
            {until.label}
          </Button>
        )}

        {others.length > 0 && (
          <>
            <p className="mt-3 mb-2 text-xs font-medium text-muted-foreground">Other lengths</p>
            <div className="grid max-h-40 grid-cols-4 gap-1 overflow-y-auto" role="group" aria-label="Other lengths">
              {others.map((m) => (
                <Button
                  key={m}
                  type="button"
                  size="sm"
                  variant={m === value && !isUntil ? 'default' : 'ghost'}
                  aria-pressed={m === value && !isUntil}
                  className="font-mono"
                  onClick={() => pick(m)}
                >
                  {formatDuration(m)}
                </Button>
              ))}
            </div>
          </>
        )}
      </PopoverContent>
    </Popover>
  )
}
