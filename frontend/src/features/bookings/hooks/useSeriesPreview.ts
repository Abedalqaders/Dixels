import { useCallback, useEffect, useState } from 'react'
import { ApiError, previewSeries } from '@/features/bookings/api/bookingsApi'
import type { SeriesPreviewDto, SeriesRequestDto } from '@/features/bookings/api/bookingsApi'

export type SeriesPreviewState =
  | { status: 'idle' }
  | { status: 'checking' }
  | { status: 'done'; preview: SeriesPreviewDto }
  | { status: 'error'; message: string }

type Answer = Extract<SeriesPreviewState, { status: 'done' | 'error' }>

const DEBOUNCE_MS = 400

/**
 * The recurring-booking twin of useBookingPreview: every date of the series checked by the
 * server (the same rules the real create runs) once the person pauses, and an answer only
 * counts for the exact request it was asked for — a slow reply to an earlier edit is never
 * shown for a newer one. `request` must be memoised; null means "not complete yet".
 */
export function useSeriesPreview(token: string, request: SeriesRequestDto | null) {
  const [round, setRound] = useState(0)
  const [answer, setAnswer] = useState<{ request: SeriesRequestDto; round: number; state: Answer } | null>(null)

  const recheck = useCallback(() => setRound((r) => r + 1), [])

  useEffect(() => {
    if (!request) return

    const timer = setTimeout(() => {
      previewSeries(token, request)
        .then((preview) => setAnswer({ request, round, state: { status: 'done', preview } }))
        .catch((err: unknown) =>
          setAnswer({
            request,
            round,
            state: {
              status: 'error',
              message: err instanceof ApiError ? err.message : "Couldn't check the dates — please try again.",
            },
          }),
        )
    }, DEBOUNCE_MS)

    return () => clearTimeout(timer)
  }, [token, request, round])

  let state: SeriesPreviewState
  if (!request) {
    state = { status: 'idle' }
  } else if (answer && answer.request === request && answer.round === round) {
    state = answer.state
  } else {
    state = { status: 'checking' }
  }

  return { state, recheck }
}
