import { useTranslation } from 'react-i18next'
import { Label } from '@/components/ui/label'
import { fromMinutes, nextSlot, toMinutes } from '@/lib/time/buildingTime'
import type { HhMm } from '@/lib/time/buildingTime'
import type { FreeTimeRules } from '@/features/bookings/dragRange'
import { endTimes, startTimes } from '@/features/bookings/freeTimes'
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
  /**
   * A specific room's day (open times, bookings, closures): when given, From offers only its
   * free starts and To only ends before the next booking, closure or closing time — instead
   * of the limits above.
   */
  rules?: FreeTimeRules
  /** The id of a message under the fields about a problem with the time; marks both invalid. */
  errorId?: string
  onChange: (range: { start: HhMm; end: HhMm }) => void
}

/**
 * From and To, each a dropdown of times. From only offers times that haven't passed; To only
 * offers times after From — and, for a specific room, no later than its closing time or
 * its maximum length. Given the room's day (`rules`), only free times are offered at all.
 * Moving From keeps the booking's length where it still fits. Renders two grid cells so the
 * parent lays them out.
 */
export function FromToFields({
  idPrefix,
  start,
  end,
  slotMinutes,
  minStart = 0,
  maxLength = DAY_MINUTES,
  latestEnd = DAY_MINUTES,
  rules,
  errorId,
  onChange,
}: FromToFieldsProps) {
  const { t } = useTranslation()
  const latestEndFor = (s: number) => {
    if (!rules) return Math.min(DAY_MINUTES, s + maxLength, latestEnd)
    const ends = endTimes(s, rules)
    return ends.length > 0 ? ends[ends.length - 1] : s + slotMinutes
  }

  function changeStart(next: HhMm) {
    const s = toMinutes(next)
    const length = Math.max(toMinutes(end) - toMinutes(start), slotMinutes)
    const e = Math.max(Math.min(s + length, latestEndFor(s)), s + slotMinutes)
    onChange({ start: next, end: fromMinutes(e) })
  }

  const startMinute = toMinutes(start)
  const starts = rules ? startTimes(rules) : undefined

  return (
    <>
      <div className="grid gap-2">
        <Label htmlFor={`${idPrefix}-from`}>{t('Booking:From')}</Label>
        <TimePicker
          id={`${idPrefix}-from`}
          value={start}
          slotMinutes={slotMinutes}
          min={minStart}
          max={DAY_MINUTES - slotMinutes}
          times={starts}
          // "Now" only when the first time offered really is the next one from now.
          showNow={minStart > 0 && (!starts || starts[0] === nextSlot(minStart, slotMinutes))}
          errorId={errorId}
          onChange={changeStart}
        />
      </div>
      <div className="grid gap-2">
        <Label htmlFor={`${idPrefix}-to`}>{t('Booking:To')}</Label>
        <TimePicker
          id={`${idPrefix}-to`}
          value={end}
          slotMinutes={slotMinutes}
          min={startMinute + slotMinutes}
          max={latestEndFor(startMinute)}
          times={rules ? endTimes(startMinute, rules) : undefined}
          errorId={errorId}
          onChange={(next) => onChange({ start, end: next })}
        />
      </div>
    </>
  )
}
