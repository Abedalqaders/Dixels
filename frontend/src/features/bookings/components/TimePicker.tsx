import { useState } from 'react'
import { Clock } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { fromMinutes, nextSlot, toMinutes } from '../../../lib/time/buildingTime'
import type { HhMm } from '../../../lib/time/buildingTime'

interface TimePickerProps {
  id: string
  value: HhMm
  slotMinutes: number
  /** Earliest time offered (minutes from midnight). Anything earlier isn't shown at all. */
  min?: number
  /** Latest time offered. 1440 offers "24:00" (midnight) — for an end time. */
  max?: number
  /** Show a "Now" shortcut that jumps to `min` — for a start time on today's date. */
  showNow?: boolean
  onChange: (time: HhMm) => void
}

const DAY_MINUTES = 24 * 60

/**
 * A time in two taps instead of scrolling a 96-item list: pick the hour from a grid, then
 * the minute. Only times between `min` and `max` are offered — past times, or an end
 * before the start, simply aren't there to pick. Picking an hour keeps the minute when
 * it's still allowed; picking a minute closes the picker.
 */
export function TimePicker({ id, value, slotMinutes, min = 0, max = DAY_MINUTES - slotMinutes, showNow, onChange }: TimePickerProps) {
  const [open, setOpen] = useState(false)

  const allowed: number[] = []
  for (let t = nextSlot(min, slotMinutes); t <= max; t += slotMinutes) allowed.push(t)

  const hours = [...new Set(allowed.map((t) => Math.floor(t / 60)))]
  const current = toMinutes(value)
  const currentHour = Math.floor(current / 60)
  const shownHour = hours.includes(currentHour) ? currentHour : hours[0]
  const minutesInHour = allowed.filter((t) => Math.floor(t / 60) === shownHour).map((t) => t % 60)

  function pickHour(h: number) {
    const keep = h * 60 + (current % 60)
    const inHour = allowed.filter((t) => Math.floor(t / 60) === h)
    onChange(fromMinutes(inHour.includes(keep) ? keep : inHour[0]))
  }

  function pickMinute(m: number) {
    onChange(fromMinutes(shownHour * 60 + m))
    setOpen(false)
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button id={id} variant="outline" className="w-full justify-start font-mono font-normal">
          <Clock className="text-muted-foreground" />
          {value === '24:00' ? '24:00 (midnight)' : value}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-72 p-3" align="start">
        {allowed.length === 0 ? (
          <p className="text-sm text-muted-foreground">No times left to pick on this day.</p>
        ) : (
          <>
            {showNow && (
              <Button
                type="button"
                variant="secondary"
                size="sm"
                className="mb-3 w-full"
                onClick={() => {
                  onChange(fromMinutes(allowed[0]))
                  setOpen(false)
                }}
              >
                Now · {fromMinutes(allowed[0])}
              </Button>
            )}

            <p className="mb-2 text-xs font-medium text-muted-foreground">Hour</p>
            <div className="grid grid-cols-6 gap-1" role="group" aria-label="Hour">
              {hours.map((h) => (
                <Button
                  key={h}
                  type="button"
                  size="sm"
                  variant={h === shownHour ? 'default' : 'ghost'}
                  aria-pressed={h === shownHour}
                  className="font-mono"
                  onClick={() => pickHour(h)}
                >
                  {String(h).padStart(2, '0')}
                </Button>
              ))}
            </div>

            <p className="mt-3 mb-2 text-xs font-medium text-muted-foreground">Minute</p>
            <div className="grid grid-cols-4 gap-1" role="group" aria-label="Minute">
              {minutesInHour.map((m) => (
                <Button
                  key={m}
                  type="button"
                  size="sm"
                  variant={shownHour === currentHour && m === current % 60 ? 'default' : 'outline'}
                  aria-pressed={shownHour === currentHour && m === current % 60}
                  className="font-mono"
                  onClick={() => pickMinute(m)}
                >
                  :{String(m).padStart(2, '0')}
                </Button>
              ))}
            </div>
          </>
        )}
      </PopoverContent>
    </Popover>
  )
}
