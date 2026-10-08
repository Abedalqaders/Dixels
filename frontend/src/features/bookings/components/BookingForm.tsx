import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { FieldError } from '@/components/FieldError'
import { PeoplePicker } from '@/components/PeoplePicker'
import { BusyStatus, BusySummary, presenceOf } from '@/components/BusyStatus'
import type { PersonBusy } from '@/components/BusyStatus'
import { randomUuid } from '@/lib/uuid'
import { groupViolations, issueText, NO_ISSUES } from '@/features/bookings/violationFields'
import {
  addDays,
  formatDate,
  fromMinutes,
  nowInZone,
  toLocalDateTime,
  toMinutes,
} from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { ApiError, createBooking, createSeries, getSpaceDays } from '@/features/bookings/api/bookingsApi'
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
import { getExternalGuestsEnabled, searchColleagues } from '@/features/bookings/api/inviteesApi'
import { MAX_INVITEES, resolvedInvitees, toInviteeDtos } from '@/features/bookings/invitees'
import { HeadCount } from './HeadCount'
import type { Invitee } from '@/features/bookings/invitees'
import { suggestSlot } from '@/features/bookings/suggestSlot'
import type { Slot } from '@/features/bookings/suggestSlot'
import type { FreeTimeRules } from '@/features/bookings/dragRange'
import { dayRules, fitsFreeTime, hasFreeTime, nearestFreeRange } from '@/features/bookings/freeTimes'
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
}

