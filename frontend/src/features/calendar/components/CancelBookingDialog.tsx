import { useState } from 'react'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { dateOf, formatDate, timeOf } from '@/lib/time/buildingTime'
import { ApiError, CancelScope, cancelBooking } from '@/features/bookings/api/bookingsApi'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'

// Mirrors BookingConsts.MaxCancelReasonLength on the server.
const MAX_REASON_LENGTH = 512

interface CancelBookingDialogProps {
  token: string
  booking: BookingDto
  onClose: () => void
  /** Every booking cancelled — more than one for a series. */
  onCancelled: (cancelled: BookingDto[]) => void
}

// Teams' wording for which bookings of a series to cancel.
const SCOPES = [
  { value: CancelScope.This, label: 'This event' },
  { value: CancelScope.ThisAndFollowing, label: 'This and all following events' },
  { value: CancelScope.Series, label: 'All events in the series' },
]

/**
 * "Are you sure?" with an optional reason — and, for a recurring booking, which ones: this
 * event, this and the following ones, or the whole series (only upcoming ones are ever
 * cancelled). Stays open, showing the server's message, if it fails.
 */
export function CancelBookingDialog({ token, booking, onClose, onCancelled }: CancelBookingDialogProps) {
  const [reason, setReason] = useState('')
  const [scope, setScope] = useState<CancelScope>(CancelScope.This)
  const isSeries = Boolean(booking.seriesId)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function confirm() {
    setBusy(true)
    setError(null)
    try {
      onCancelled(await cancelBooking(token, booking.id, reason, isSeries ? scope : CancelScope.This))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Couldn't cancel the booking — please try again.")
      setBusy(false)
    }
  }

  return (
    <AlertDialog open onOpenChange={(open) => !open && !busy && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{isSeries ? 'Cancel recurring booking?' : `Cancel “${booking.title}”?`}</AlertDialogTitle>
          <AlertDialogDescription>
            {booking.spaceName}, {formatDate(dateOf(booking.localStart))} {timeOf(booking.localStart)}–{timeOf(booking.localEnd)}.
            The room is released straight away, so someone else can book it.
          </AlertDialogDescription>
        </AlertDialogHeader>

        {isSeries && (
          <fieldset className="grid gap-2 text-sm">
            <legend className="mb-2 font-medium">Cancel</legend>
            {SCOPES.map((s) => (
              <label key={s.value} className="flex cursor-pointer items-center gap-2">
                <input
                  type="radio"
                  name="cancel-scope"
                  className="size-4 accent-[var(--focus-ring)]"
                  checked={scope === s.value}
                  onChange={() => setScope(s.value)}
                />
                {s.label}
              </label>
            ))}
            <p className="text-xs text-muted-foreground">Only upcoming ones are cancelled — past ones stay in your history.</p>
          </fieldset>
        )}

        <div className="grid gap-2">
          <Label htmlFor="cancel-reason">
            Reason <span className="font-normal text-muted-foreground">(optional)</span>
          </Label>
          <textarea
            id="cancel-reason"
            className="min-h-20 rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
            maxLength={MAX_REASON_LENGTH}
            placeholder="e.g. Meeting moved online"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
          />
        </div>

        {error && (
          <p role="alert" className="rounded-md bg-slot-closed px-3 py-2 text-sm text-slot-closed-ink">
            {error}
          </p>
        )}

        <AlertDialogFooter>
          <AlertDialogCancel disabled={busy}>Keep booking</AlertDialogCancel>
          <Button variant="destructive" disabled={busy} onClick={confirm}>
            {busy ? 'Cancelling…' : 'Cancel booking'}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
