import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  addDays,
  formatDate,
  nowInZone,
  toLocalDateTime,
  toMinutes,
} from '@/lib/time/buildingTime'
import { ApiError, createBooking, createSeries } from '@/features/bookings/api/bookingsApi'
import type {
  BookableBuildingDto,
  BookableSpaceDto,
  BookingDto,
  BookingRequestDto,
  RecurrenceDto,
  SeriesRequestDto,
} from '@/features/bookings/api/bookingsApi'
import { formatDays, formatDuration, formatHours } from '@/features/bookings/format'
import { useBookingPreview } from '@/features/bookings/hooks/useBookingPreview'
import { useSeriesPreview } from '@/features/bookings/hooks/useSeriesPreview'
import { defaultEndDate, NO_REPEAT, repeatPresets } from '@/features/bookings/recurrence'
import type { RepeatValue } from '@/features/bookings/recurrence'
import { closingMinute, rememberDuration } from '@/features/bookings/preferences'
import { emitBookingsChanged } from '@/features/bookings/bookingEvents'
import { suggestSlot } from '@/features/bookings/suggestSlot'
import type { Slot } from '@/features/bookings/suggestSlot'
import { DatePicker } from './DatePicker'
import { FromToFields } from './FromToFields'
import { RepeatField } from './RepeatField'
import { SeriesPreviewList } from './SeriesPreviewList'
import { VerdictPanel } from './VerdictPanel'

interface BookingFormProps {
  token: string
  building: BookableBuildingDto
  space: BookableSpaceDto
  floorName: string
  onClose: () => void
  /** The booking made — for a recurring one, its first date, with how many dates were booked. */
  onBooked: (booking: BookingDto, count?: number) => void
  /** Pre-fill from a search ("free 10:00–11:00"); otherwise the next sensible slot is suggested. */
  initialSlot?: Slot
  initialAttendees?: number
}

