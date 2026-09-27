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
} from '../../../lib/time/buildingTime'
import { ApiError, createBooking } from '../api/bookingsApi'
import type { BookableBuildingDto, BookableSpaceDto, BookingDto, BookingRequestDto } from '../api/bookingsApi'
import { formatDays, formatDuration, formatHours } from '../format'
import { useBookingPreview } from '../hooks/useBookingPreview'
import { closingMinute, rememberDuration } from '../preferences'
import { suggestSlot } from '../suggestSlot'
import type { Slot } from '../suggestSlot'
import { DatePicker } from './DatePicker'
import { FromToFields } from './FromToFields'
import { VerdictPanel } from './VerdictPanel'

interface BookingFormProps {
  token: string
  building: BookableBuildingDto
  space: BookableSpaceDto
  floorName: string
  onClose: () => void
  onBooked: (booking: BookingDto) => void
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

  const slot = building.slotMinutes
  const now = nowInZone(building.timezone)
  const today = now.date
  const lastDate = addDays(today, building.maxHorizonDays)

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

  const { state: preview, recheck } = useBookingPreview(token, request)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!request) return

    setSubmitting(true)
    setSubmitError(null)
    try {
      const booking = await createBooking(token, { ...request, title: title.trim() || null, idempotencyKey })
      rememberDuration(toMinutes(end) - toMinutes(start))
      onBooked(booking)
    } catch (err) {
      if (err instanceof ApiError) {
        setSubmitError(err.message)
        // 409: someone took the slot between the preview and the click — refresh the verdict.
        if (err.status === 409) recheck()
      } else {
        setSubmitError('Something went wrong — please try again.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  const canBook = preview.status === 'done' && preview.preview.isValid && !submitting
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

          <VerdictPanel state={preview} slotLabel={`${formatDate(date)}, ${start}–${end}`} timezone={building.timezone} />

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
              {submitting ? 'Booking…' : 'Book'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
