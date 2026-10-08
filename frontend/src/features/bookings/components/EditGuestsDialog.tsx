import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Info } from 'lucide-react'
import { PeoplePicker } from '@/components/PeoplePicker'
import { BusyStatus, BusySummary, presenceOf } from '@/components/BusyStatus'
import type { PersonBusy } from '@/components/BusyStatus'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { useApiQuery } from '@/hooks/useApiQuery'
import { dateOf, formatDate, timeOf } from '@/lib/time/buildingTime'
import { ApiError, getBusyGuests, getSeriesBusyGuests, updateInvitees, updateSeriesInvitees } from '@/features/bookings/api/bookingsApi'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'
import { getExternalGuestsEnabled, searchColleagues } from '@/features/bookings/api/inviteesApi'
import { fromInviteeDtos, headCount, MAX_INVITEES, toInviteeDtos } from '@/features/bookings/invitees'
import type { Invitee } from '@/features/bookings/invitees'
import { queryKeys } from '@/lib/api/queryKeys'
import { fieldFor } from '@/features/bookings/violationFields'
import { HeadCount } from './HeadCount'

interface EditGuestsDialogProps {
  token: string
  booking: BookingDto
  onClose: () => void
  onSaved: () => void
}

/**
 * The owner changing who's invited; the head count follows (you + the guests). The same picker
 * as the booking form; on a series it changes every upcoming date. The count is checked here the
 * way the server checks it — grandfathered: a room that has since shrunk only refuses a higher
 * count, a raised minimum only a lower one — so Save is off while a problem shows under it.
 */
export function EditGuestsDialog({ token, booking, onClose, onSaved }: EditGuestsDialogProps) {
  const { t } = useTranslation()
  const [invitees, setInvitees] = useState<Invitee[]>(() => fromInviteeDtos(booking.invitees))
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
    (busyGuests.data?.items ?? []).map((b) => [b.userId, { busyDates: b.busyDates, busyTimes: b.times }]),
  )
  const dateCount = busyGuests.data?.dates ?? 1
  const knowsBusy = (p: Invitee) => busyGuests.data !== undefined && !p.isExternal && Boolean(p.userId)

  // Grandfathered like the server: the room's size only matters if this raises the count, its
  // minimum only if this lowers it (a booking kept under changed rules can still be tidied).
  const people = headCount(invitees.length)
  const lowering = people < booking.attendees
  const overCapacity = people > booking.attendees && booking.capacity != null && people > booking.capacity
  const belowMinimum = lowering && booking.minAttendees != null && people < booking.minAttendees
  const attendeesError = overCapacity ? t('BookingForm:RoomSeats', { count: booking.capacity! }) : null
  const attendeesMessage = attendeesError ?? (serverError?.attendees ? serverError.message : null)

  function changeInvitees(next: Invitee[]) {
    setInvitees(next)
    setServerError(null)
  }

  async function save() {
    setBusy(true)
    setServerError(null)
    try {
      const input = { invitees: toInviteeDtos(invitees) }
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

        <HeadCount
          id="eg-people-count"
          guests={invitees.length}
          minAttendees={lowering ? booking.minAttendees : null}
          message={attendeesMessage}
        />

        {serverError && !serverError.attendees && (
          <p role="alert" className="rounded-md bg-slot-closed px-3 py-2 text-sm text-slot-closed-ink">
            {serverError.message}
          </p>
        )}

        <DialogFooter>
          <Button type="button" variant="outline" disabled={busy} onClick={onClose}>
            {t('Common:Cancel')}
          </Button>
          <Button type="button" disabled={busy || overCapacity || belowMinimum} onClick={save}>
            {busy ? t('Common:Saving') : t('Common:Save')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
