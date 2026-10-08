import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Info } from 'lucide-react'
import { FieldError } from '@/components/FieldError'
import { PeoplePicker } from '@/components/PeoplePicker'
import { BusyStatus, BusySummary, presenceOf } from '@/components/BusyStatus'
import type { PersonBusy } from '@/components/BusyStatus'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useApiQuery } from '@/hooks/useApiQuery'
import { dateOf, formatDate, timeOf } from '@/lib/time/buildingTime'
import { ApiError, getBusyGuests, getSeriesBusyGuests, updateInvitees, updateSeriesInvitees } from '@/features/bookings/api/bookingsApi'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'
import { getExternalGuestsEnabled, searchColleagues } from '@/features/bookings/api/inviteesApi'
import { fromInviteeDtos, headCountFor, MAX_INVITEES, toInviteeDtos } from '@/features/bookings/invitees'
import type { Invitee } from '@/features/bookings/invitees'
import { queryKeys } from '@/lib/api/queryKeys'
import { fieldFor } from '@/features/bookings/violationFields'

interface EditGuestsDialogProps {
  token: string
  booking: BookingDto
  onClose: () => void
  onSaved: () => void
}

/**
 * The owner changing who's invited, and the head count with it. The same picker as the
 * booking form; on a series it changes every upcoming date. The head count is checked
 * here the way the server checks it — grandfathered: a room that has since shrunk only
 * refuses a higher number, a raised minimum only a lower one — so Save is off while a
 * problem shows under Attendees.
 */
export function EditGuestsDialog({ token, booking, onClose, onSaved }: EditGuestsDialogProps) {
  const { t } = useTranslation()
  const [invitees, setInvitees] = useState<Invitee[]>(() => fromInviteeDtos(booking.invitees))
  const [attendees, setAttendees] = useState(booking.attendees)
  const [busy, setBusy] = useState(false)
  const [serverError, setServerError] = useState<{ attendees: boolean; message: string } | null>(null)
  const guestsAllowed = useApiQuery(queryKeys.bookings.externalGuestsEnabled(), () => getExternalGuestsEnabled(token))
  const seriesId = booking.seriesId

  // "Busy then" for the colleagues listed: at this booking's time, or across the series'
  // upcoming dates (this booking itself doesn't count). Asked again as the list changes.
  const colleagueIds = invitees.filter((p) => !p.isExternal && p.userId).map((p) => p.userId!).sort()
  const busyGuests = useApiQuery(queryKeys.bookings.busyGuests(seriesId ?? booking.id, colleagueIds), () =>
    colleagueIds.length === 0
      ? Promise.resolve({ dates: 1, items: [] })
      : seriesId
        ? getSeriesBusyGuests(token, seriesId, colleagueIds)
        : getBusyGuests(token, booking.id, colleagueIds),
  )
  const busyById = new Map<string, PersonBusy>(
    (busyGuests.data?.items ?? []).map((b) => [b.userId, { busyDates: b.busyDates, busyTimes: b.times, maybeBusyDates: b.maybeBusyDates }]),
  )
  const dateCount = busyGuests.data?.dates ?? 1
  const knowsBusy = (p: Invitee) => busyGuests.data !== undefined && !p.isExternal && Boolean(p.userId)

  const needed = 1 + invitees.length
  const attendeesError = !Number.isInteger(attendees) || attendees < 1
    ? t('BookingForm:AttendeesRequired')
    : attendees < needed
      ? t('Dixels:Bookings:AttendeesBelowInvitees:Short', { needed })
      : attendees > booking.attendees && booking.capacity != null && attendees > booking.capacity
        ? t('BookingForm:RoomSeats', { count: booking.capacity })
        : attendees < booking.attendees && booking.minAttendees != null && attendees < booking.minAttendees
          ? t('Dixels:Bookings:BelowMinAttendees:Short', { minAttendees: booking.minAttendees })
          : null
  const attendeesMessage = attendeesError ?? (serverError?.attendees ? serverError.message : null)

  // Adding people raises the head count to fit them; removing someone leaves it, as in the form.
  function changeInvitees(next: Invitee[]) {
    setInvitees(next)
    setAttendees((current) => headCountFor(current, next.length))
    setServerError(null)
  }

  async function save() {
    setBusy(true)
    setServerError(null)
    try {
      const input = { attendees, invitees: toInviteeDtos(invitees) }
      if (seriesId) await updateSeriesInvitees(token, seriesId, input)
      else await updateInvitees(token, booking.id, input)
      onSaved()
    } catch (err) {
      setServerError(
        err instanceof ApiError
          ? { attendees: fieldFor(err.code ?? '') === 'attendees', message: err.message }
          : { attendees: false, message: t('Booking:EditGuestsFailed') },
      )
      setBusy(false)
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !busy && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('Booking:EditGuestsTitle')}</DialogTitle>
          <DialogDescription>
            {t('Booking:EditGuestsSummary', {
              space: booking.spaceName,
              date: formatDate(dateOf(booking.localStart)),
              start: timeOf(booking.localStart),
              end: timeOf(booking.localEnd),
            })}
          </DialogDescription>
        </DialogHeader>

        {seriesId && (
          <p className="flex items-start gap-2 rounded-md bg-slot-open px-3 py-2 text-sm text-brand">
            <Info className="mt-0.5 size-4 flex-none" aria-hidden="true" />
            {t('Booking:EditGuestsSeriesNote')}
          </p>
        )}

        <div className="grid gap-2" role="group" aria-labelledby="eg-people-label">
          <span id="eg-people-label" className="text-sm leading-none font-medium">
            {t('BookingForm:InvitePeople')}
          </span>
          <BusySummary
            busy={invitees.filter(knowsBusy).flatMap((p) => busyById.get(p.userId!) ?? [])}
            dates={dateCount}
            start={booking.localStart}
          />
          <PeoplePicker
            id="eg-people"
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

        <div className="grid gap-2">
          <Label htmlFor="eg-attendees">{t('BookingForm:Attendees')}</Label>
          <div className="flex max-w-64 items-center gap-2">
            <Input
              id="eg-attendees"
              type="number"
              className="font-mono"
              min={needed}
              value={Number.isNaN(attendees) ? '' : attendees}
              onChange={(e) => {
                setAttendees(e.target.valueAsNumber)
                setServerError(null)
              }}
              aria-invalid={attendeesMessage ? true : undefined}
              aria-describedby={attendeesMessage ? 'eg-attendees-error' : undefined}
              required
            />
            {booking.capacity != null && (
              <span className="whitespace-nowrap text-sm text-muted-foreground">{t('BookingForm:OfSeats', { count: booking.capacity })}</span>
            )}
          </div>
          {attendeesMessage && <FieldError id="eg-attendees-error" message={attendeesMessage} />}
        </div>

        {serverError && !serverError.attendees && (
          <p role="alert" className="rounded-md bg-slot-closed px-3 py-2 text-sm text-slot-closed-ink">
            {serverError.message}
          </p>
        )}

        <DialogFooter>
          <Button type="button" variant="outline" disabled={busy} onClick={onClose}>
            {t('Common:Cancel')}
          </Button>
          <Button type="button" disabled={busy || attendeesError !== null} onClick={save}>
            {busy ? t('Common:Saving') : t('Common:Save')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
