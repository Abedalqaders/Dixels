import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Dialog } from '../../../components/Dialog'
import {
  addDays,
  formatDate,
  fromMinutes,
  nowInZone,
  slotTimes,
  toLocalDateTime,
  toMinutes,
} from '../../../lib/time/buildingTime'
import type { HhMm } from '../../../lib/time/buildingTime'
import { ApiError, createBooking } from '../api/bookingsApi'
import type { BookableBuildingDto, BookableSpaceDto, BookingDto, BookingRequestDto } from '../api/bookingsApi'
import { formatDays, formatDuration, formatHours } from '../format'
import { useBookingPreview } from '../hooks/useBookingPreview'
import { suggestSlot } from '../suggestSlot'
import type { Slot } from '../suggestSlot'
import { VerdictPanel } from './VerdictPanel'

const DAY_MINUTES = 24 * 60

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
  const today = nowInZone(building.timezone).date
  const lastDate = addDays(today, building.maxHorizonDays)
  const startOptions = slotTimes(slot, 0, DAY_MINUTES - slot)
  const endOptions = slotTimes(slot, toMinutes(start) + slot, DAY_MINUTES)

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

  function changeStart(next: HhMm) {
    // Keep the booking's length when the start moves, so "move it an hour later" is one change.
    const length = toMinutes(end) - toMinutes(start)
    setStart(next)
    setEnd(fromMinutes(Math.min(toMinutes(next) + Math.max(length, slot), DAY_MINUTES)))
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!request) return

    setSubmitting(true)
    setSubmitError(null)
    try {
      onBooked(await createBooking(token, { ...request, title: title.trim() || null, idempotencyKey }))
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
    <Dialog title={`Book ${space.name}`} subtitle={`${floorName} · ${space.spaceTypeName}`} onClose={onClose} wide>
      <form className="bookingform" onSubmit={handleSubmit}>
        <p className="rules">
          Open {formatHours(space.hours.value)}, {formatDays(space.days.value)} · up to{' '}
          {formatDuration(space.maxDurationMinutes.value)} · {rules}
        </p>

        <div className="field">
          <label className="lbl" htmlFor="bk-title">
            Title
          </label>
          <input
            id="bk-title"
            className="ctrl"
            placeholder="Booking"
            maxLength={128}
            value={title}
            onChange={(e) => setTitle(e.target.value)}
          />
        </div>

        <div className="row3">
          <div className="field">
            <label className="lbl" htmlFor="bk-date">
              Date
            </label>
            <input
              id="bk-date"
              className="ctrl mono"
              type="date"
              min={today}
              max={lastDate}
              value={date}
              onChange={(e) => setDate(e.target.value)}
              required
            />
          </div>
          <div className="field">
            <label className="lbl" htmlFor="bk-start">
              Start
            </label>
            <select id="bk-start" className="ctrl mono" value={start} onChange={(e) => changeStart(e.target.value)}>
              {startOptions.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label className="lbl" htmlFor="bk-end">
              End
            </label>
            <select id="bk-end" className="ctrl mono" value={end} onChange={(e) => setEnd(e.target.value)}>
              {endOptions.map((t) => (
                <option key={t} value={t}>
                  {t === '24:00' ? '24:00 (midnight)' : t}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="field">
          <label className="lbl" htmlFor="bk-attendees">
            Attendees
          </label>
          <div className="pair narrow">
            <input
              id="bk-attendees"
              className="ctrl mono"
              type="number"
              min={1}
              max={space.capacity}
              value={Number.isNaN(attendees) ? '' : attendees}
              onChange={(e) => setAttendees(e.target.valueAsNumber)}
              required
            />
            <span className="unit">of {space.capacity} seats</span>
          </div>
        </div>

        <p className="tzhint">
          Times are in {building.timezone} ({building.name}'s local time). You can book up to {formatDate(lastDate)}.
        </p>

        <VerdictPanel
          state={preview}
          slotLabel={`${formatDate(date)}, ${start}–${end}`}
          timezone={building.timezone}
        />

        {submitError && (
          <div className="verdict bad" role="alert">
            {submitError}
          </div>
        )}

        <div className="modalfoot">
          <button type="button" className="btn sec" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="btn" disabled={!canBook}>
            {submitting ? 'Booking…' : 'Book'}
          </button>
        </div>
      </form>
    </Dialog>
  )
}
