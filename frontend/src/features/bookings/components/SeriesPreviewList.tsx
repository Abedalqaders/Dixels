import { CircleAlert, CircleCheck, LoaderCircle, TriangleAlert } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { formatDate, timeOf } from '@/lib/time/buildingTime'
import type { IsoDate } from '@/lib/time/buildingTime'
import { formatClockRange } from '@/lib/time/format'
import type { SeriesPreviewState } from '@/features/bookings/hooks/useSeriesPreview'

interface SeriesPreviewListProps {
  state: SeriesPreviewState
  /** Dates the person unticked themselves. */
  skipped: Set<IsoDate>
  onToggle: (date: IsoDate) => void
}

const BOX = 'flex gap-2.5 rounded-md px-4 py-3 text-sm [&>svg]:mt-0.5 [&>svg]:size-4 [&>svg]:flex-none'

/**
 * Every date of the series with the server's verdict: free (ticked, can be unticked) or
 * why not (unticked, can't be ticked). A problem every date shares — too many people,
 * longer than the room allows — is shown once above the list instead.
 */
export function SeriesPreviewList({ state, skipped, onToggle }: SeriesPreviewListProps) {
  const { t } = useTranslation()
  if (state.status === 'idle') return null

  if (state.status === 'checking') {
    return (
      <div className={`${BOX} bg-muted text-muted-foreground`} role="status" aria-live="polite">
        <LoaderCircle className="animate-spin" />
        {t('BookingForm:CheckingDates')}
      </div>
    )
  }

  if (state.status === 'error') {
    return (
      <div className={`${BOX} bg-slot-closed`} role="alert">
        <CircleAlert />
        {state.message}
      </div>
    )
  }

  const { preview } = state
  if (preview.seriesViolations.length > 0) {
    return (
      <div className={`${BOX} bg-slot-closed`} role="alert">
        <CircleAlert />
        <div>
          <strong>{preview.seriesViolations[0].message}</strong>
          <p className="mt-1">{t('BookingForm:AppliesToEveryDate')}</p>
        </div>
      </div>
    )
  }

  const total = preview.occurrences.length
  const free = preview.occurrences.filter((o) => o.isValid).length

  return (
    <section className="grid gap-2" aria-label={t('BookingForm:Dates')}>
      <p className="text-sm font-medium" aria-live="polite">
        {free === total ? t('BookingForm:AllDatesFree', { total }) : t('BookingForm:SomeDatesFree', { free, total })}
        <span className="font-normal text-muted-foreground"> · {t('BookingForm:UntickHint')}</span>
      </p>
      <ul className="grid max-h-64 gap-1 overflow-y-auto rounded-md border p-1">
        {preview.occurrences.map((o) => {
          const ticked = o.isValid && !skipped.has(o.date)
          return (
            <li key={o.date}>
              <label
                className={cn(
                  'flex items-start gap-3 rounded px-2 py-1.5 text-sm',
                  o.isValid ? 'cursor-pointer hover:bg-muted' : 'cursor-not-allowed opacity-80',
                )}
              >
                <input
                  type="checkbox"
                  className="mt-0.5 size-4 accent-[var(--focus-ring)]"
                  checked={ticked}
                  disabled={!o.isValid}
                  onChange={() => onToggle(o.date)}
                  aria-label={`${formatDate(o.date)} ${o.isValid ? t('BookingForm:DateFree') : (o.violations[0]?.shortMessage ?? t('BookingForm:DateNotAvailable'))}`}
                />
                {/* Arabic weekday names are full words ("الثلاثاء"), so the column is wider there. */}
                <span className="w-28 flex-none font-medium rtl:w-36">{formatDate(o.date)}</span>
                <span className="font-mono text-xs leading-5 text-muted-foreground">
                  {formatClockRange(timeOf(o.localStart), timeOf(o.localEnd))}
                </span>
                <span className="ms-auto flex flex-col items-end gap-0.5 text-end text-xs leading-5">
                  {o.isValid ? (
                    <span className="inline-flex items-center gap-1 text-brand">
                      <CircleCheck className="size-3.5" /> {t('BookingForm:Free')}
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1 text-slot-closed-ink">
                      <CircleAlert className="size-3.5" /> {o.violations[0]?.shortMessage}
                    </span>
                  )}
                  {o.warnings.map((w) => (
                    <span key={w.code} className="inline-flex items-center gap-1 text-[var(--state-expired-ink)]">
                      <TriangleAlert className="size-3.5" /> {w.shortMessage}
                    </span>
                  ))}
                </span>
              </label>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