export function BookingForm({
  token,
  building,
  space,
  floorName,
  onClose,
  onBooked,
  initialSlot,
}: BookingFormProps) {
  const { t } = useTranslation()
  const [initial] = useState(() => initialSlot ?? suggestSlot(building, space))
  const [title, setTitle] = useState('')
  const [date, setDate] = useState(initial.date)
  const [start, setStart] = useState(initial.start)
  const [end, setEnd] = useState(initial.end)
  const [invitees, setInvitees] = useState<Invitee[]>([])
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  // One key per opening of the form: a retry of the same attempt (double-click, network
  // blip) can never create a second booking — the server answers it with the first one.
  const [idempotencyKey] = useState(() => randomUuid())
  const [seriesKey] = useState(() => randomUuid())

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

  // The room's days, today … the last bookable date, in one request: the date picker and
  // both time lists then offer only free times. Until they arrive the plain limits apply —
  // the preview checks everything either way.
  const guestsAllowed = useApiQuery(queryKeys.bookings.externalGuestsEnabled(), () => getExternalGuestsEnabled(token))

  const days = useApiQuery(queryKeys.bookings.spaceDays(space.id, today, lastDate), () => getSpaceDays(token, space.id, today, lastDate))
  const dayByDate = useMemo(() => new Map((days.data?.days ?? []).map((d) => [d.date, d])), [days.data])
  const minStartOn = (d: IsoDate) => (d === today ? now.minutes + building.minLeadMinutes : 0)
  const rulesOn = (d: IsoDate): FreeTimeRules | undefined => {
    if (days.status !== 'success') return undefined
    const limits = { slotMinutes: slot, minStart: minStartOn(d), maxDuration: space.maxDurationMinutes.value }
    const day = dayByDate.get(d)
    return day ? dayRules(day, limits) : { ...limits, open: [], blockers: [] }
  }
  const freeOn = (d: IsoDate) => {
    const r = rulesOn(d)
    return !r || hasFreeTime(r)
  }
  const noFreeDays = days.status === 'success' && !days.data.days.some((d) => freeOn(d.date))

  // When the days arrive or the date changes, a time that isn't free moves to the nearest
  // free one (keeping the length where it fits), and a date with no free time at all to the
  // next one that has some. Adjusted while rendering — React's way for state that follows
  // other state — so a taken time is never shown first.
  const [fittedFor, setFittedFor] = useState<{ data: unknown; date: IsoDate } | null>(null)
  if (days.status === 'success' && (fittedFor?.data !== days.data || fittedFor.date !== date)) {
    setFittedFor({ data: days.data, date })
    const length = toMinutes(end) - toMinutes(start)
    const current = rulesOn(date)
    if (current && !fitsFreeTime(toMinutes(start), toMinutes(end), current)) {
      const later = days.data.days.filter((d) => d.date > date).map((d) => d.date)
      for (const d of [date, ...later]) {
        const range = nearestFreeRange(d === date ? toMinutes(start) : 0, length, rulesOn(d)!)
        if (range) {
          setDate(d)
          setStart(fromMinutes(range.start))
          setEnd(fromMinutes(range.end))
          break
        }
      }
    }
  }


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
    if (!date || toMinutes(end) <= toMinutes(start)) {
      return null
    }
    return {
      spaceId: space.id,
      localStart: toLocalDateTime(date, start),
      localEnd: toLocalDateTime(date, end),
      invitees: toInviteeDtos(invitees),
    }
  }, [space.id, date, start, end, invitees])

  const seriesRequest = useMemo<SeriesRequestDto | null>(
    () => (request && rule ? { ...request, recurrence: rule } : null),
    [request, rule],
  )

  // One preview or the other: a single booking's verdict, or every date of the series.
  const { state: preview, recheck } = useBookingPreview(token, rule ? null : request)

  // Each broken rule is said under the field it's about — date, time or attendees. Only
  // what fits no single field is left for the panel below.
  const rejected = preview.status === 'done' && !preview.preview.isValid
  const issues = rejected ? groupViolations(preview.preview.violations) : NO_ISSUES
  const panelState = rejected ? { ...preview, preview: { ...preview.preview, violations: issues.other } } : preview
  const attendeesMessage = issueText(issues.attendees)
  const dateMessage = issueText(issues.date)
  const timeMessage = issueText(issues.time)
  const { state: seriesPreview, recheck: recheckSeries } = useSeriesPreview(token, seriesRequest)

  // The preview answers with the guests as the server resolved them: an email typed as a
  // guest that belongs to a colleague in this building comes back as that colleague. The
  // list follows it (adjusted while rendering), so the form shows it before booking.
  const resolved = rule
    ? seriesPreview.status === 'done' ? seriesPreview.preview.invitees : undefined
    : preview.status === 'done' ? preview.preview.invitees : undefined
  const followed = resolvedInvitees(invitees, resolved)
  if (followed !== invitees) setInvitees(followed)

  // Who of the colleagues is free or busy then (on a series: on how many of its dates), once
  // the preview has said — Teams-style, on each guest and in one line above them.
  const busyById = new Map<string, PersonBusy>(
    (resolved ?? []).filter((i) => i.userId).map((i) => [i.userId!, { busyDates: i.busyDates, busyTimes: i.busyTimes, maybeBusyDates: i.maybeBusyDates }]),
  )
  const dateCount = rule && seriesPreview.status === 'done' ? seriesPreview.preview.occurrences.length : 1
  const knowsBusy = (p: Invitee) => resolved !== undefined && !p.isExternal && Boolean(p.userId)

  // The head count follows the guests (you + everyone invited): see HeadCount.
  function changeInvitees(next: Invitee[]) {
    setInvitees(next)
  }
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
        // either way, so the panel never says "valid" next to a rejection — and reload the
        // room's days, so the lists stop offering what's gone.
        recheck()
        recheckSeries()
        days.refetch()
      } else {
        setSubmitError(t('Error:Generic'))
      }
    } finally {
      setSubmitting(false)
    }
  }

  const canBook = rule
    ? toBook.length > 0 && !submitting
    : preview.status === 'done' && preview.preview.isValid && !submitting
  const seriesTotal = seriesPreview.status === 'done' ? seriesPreview.preview.occurrences.length : 0
  const seats = t('Booking:Seats', { count: space.capacity })
  const limits = space.minAttendees ? `${seats} · ${t('BookingForm:AtLeast', { min: space.minAttendees })}` : seats

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{t('BookingForm:Title', { space: space.name })}</DialogTitle>
          <DialogDescription>
            {floorName} · {space.spaceTypeName}
          </DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={handleSubmit}>
          <p className="text-xs text-muted-foreground tabular-nums">
            {t('BookingForm:Rules', {
              hours: formatHours(space.hours.value),
              days: formatDays(space.days.value),
              duration: formatDuration(space.maxDurationMinutes.value),
              limits,
            })}
          </p>

          <div className="grid gap-2">
            <Label htmlFor="bk-title">{t('BookingForm:TitleLabel')}</Label>
            <Input
              id="bk-title"
              placeholder={t('BookingForm:TitlePlaceholder')}
              maxLength={128}
              value={title}
              onChange={(e) => setTitle(e.target.value)}
            />
          </div>

          <div className="grid gap-3 sm:grid-cols-[1fr_1fr]">
            <div className="grid gap-2 sm:col-span-2">
              <Label htmlFor="bk-date">{t('Booking:Date')}</Label>
              <DatePicker
                id="bk-date"
                value={date}
                min={today}
                max={lastDate}
                isDisabled={(d) => !freeOn(d)}
                onChange={setDate}
                errorId={dateMessage ? 'bk-date-error' : undefined}
              />
              {dateMessage && <FieldError id="bk-date-error" message={dateMessage} />}
              {noFreeDays && <p className="text-sm text-muted-foreground">{t('BookingForm:NoFreeDays', { date: formatDate(lastDate) })}</p>}
            </div>
            <FromToFields
              idPrefix="bk"
              start={start}
              end={end}
              slotMinutes={slot}
              minStart={minStartOn(date)}
              maxLength={space.maxDurationMinutes.value}
              latestEnd={closingMinute(space)}
              rules={rulesOn(date)}
              errorId={timeMessage ? 'bk-time-error' : undefined}
              onChange={(range) => {
                setStart(range.start)
                setEnd(range.end)
              }}
            />
            {timeMessage && (
              <div className="sm:col-span-2">
                <FieldError id="bk-time-error" message={timeMessage} />
              </div>
            )}
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

          <div className="grid gap-2" role="group" aria-labelledby="bk-people-label">
            <span id="bk-people-label" className="text-sm leading-none font-medium">
              {t('BookingForm:InvitePeople')}
            </span>
            <BusySummary
              busy={invitees.filter(knowsBusy).flatMap((p) => busyById.get(p.userId!) ?? [])}
              dates={dateCount}
              start={`${date}T${start}:00`}
            />
            <PeoplePicker
              id="bk-people"
              value={invitees}
              onChange={changeInvitees}
              searchKey={queryKeys.bookings.colleagues}
              search={(filter) => searchColleagues(token, filter)}
              allowGuests={guestsAllowed.data === true}
              max={MAX_INVITEES}
              presence={(p) => (knowsBusy(p) ? presenceOf(busyById.get(p.userId!), dateCount) : undefined)}
              rowExtra={(p) => (knowsBusy(p) ? <BusyStatus busy={busyById.get(p.userId!)} dates={dateCount} /> : null)}
            />
          </div>

          <HeadCount id="bk-headcount" guests={invitees.length} minAttendees={space.minAttendees} message={attendeesMessage} />

          <p className="text-sm text-muted-foreground">
            {t('BookingForm:TimesNote', { timezone: building.timezone, building: building.name, date: formatDate(lastDate) })}
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
            <VerdictPanel
              state={panelState}
              slotLabel={t('BookingForm:Slot', { date: formatDate(date), start, end })}
              timezone={building.timezone}
            />
          )}

          {submitError && (
            <p className="rounded-md bg-slot-closed px-4 py-3 text-sm" role="alert">
              {submitError}
            </p>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {t('Common:Cancel')}
            </Button>
            <Button type="submit" disabled={!canBook}>
              {submitting
                ? t('BookingForm:Booking')
                : rule
                  ? t('BookingForm:BookSome', { booked: toBook.length, total: seriesTotal })
                  : t('BookingForm:Book')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
