import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Check, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { FieldError } from '@/components/FieldError'
import { cn } from '@/lib/utils'
import { formatDate } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import { ApiError, InviteeResponseStatus } from '@/features/bookings/api/bookingsApi'
import { BusyNote } from '@/components/BusyStatus'
import type { BusyTime } from '@/components/BusyStatus'
import { AnswerMark } from './InviteeList'

/** Which dates an answer covers: this one, or (a series) every upcoming one. */
export type AnswerScope = 'date' | 'series'

export interface InviteResponseProps {
  /** My answer now (Pending until I give one). */
  answer: InviteeResponseStatus
  /** Answers are open until the meeting starts and while it isn't cancelled. */
  open: boolean
  /** The booking's date, for "This date only (…)" on a series. */
  date: IsoDate
  isSeries: boolean
  /** When I'm already taken at this time (my own bookings, other meetings I accepted): a heads-up next to Accept. */
  busyTimes?: BusyTime[]
  onRespond: (status: InviteeResponseStatus, scope: AnswerScope) => Promise<void>
}

const ACCEPTED_ON = 'border-[var(--state-confirmed-ink)]/40 bg-[var(--state-confirmed-soft)] text-[var(--state-confirmed-ink)] hover:bg-[var(--state-confirmed-soft)]'
const DECLINED_ON = 'border-[var(--state-cancelled-ink)]/40 bg-[var(--state-cancelled-soft)] text-[var(--state-cancelled-ink)] hover:bg-[var(--state-cancelled-soft)]'

/**
 * A colleague guest's answer to an invitation: Accept / Decline, the chosen one soft green or
 * soft red. On a series a click first asks "this date only, or all upcoming dates?" (all by
 * default). Once the meeting has started, or was cancelled, the answer only shows.
 */
export function InviteResponse({ answer, open, date, isSeries, busyTimes = [], onRespond }: InviteResponseProps) {
  const { t } = useTranslation()
  const [asking, setAsking] = useState<InviteeResponseStatus | null>(null)
  const [scope, setScope] = useState<AnswerScope>('series')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function send(status: InviteeResponseStatus, to: AnswerScope) {
    setBusy(true)
    setError(null)
    try {
      await onRespond(status, to)
      setAsking(null)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t('Booking:AnswerFailed'))
    } finally {
      setBusy(false)
    }
  }

  function choose(status: InviteeResponseStatus) {
    if (isSeries) {
      setScope('series')
      setAsking(status)
    } else {
      void send(status, 'date')
    }
  }

  const current = answer === InviteeResponseStatus.Accepted
    ? t('Booking:AnswerAccepted')
    : answer === InviteeResponseStatus.Declined
      ? t('Booking:AnswerDeclined')
      : t('Booking:AnswerNone')

  return (
    <section className="grid gap-2" aria-labelledby="invite-answer-label">
      <span id="invite-answer-label" className="text-sm font-medium">
        {t('Booking:YourAnswer')}
      </span>

      {open && <BusyNote times={busyTimes} />}

      {open ? (
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={busy}
            aria-pressed={answer === InviteeResponseStatus.Accepted}
            className={cn(answer === InviteeResponseStatus.Accepted && ACCEPTED_ON)}
            onClick={() => choose(InviteeResponseStatus.Accepted)}
          >
            <Check /> {answer === InviteeResponseStatus.Accepted ? t('Booking:AnswerAccepted') : t('Booking:Accept')}
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={busy}
            aria-pressed={answer === InviteeResponseStatus.Declined}
            className={cn(answer === InviteeResponseStatus.Declined && DECLINED_ON)}
            onClick={() => choose(InviteeResponseStatus.Declined)}
          >
            <X /> {answer === InviteeResponseStatus.Declined ? t('Booking:AnswerDeclined') : t('Booking:Decline')}
          </Button>
        </div>
      ) : (
        <>
          <p className="m-0 flex items-center gap-2 text-sm">
            <AnswerMark status={answer} />
            {current}
          </p>
          <p className="m-0 rounded-md bg-muted px-3 py-2 text-sm text-muted-foreground">{t('Booking:AnswersClosed')}</p>
        </>
      )}

      {open && asking !== null && (
        <fieldset className="m-0 grid gap-2 rounded-lg border border-border bg-muted/40 p-3">
          <legend className="px-1 text-sm font-medium">{t('Booking:AnswerFor')}</legend>
          {(['date', 'series'] as const).map((value) => (
            <label key={value} className="flex items-center gap-2 text-sm">
              <input type="radio" name="answer-scope" value={value} checked={scope === value} onChange={() => setScope(value)} />
              {value === 'date' ? t('Booking:AnswerThisDate', { date: formatDate(date) }) : t('Booking:AnswerAllUpcoming')}
            </label>
          ))}
          {scope === 'series' && <p className="m-0 text-xs text-muted-foreground">{t('Booking:AnswerSeriesNote')}</p>}
          <div className="flex flex-wrap justify-end gap-2">
            <Button type="button" variant="ghost" size="sm" disabled={busy} onClick={() => setAsking(null)}>
              {t('Common:Cancel')}
            </Button>
            <Button
              type="button"
              size="sm"
              disabled={busy}
              variant={asking === InviteeResponseStatus.Declined ? 'destructive' : 'default'}
              onClick={() => void send(asking, scope)}
            >
              {asking === InviteeResponseStatus.Declined ? t('Booking:Decline') : t('Booking:Accept')}
            </Button>
          </div>
        </fieldset>
      )}

      {error && <FieldError id="invite-answer-error" message={error} />}
    </section>
  )
}
