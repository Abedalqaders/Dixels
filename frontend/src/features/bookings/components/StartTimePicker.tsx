import { useState } from 'react'
import { Clock } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { fromMinutes, nextSlot, toMinutes } from '../../../lib/time/buildingTime'
import type { HhMm } from '../../../lib/time/buildingTime'

interface StartTimePickerProps {
  id: string
  value: HhMm
  slotMinutes: number
  /** Earliest bookable minute today (now + notice); earlier times are dimmed. 0 on other days. */
  minMinute?: number
  onChange: (time: HhMm) => void
}

const HOURS = Array.from({ length: 24 }, (_, h) => h)

/**
 * A start time in two taps instead of scrolling a 96-item list: pick the hour from a grid,
 * then the minute (00/15/30/45 on a 15-minute grid). Picking the hour keeps the current
 * minute, so "10:30 → 11:30" is one tap; picking a minute closes the picker. On today's
 * date a "Now" shortcut jumps to the earliest bookable slot, for walk-up bookings.
 */
export function StartTimePicker({ id, value, slotMinutes, minMinute = 0, onChange }: StartTimePickerProps) {
  const [open, setOpen] = useState(false)
  const current = toMinutes(value)
  const hour = Math.floor(current / 60)
  const minute = current % 60
  const minutes = Array.from({ length: 60 / slotMinutes }, (_, i) => i * slotMinutes)

  // "Now" = the earliest start allowed today (now + notice), rounded up onto the grid.
  const nowSlot = nextSlot(minMinute, slotMinutes)

  // An hour is unavailable only when even its last slot is before the earliest start.
  const hourDisabled = (h: number) => h * 60 + (60 - slotMinutes) < minMinute
  const minuteDisabled = (m: number) => hour * 60 + m < minMinute

  function pickHour(h: number) {
    // Keep the minute, unless that exact time is already past — then the first allowed one.
    const keep = h * 60 + minute
    const firstAllowed = minutes.map((m) => h * 60 + m).find((t) => t >= minMinute) ?? keep
    onChange(fromMinutes(keep >= minMinute ? keep : firstAllowed))
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button id={id} variant="outline" className="w-full justify-start font-mono font-normal">
          <Clock className="text-muted-foreground" />
          {value}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-72 p-3" align="start">
        {minMinute > 0 && nowSlot < 24 * 60 && (
          <Button
            type="button"
            variant="secondary"
            size="sm"
            className="mb-3 w-full"
            onClick={() => {
              onChange(fromMinutes(nowSlot))
              setOpen(false)
            }}
          >
            Now · {fromMinutes(nowSlot)}
          </Button>
        )}
        <p className="mb-2 text-xs font-medium text-muted-foreground">Hour</p>
        <div className="grid grid-cols-6 gap-1" role="group" aria-label="Hour">
          {HOURS.map((h) => (
            <Button
              key={h}
              type="button"
              size="sm"
              variant={h === hour ? 'default' : 'ghost'}
              aria-pressed={h === hour}
              disabled={hourDisabled(h)}
              className="font-mono"
              onClick={() => pickHour(h)}
            >
              {String(h).padStart(2, '0')}
            </Button>
          ))}
        </div>

        <p className="mt-3 mb-2 text-xs font-medium text-muted-foreground">Minute</p>
        <div className="grid grid-cols-4 gap-1" role="group" aria-label="Minute">
          {minutes.map((m) => (
            <Button
              key={m}
              type="button"
              size="sm"
              variant={m === minute ? 'default' : 'outline'}
              aria-pressed={m === minute}
              disabled={minuteDisabled(m)}
              className="font-mono"
              onClick={() => {
                onChange(fromMinutes(hour * 60 + m))
                setOpen(false)
              }}
            >
              :{String(m).padStart(2, '0')}
            </Button>
          ))}
        </div>
      </PopoverContent>
    </Popover>
  )
}