export function BookingForm({
  token,
  building,
  space,
  floorName,
  onClose,
  onBooked,
  initialSlot,
  initialAttendees,
}: BookingFormProps) {
  const [initial] = useState(() => initialSlot ?? suggestSlot(building, space))
  const [title, setTitle] = useState('')
  const [date, setDate] = useState(initial.date)
  const [start, setStart] = useState(initial.start)
  const [end, setEnd] = useState(initial.end)
  const [attendees, setAttendees] = useState(initialAttendees ?? space.minAttendees ?? 1)
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  // One key per opening of the form: a retry of the same attempt (double-click, network
  // blip) can never create a second booking — the server answers it with the first one.
  const [idempotencyKey] = useState(() => crypto.randomUUID())
  const [seriesKey] = useState(() => crypto.randomUUID())

  // Repeat (Teams-style): a quick choice worded from the date, or a custom rule. The
  // dates the person unticks in the preview are left out when booking.
  const [repeat, setRepeat] = useState<RepeatValue>(NO_REPEAT)
  const [skipped, setSkipped] = useState<Set<string>>(() => new Set())

  const slot = building.slotMinutes
  const now = nowInZone(building.timezone)
  const today = now.date
  const lastDate = addDays(today, building.maxHorizonDays)
  const seriesLastDate = addDays(today, Math.max(building.maxSeriesHorizonDays ?? 0, building.maxHorizonDays))
  const openDays = space.days.value

  // The rule the Repeat field stands for on the current date: a quick choice follows the
  // date ("Weekly on Tuesday" becomes "…on Wednesday" when the date moves), keeping its end
  // date unless that's now before the first one.
  const rule = useMemo<RecurrenceDto | null>(() => {
    if (repeat.choice === 'none') return null
    if (repeat.choice === 'custom') return repeat.custom
    const preset = repeatPresets(date, openDays).find((p) => p.choice === repeat.choice)
    if (!preset?.rule) return null
    const draft = preset.rule(date)
    const end = repeat.endDate && repeat.endDate >= date ? repeat.endDate : defaultEndDate(date, draft.frequency, seriesLastDate)
    return preset.rule(end)
  }, [repeat, date, openDays, seriesLastDate])

  // Only the fields that affect the rules — the title is deliberately left out, so typing
  // one doesn't re-check availability on every keystroke.
  const request = useMemo<BookingRequestDto | null>(() => {
    if (!date || !Number.isInteger(attendees) || attendees < 1 || toMinutes(end) <= toMinutes(start)) {
      return null
    }
    return {
      spaceId: space.id,
      localStart: toLocalDateTime(date, start),
      localEnd: toLocalDateTime(date, end),
      attendees,
    }
  }, [space.id, date, start, end, attendees])

  const seriesRequest = useMemo<SeriesRequestDto | null>(
    () => (request && rule ? { ...request, recurrence: rule } : null),
    [request, rule],
  )

  // One preview or the other: a single booking's verdict, or every date of the series.
  const { state: preview, recheck } = useBookingPreview(token, rule ? null : request)
  const { state: seriesPreview, recheck: recheckSeries } = useSeriesPreview(token, seriesRequest)
  const toBook =
    seriesPreview.status === 'done' && seriesPreview.preview.seriesViolations.length === 0
      ? seriesPreview.preview.occurrences.filter((o) => o.isValid && !skipped.has(o.date))
      : []

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!request) return

    setSubmitting(true)
    setSubmitError(null)
    try {
      if (seriesRequest && seriesPreview.status === 'done') {
        // Everything not ticked is skipped — dates that failed a rule and ones unticked.
        const booked = new Set(toBook.map((o) => o.date))
        const created = await createSeries(token, {
          ...seriesRequest,
          title: title.trim() || null,
          skipDates: seriesPreview.preview.occurrences.map((o) => o.date).filter((d) => !booked.has(d)),
          idempotencyKey: seriesKey,
        })
        rememberDuration(toMinutes(end) - toMinutes(start))
        emitBookingsChanged()
        onBooked(created.bookings[0], created.bookings.length)
        return
      }

      const booking = await createBooking(token, { ...request, title: title.trim() || null, idempotencyKey })
      rememberDuration(toMinutes(end) - toMinutes(start))
      emitBookingsChanged()
      onBooked(booking)
    } catch (err) {
      if (err instanceof ApiError) {
        setSubmitError(err.message)
        // The server said no, so what the verdict panel showed is out of date: someone took
        // the slot (409), or an admin changed the room's rules since the preview. Re-check
        // either way, so the panel never says "valid" next to a rejection.
        recheck()
        recheckSeries()
      } else {
        setSubmitError('Something went wrong — please try again.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  const canBook = rule
    ? toBook.length > 0 && !submitting
    : preview.status === 'done' && preview.preview.isValid && !submitting
  const seriesTotal = seriesPreview.status === 'done' ? seriesPreview.preview.occurrences.length : 0
  const rules = space.minAttendees
    ? `${space.capacity} seats · at least ${space.minAttendees}`
    : `${space.capacity} seats`

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Book {space.name}</DialogTitle>
          <DialogDescription>
            {floorName} · {space.spaceTypeName}
          </DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={handleSubmit}>
          <p className="font-mono text-xs text-muted-foreground">
            Open {formatHours(space.hours.value)}, {formatDays(space.days.value)} · up to{' '}
            {formatDuration(space.maxDurationMinutes.value)} · {rules}
          </p>

          <div className="grid gap-2">
            <Label htmlFor="bk-title">Title</Label>
            <Input
              id="bk-title"
              placeholder="Booking"
              maxLength={128}
              value={title}
              onChange={(e) => setTitle(e.target.value)}
            />
          </div>

          <div className="grid gap-3 sm:grid-cols-[1fr_1fr]">
            <div className="grid gap-2 sm:col-span-2">
              <Label htmlFor="bk-date">Date</Label>
              <DatePicker id="bk-date" value={date} min={today} max={lastDate} onChange={setDate} />
            </div>
            <FromToFields
              idPrefix="bk"
              start={start}
              end={end}
              slotMinutes={slot}
              minStart={date === today ? now.minutes + building.minLeadMinutes : 0}
              maxLength={space.maxDurationMinutes.value}
              latestEnd={closingMinute(space)}
              onChange={(range) => {
                setStart(range.start)
                setEnd(range.end)
              }}
            />
          </div>

          <RepeatField
            date={date}
            openDays={openDays}
            lastDate={seriesLastDate}
            value={repeat}
            rule={rule}
            defaultEnd={(frequency) => defaultEndDate(date, frequency, seriesLastDate)}
            onChange={setRepeat}
          />

          <div className="grid gap-2">
            <Label htmlFor="bk-attendees">Attendees</Label>
            <div className="flex max-w-64 items-center gap-2">
              <Input
                id="bk-attendees"
                type="number"
                className="font-mono"
                min={1}
                max={space.capacity}
                value={Number.isNaN(attendees) ? '' : attendees}
                onChange={(e) => setAttendees(e.target.valueAsNumber)}
                required
              />
              <span className="whitespace-nowrap text-sm text-muted-foreground">of {space.capacity} seats</span>
            </div>
          </div>

          <p className="text-sm text-muted-foreground">
            Times are in {building.timezone} ({building.name}'s local time). You can book up to {formatDate(lastDate)}.
          </p>

          {rule ? (
            <SeriesPreviewList
              state={seriesPreview}
              skipped={skipped}
              onToggle={(d) =>
                setSkipped((prev) => {
                  const next = new Set(prev)
                  if (next.has(d)) next.delete(d)
                  else next.add(d)
                  return next
                })
              }
            />
          ) : (
            <VerdictPanel state={preview} slotLabel={`${formatDate(date)}, ${start}–${end}`} timezone={building.timezone} />
          )}

          {submitError && (
            <p className="rounded-md bg-slot-closed px-4 py-3 text-sm" role="alert">
              {submitError}
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={!canBook}>
              {submitting ? 'Booking…' : rule ? `Book ${toBook.length} of ${seriesTotal}` : 'Book'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
