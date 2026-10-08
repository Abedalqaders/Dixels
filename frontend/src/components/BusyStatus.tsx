import { useTranslation } from 'react-i18next'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { dateOf, formatDate, timeOf } from '@/lib/time/buildingTime'
import { cn } from '@/lib/utils'

/**
 * A stretch of someone's busy time, on the building's clock ("2026-10-13T10:00:00").
 * Tentative: only maybe busy then (a meeting they said Maybe to).
 */
export interface BusyTime {
  localStart: string
  localEnd: string
  isTentative?: boolean
}

/**
 * Where a colleague stands for a booking's time: free, busy, (a series) busy on some of its
 * dates, or only maybe busy (they said Maybe to another meeting then). Busy wins over maybe.
 */
export type Presence = 'free' | 'busy' | 'part' | 'maybe'

/** One person's busy facts, as previews and Edit guests return them. */
export interface PersonBusy {
  busyDates: number
  busyTimes: BusyTime[]
  /** On how many more dates only maybe busy. */
  maybeBusyDates?: number
}

/** Free, busy, busy on part of a series, or only maybe busy — from how many of `dates` they're busy on. */
export function presenceOf(busy: PersonBusy | undefined, dates: number): Presence {
  if (!busy) return 'free'
  if (busy.busyDates > 0) return dates > 1 && busy.busyDates < dates ? 'part' : 'busy'
  return (busy.maybeBusyDates ?? 0) > 0 ? 'maybe' : 'free'
}

const firm = (times: BusyTime[]) => times.filter((x) => !x.isTentative)
const tentative = (times: BusyTime[]) => times.filter((x) => x.isTentative)

// Isolated left-to-right (LRI … PDI): in Arabic, "10:00–10:30" would otherwise read as
// 10:30–10:00. Word joiners round the dash keep a range from breaking across two lines.
const range = (t: BusyTime) => `⁦${timeOf(t.localStart)}⁠–⁠${timeOf(t.localEnd)}⁩`

/** Busy times grouped by date, earliest first: [["2026-10-13", [{ text: "10:00–10:30", maybe: false }]], …]. */
function byDate(times: BusyTime[]): [string, { text: string; maybe: boolean }[]][] {
  const out = new Map<string, { text: string; maybe: boolean }[]>()
  for (const t of [...times].sort((a, b) => a.localStart.localeCompare(b.localStart))) {
    const day = dateOf(t.localStart)
    out.set(day, [...(out.get(day) ?? []), { text: range(t), maybe: !!t.isTentative }])
  }
  return [...out]
}

/**
 * A guest's status pill, Teams-style: "● Free" in green, a red "● Busy 10:00–10:30" ("● Busy
 * on 2 of 8 dates" on a series), or — only a Maybe to another meeting — an amber "● Maybe busy
 * 10:00–10:30"; busy and maybe-busy pills open the times. Only times, never with what. A
 * heads-up: nothing here stops the booking.
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
  const maybe = presence === 'maybe'
  const maybeDates = busy.maybeBusyDates ?? 0
  const groups = byDate(busy.busyTimes)
  const label = maybe
    ? series
      ? t('People:MaybeBusyOnDates', { count: maybeDates, total: dates })
      : t('People:MaybeBusyAt', { times: busy.busyTimes.map(range).join(', ') })
    : series
      ? t('People:BusyOnDates', { count: busy.busyDates, total: dates }) + (maybeDates > 0 ? ` · ${t('People:MaybeOnMore', { count: maybeDates })}` : '')
      : t('People:BusyAt', { times: firm(busy.busyTimes).map(range).join(', ') })
  return (
    <Popover>
      <PopoverTrigger
        className={cn(
          pill,
          'cursor-pointer outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50',
          maybe
            ? 'bg-[var(--state-expired-soft)] text-[var(--state-expired-ink)]'
            : 'bg-[var(--state-cancelled-soft)] text-[var(--state-cancelled-ink)]',
        )}
      >
        <span className={cn('size-1.5 rounded-full', maybe ? 'bg-[var(--presence-maybe)]' : 'bg-[var(--presence-busy)]')} aria-hidden="true" />
        {label}
      </PopoverTrigger>
      <PopoverContent align="end" className="w-64 p-3 text-sm">
        <p className="mb-1 text-xs font-semibold tracking-wide text-muted-foreground uppercase">
          {series ? t('People:BusyOnTheseDates') : t('People:BusyTimes')}
        </p>
        <ul className="m-0 grid list-none gap-0.5 p-0">
          {groups.map(([day, ranges]) => (
            <li key={day}>
              {formatDate(day)} ·{' '}
              {ranges.map((r, i) => (
                <span key={i}>
                  {i > 0 && ', '}
                  <span className="font-mono tabular-nums">{r.text}</span>
                  {r.maybe && <span className="text-[var(--state-expired-ink)]"> ({t('People:MaybeTag')})</span>}
                </span>
              ))}
            </li>
          ))}
        </ul>
        <p className="mt-2 text-xs text-muted-foreground">{t('People:BusyPrivacy')}</p>
      </PopoverContent>
    </Popover>
  )
}

/**
 * "You have another booking at this time (10:00–10:30)" — for a guest about to answer an
 * invite while already taken — in the busy pill's red; and, softer in amber, "You said Maybe
 * to another meeting then". Heads-ups only: Accept still works.
 */
