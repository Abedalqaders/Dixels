import { useTranslation } from 'react-i18next'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { dateOf, formatDate, timeOf } from '@/lib/time/buildingTime'
import { cn } from '@/lib/utils'

/** A stretch of someone's busy time, on the building's clock ("2026-10-13T10:00:00"). */
export interface BusyTime {
  localStart: string
  localEnd: string
}

/** Where a colleague stands for a booking's time: free, busy, or (a series) busy on some of its dates. */
export type Presence = 'free' | 'busy' | 'part'

/** One person's busy facts, as previews and Edit guests return them. */
export interface PersonBusy {
  busyDates: number
  busyTimes: BusyTime[]
}

/** Free, busy, or busy on part of a series — from how many of `dates` they're busy on. */
export function presenceOf(busy: PersonBusy | undefined, dates: number): Presence {
  if (!busy || busy.busyDates === 0) return 'free'
  return dates > 1 && busy.busyDates < dates ? 'part' : 'busy'
}

const range = (t: BusyTime) => `${timeOf(t.localStart)}–${timeOf(t.localEnd)}`

/** Busy times grouped by date, earliest first: [["2026-10-13", ["10:00–10:30"]], …]. */
function byDate(times: BusyTime[]): [string, string[]][] {
  const out = new Map<string, string[]>()
  for (const t of [...times].sort((a, b) => a.localStart.localeCompare(b.localStart))) {
    const day = dateOf(t.localStart)
    out.set(day, [...(out.get(day) ?? []), range(t)])
  }
  return [...out]
}

/**
 * A guest's status pill, Teams-style: "● Free" in green, or a red "● Busy 10:00–10:30"
 * ("● Busy on 2 of 8 dates" on a series) that opens the busy times. Only times, never with
 * what. A heads-up: nothing here stops the booking.
 */
export function BusyStatus({ busy, dates }: { busy: PersonBusy | undefined; dates: number }) {
  const { t } = useTranslation()
  const presence = presenceOf(busy, dates)
  const pill = 'inline-flex flex-none items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium'

  if (presence === 'free' || !busy) {
    return (
      <span className={cn(pill, 'bg-[var(--state-confirmed-soft)] text-[var(--state-confirmed-ink)]')}>
        <span className="size-1.5 rounded-full bg-[var(--presence-free)]" aria-hidden="true" />
        {t('People:Free')}
      </span>
    )
  }

  const series = dates > 1
  const groups = byDate(busy.busyTimes)
  return (
    <Popover>
      <PopoverTrigger
        className={cn(pill, 'cursor-pointer bg-[var(--state-cancelled-soft)] text-[var(--state-cancelled-ink)] outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50')}
      >
        <span className="size-1.5 rounded-full bg-[var(--presence-busy)]" aria-hidden="true" />
        {series
          ? t('People:BusyOnDates', { count: busy.busyDates, total: dates })
          : t('People:BusyAt', { times: busy.busyTimes.map(range).join(', ') })}
      </PopoverTrigger>
      <PopoverContent align="end" className="w-64 p-3 text-sm">
        <p className="mb-1 text-xs font-semibold tracking-wide text-muted-foreground uppercase">
          {series ? t('People:BusyOnTheseDates') : t('People:BusyTimes')}
        </p>
        <ul className="m-0 grid list-none gap-0.5 p-0">
          {groups.map(([day, ranges]) => (
            <li key={day}>
              {formatDate(day)} · <span className="font-mono tabular-nums">{ranges.join(', ')}</span>
            </li>
          ))}
        </ul>
        <p className="mt-2 text-xs text-muted-foreground">{t('People:BusyPrivacy')}</p>
      </PopoverContent>
    </Popover>
  )
}

/**
 * One line above the guests: "2 people are busy at 10:00", or "Someone is busy on 4 of 8
 * dates" on a series. Nothing when everyone is free.
 */
export function BusySummary({ busy, dates, start }: { busy: PersonBusy[]; dates: number; start: string }) {
  const { t } = useTranslation()
  const busyPeople = busy.filter((b) => b.busyDates > 0)
  if (busyPeople.length === 0) return null

  const text =
    dates > 1
      ? t('People:BusySummarySeries', { count: new Set(busyPeople.flatMap((b) => b.busyTimes.map((x) => dateOf(x.localStart)))).size, total: dates })
      : t('People:BusySummary', { count: busyPeople.length, time: timeOf(start) })

  return (
    <p
      role="status"
      className="m-0 flex items-center gap-2 rounded-md bg-[var(--state-cancelled-soft)] px-3 py-2 text-sm font-medium text-[var(--state-cancelled-ink)]"
    >
      <span
        className="grid size-5 flex-none place-items-center rounded-full bg-[var(--presence-busy)] text-xs font-bold text-background"
        aria-hidden="true"
      >
        !
      </span>
      {text}
    </p>
  )
}
