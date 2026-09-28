import { useCallback, useEffect, useState } from 'react'
import { ApiError, previewBooking } from '@/features/bookings/api/bookingsApi'
import type { BookingPreviewDto, BookingRequestDto } from '@/features/bookings/api/bookingsApi'

export type PreviewState =
  | { status: 'idle' }
  | { status: 'checking' }
  | { status: 'done'; preview: BookingPreviewDto }
  | { status: 'error'; message: string }

type Answer = Extract<PreviewState, { status: 'done' | 'error' }>

const DEBOUNCE_MS = 400

/**
 * Live server-side validation for the booking form: waits until the user pauses
 * (400 ms) and then asks /bookings/preview — the same rules the real create runs — so the
 * form can never promise a slot the server would reject.
 *
 * Each answer is stored together with the exact request (and recheck round) it answers,
 * and only counts while that's still the current one. So "checking" is simply "no answer
 * for the current request yet", and a slow response to an earlier edit can never be shown
 * as the verdict for a newer one.
 *
 * `request` must be referentially stable between edits (memoise it); null means "the
 * form isn't complete enough to check yet".
 */
export function useBookingPreview(token: string, request: BookingRequestDto | null) {
  const [round, setRound] = useState(0)
  const [answer, setAnswer] = useState<{ request: BookingRequestDto; round: number; state: Answer } | null>(null)

  /** Re-runs the check now (e.g. after a create lost a race and the slot is gone). */
  const recheck = useCallback(() => setRound((r) => r + 1), [])

  useEffect(() => {
    if (!request) return

    const timer = setTimeout(() => {
      previewBooking(token, request)
        .then((preview) => setAnswer({ request, round, state: { status: 'done', preview } }))
        .catch((err: unknown) =>
          setAnswer({
            request,
            round,
            state: {
              status: 'error',
              message: err instanceof ApiError ? err.message : "Couldn't check availability — please try again.",
            },
          }),
        )
    }, DEBOUNCE_MS)

    return () => clearTimeout(timer)
  }, [token, request, round])

  let state: PreviewState
  if (!request) {
    state = { status: 'idle' }
  } else if (answer && answer.request === request && answer.round === round) {
    state = answer.state
  } else {
    state = { status: 'checking' }
  }

  return { state, recheck }
}
