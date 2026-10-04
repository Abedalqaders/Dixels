import { useState } from 'react'
import { useTranslation } from 'react-i18next'
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
  { value: CancelScope.This, label: 'Booking:CancelScopeThis' },
  { value: CancelScope.ThisAndFollowing, label: 'Booking:CancelScopeFollowing' },
  { value: CancelScope.Series, label: 'Booking:CancelScopeSeries' },
] as const

/**
 * "Are you sure?" with an optional reason — and, for a recurring booking, which ones: this
 * event, this and the following ones, or the whole series (only upcoming ones are ever
 * cancelled). Stays open, showing the server's message, if it fails.
 */
export function CancelBookingDialog({ token, booking, onClose, onCancelled }: CancelBookingDialogProps) {
  const { t } = useTranslation()
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
      setError(err instanceof ApiError ? err.message : t('Booking:CancelFailed'))
      setBusy(false)
    }
  }

  return (
    <AlertDialog open onOpenChange={(open) => !open && !busy && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{isSeries ? t('Booking:CancelSeriesTitle') : t('Booking:CancelTitle', { title: booking.title })}</AlertDialogTitle>
          <AlertDialogDescription>
            {t('Booking:CancelSummary', {
              space: booking.spaceName,
              date: formatDate(dateOf(booking.localStart)),
              start: timeOf(booking.localStart),
              end: timeOf(booking.localEnd),
            })}
          </AlertDialogDescription>
        </AlertDialogHeader>

        {isSeries && (
          <fieldset className="grid gap-2 text-sm">
            <legend className="mb-2 font-medium">{t('Booking:CancelScope')}</legend>
            {SCOPES.map((s) => (
              <label key={s.value} className="flex cursor-pointer items-center gap-2">
                <input
                  type="radio"
                  name="cancel-scope"
                  className="size-4 accent-[var(--focus-ring)]"
                  checked={scope === s.value}
                  onChange={() => setScope(s.value)}
                />
                {t(s.label)}
              </label>
            ))}
            <p className="text-xs text-muted-foreground">{t('Booking:CancelOnlyUpcoming')}</p>
          </fieldset>
        )}

        <div className="grid gap-2">
          <Label htmlFor="cancel-reason">
            {t('Booking:Reason')} <span className="font-normal text-muted-foreground">{t('Common:Optional')}</span>
          </Label>
          <textarea
            id="cancel-reason"
            className="min-h-20 rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
            maxLength={MAX_REASON_LENGTH}
            placeholder={t('Booking:ReasonPlaceholder')}
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
          <AlertDialogCancel disabled={busy}>{t('Booking:Keep')}</AlertDialogCancel>
          <Button variant="destructive" disabled={busy} onClick={confirm}>
            {busy ? t('Booking:Cancelling') : t('Booking:Cancel')}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
