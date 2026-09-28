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
import { ApiError, cancelBooking } from '@/features/bookings/api/bookingsApi'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'

// Mirrors BookingConsts.MaxCancelReasonLength on the server.
const MAX_REASON_LENGTH = 512

interface CancelBookingDialogProps {
  token: string
  booking: BookingDto
  onClose: () => void
  onCancelled: (booking: BookingDto) => void
}

/** "Are you sure?" with an optional reason. Stays open (showing the server's message) if it fails. */
export function CancelBookingDialog({ token, booking, onClose, onCancelled }: CancelBookingDialogProps) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function confirm() {
    setBusy(true)
    setError(null)
    try {
      onCancelled(await cancelBooking(token, booking.id, reason))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Couldn't cancel the booking — please try again.")
      setBusy(false)
    }
  }

  return (
    <AlertDialog open onOpenChange={(open) => !open && !busy && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Cancel “{booking.title}”?</AlertDialogTitle>
          <AlertDialogDescription>
            {booking.spaceName}, {formatDate(dateOf(booking.localStart))} {timeOf(booking.localStart)}–{timeOf(booking.localEnd)}.
            The room is released straight away, so someone else can book it.
          </AlertDialogDescription>
        </AlertDialogHeader>

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
