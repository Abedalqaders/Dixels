import { Label } from '@/components/ui/label'
import { fromMinutes, toMinutes } from '../../../lib/time/buildingTime'
import type { HhMm } from '../../../lib/time/buildingTime'
import { DurationPicker } from './DurationPicker'
import { StartTimePicker } from './StartTimePicker'

const DAY_MINUTES = 24 * 60

interface TimeRangeFieldsProps {
  idPrefix: string
  start: HhMm
  end: HhMm
  slotMinutes: number
  /** The longest booking allowed (a room's maximum). Omit for a search across rooms. */
  maxDuration?: number
  /** Earliest bookable minute (today: now + notice). */
  minStartMinute?: number
  onChange: (range: { start: HhMm; end: HhMm }) => void
}

/**
 * "Start at … for …" instead of two long From/To lists — the way people think about a
 * booking ("at 10, for an hour"). The end is derived and shown, never typed. Renders two
 * grid cells (Start, Duration) so the parent form lays them out.
 */
export function TimeRangeFields({
  idPrefix,
  start,
  end,
  slotMinutes,
  maxDuration = DAY_MINUTES,
  minStartMinute = 0,
  onChange,
}: TimeRangeFieldsProps) {
  const startMinute = toMinutes(start)
  const duration = Math.max(toMinutes(end) - startMinute, slotMinutes)
  const limitFor = (s: number) => Math.min(maxDuration, DAY_MINUTES - s)

  function changeStart(next: HhMm) {
    // Keep the length when the start moves, trimmed only if it would run past midnight.
    const s = toMinutes(next)
    onChange({ start: next, end: fromMinutes(s + Math.min(duration, limitFor(s))) })
  }

  function changeDuration(minutes: number) {
    onChange({ start, end: fromMinutes(startMinute + minutes) })
  }

  return (
    <>
      <div className="grid gap-2">
        <Label htmlFor={`${idPrefix}-start`}>Start</Label>
        <StartTimePicker
          id={`${idPrefix}-start`}
          value={start}
          slotMinutes={slotMinutes}
          minMinute={minStartMinute}
          onChange={changeStart}
        />
      </div>
      <div className="grid gap-2">
        <Label htmlFor={`${idPrefix}-duration`}>
          Duration
          <span className="ml-auto font-mono text-xs font-normal text-muted-foreground">
            {start}–{end}
          </span>
        </Label>
        <DurationPicker
          id={`${idPrefix}-duration`}
          value={duration}
          slotMinutes={slotMinutes}
          max={limitFor(startMinute)}
          onChange={changeDuration}
        />
      </div>
    </>
  )
}
