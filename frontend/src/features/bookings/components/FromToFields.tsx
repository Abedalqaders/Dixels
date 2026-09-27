import { Label } from '@/components/ui/label'
import { fromMinutes, toMinutes } from '../../../lib/time/buildingTime'
import type { HhMm } from '../../../lib/time/buildingTime'
import { TimePicker } from './TimePicker'

const DAY_MINUTES = 24 * 60

interface FromToFieldsProps {
  idPrefix: string
  start: HhMm
  end: HhMm
  slotMinutes: number
  /** Earliest allowed start (today: now + notice, else 0). Earlier times aren't offered. */
  minStart?: number
  /** Longest allowed booking — To never offers more than this after From. */
  maxLength?: number
  /** Latest allowed end, e.g. the room's closing time. */
  latestEnd?: number
  onChange: (range: { start: HhMm; end: HhMm }) => void
}

/**
 * From and To, each a tap-to-pick grid. From only offers times that haven't passed; To only
 * offers times after From — and, for a specific room, no later than its closing time or
 * its maximum length. Moving From keeps the booking's length where it still fits. Renders
 * two grid cells so the parent lays them out.
 */
export function FromToFields({
  idPrefix,
  start,
  end,
  slotMinutes,
  minStart = 0,
  maxLength = DAY_MINUTES,
  latestEnd = DAY_MINUTES,
  onChange,
}: FromToFieldsProps) {
  const latestEndFor = (s: number) => Math.min(DAY_MINUTES, s + maxLength, latestEnd)

  function changeStart(next: HhMm) {
    const s = toMinutes(next)
    const length = Math.max(toMinutes(end) - toMinutes(start), slotMinutes)
    const e = Math.max(Math.min(s + length, latestEndFor(s)), s + slotMinutes)
    onChange({ start: next, end: fromMinutes(e) })
  }

  const startMinute = toMinutes(start)

  return (
    <>
      <div className="grid gap-2">
        <Label htmlFor={`${idPrefix}-from`}>From</Label>
        <TimePicker
          id={`${idPrefix}-from`}
          value={start}
          slotMinutes={slotMinutes}
          min={minStart}
          max={DAY_MINUTES - slotMinutes}
          showNow={minStart > 0}
          onChange={changeStart}
        />
      </div>
      <div className="grid gap-2">
        <Label htmlFor={`${idPrefix}-to`}>To</Label>
        <TimePicker
          id={`${idPrefix}-to`}
          value={end}
          slotMinutes={slotMinutes}
          min={startMinute + slotMinutes}
          max={latestEndFor(startMinute)}
          onChange={(next) => onChange({ start, end: next })}
        />
      </div>
    </>
  )
}