export function BusyNote({ times }: { times: BusyTime[] }) {
  const { t } = useTranslation()
  const busy = firm(times)
  const maybe = tentative(times)
  if (times.length === 0) return null
  return (
    <>
      {busy.length > 0 && (
        <p
          role="note"
          className="m-0 flex items-start gap-2 rounded-md bg-[var(--state-cancelled-soft)] px-3 py-2 text-sm text-[var(--state-cancelled-ink)]"
        >
          <span className="mt-1.5 size-1.5 flex-none rounded-full bg-[var(--presence-busy)]" aria-hidden="true" />
          {t('Booking:BusyWhenAnswering', { times: busy.map(range).join(', ') })}
        </p>
      )}
      {maybe.length > 0 && (
        <p role="note" className="m-0 flex items-start gap-2 px-3 text-sm text-[var(--state-expired-ink)]">
          <span className="mt-1.5 size-1.5 flex-none rounded-full bg-[var(--presence-maybe)]" aria-hidden="true" />
          {t('Booking:MaybeWhenAnswering', { times: maybe.map(range).join(', ') })}
        </p>
      )}
    </>
  )
}

/**
 * One line above the guests: "2 people are busy at 10:00", "2 people are busy, 1 might be at
 * 10:00", "1 person might be busy at 10:00" — or, on a series, "Someone is busy on 4 of 8
 * dates" (plus "maybe on 1 more"). Red when anyone is busy, amber when it's only maybe.
 * Nothing when everyone is free.
 */
export function BusySummary({ busy, dates, start }: { busy: PersonBusy[]; dates: number; start: string }) {
  const { t } = useTranslation()
  const busyPeople = busy.filter((b) => b.busyDates > 0)
  const maybePeople = busy.filter((b) => b.busyDates === 0 && (b.maybeBusyDates ?? 0) > 0)
  if (busyPeople.length === 0 && maybePeople.length === 0) return null

  const firmDays = new Set(busy.flatMap((b) => firm(b.busyTimes).map((x) => dateOf(x.localStart))))
  const maybeDays = new Set(busy.flatMap((b) => tentative(b.busyTimes).map((x) => dateOf(x.localStart))).filter((d) => !firmDays.has(d)))
  const onlyMaybe = busyPeople.length === 0
  const time = timeOf(start)

  const text =
    dates > 1
      ? onlyMaybe
        ? t('People:MaybeSummarySeries', { count: maybeDays.size, total: dates })
        : t('People:BusySummarySeries', { count: firmDays.size, total: dates }) +
          (maybeDays.size > 0 ? ` · ${t('People:MaybeOnMore', { count: maybeDays.size })}` : '')
      : onlyMaybe
        ? t('People:MaybeSummary', { count: maybePeople.length, time })
        : maybePeople.length > 0
          ? t('People:BusyAndMaybeSummary', { count: busyPeople.length, maybe: maybePeople.length, time })
          : t('People:BusySummary', { count: busyPeople.length, time })

  return (
    <p
      role="status"
      className={cn(
        'm-0 flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium',
        onlyMaybe
          ? 'bg-[var(--state-expired-soft)] text-[var(--state-expired-ink)]'
          : 'bg-[var(--state-cancelled-soft)] text-[var(--state-cancelled-ink)]',
      )}
    >
      <span
        className={cn(
          'grid size-5 flex-none place-items-center rounded-full text-xs font-bold text-background',
          onlyMaybe ? 'bg-[var(--presence-maybe)]' : 'bg-[var(--presence-busy)]',
        )}
        aria-hidden="true"
      >
        !
      </span>
      {text}
    </p>
  )
}
