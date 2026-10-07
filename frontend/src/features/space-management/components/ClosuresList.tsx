import { useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { ChevronDownIcon } from 'lucide-react'
import i18n from '@/i18n'
import type { TextKeys } from '@/i18n/keys'
import { formatClock, formatDay, localDateToIso } from '@/lib/time/format'
import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { TablePagination } from '@/components/TablePagination'
import { PAGE_SIZE_OPTIONS } from '@/hooks/useListParams'
import { OverrideEffect, ReasonCategory } from '@/features/space-management/api/spaceManagementApi'
import type { AvailabilityOverrideDto, CreateAvailabilityOverrideDto, OverrideScope } from '@/features/space-management/api/spaceManagementApi'
import { ChevronIcon, TrashIcon } from './actionIcons'

/** A calendar popover for the date, plus a plain time input — closures aren't bound to a
 * booking horizon or a slot grid, so there's no min/max or slotMinutes to honour here. */
function DateTimeField({
  id,
  label,
  timeLabel,
  date,
  time,
  onDateChange,
  onTimeChange,
}: {
  id: string
  label: string
  /** The time input's accessible name: "Starts time". */
  timeLabel: string
  date: Date | undefined
  time: string
  onDateChange: (date: Date | undefined) => void
  onTimeChange: (time: string) => void
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)

  return (
    <div className="grid gap-2">
      <Label htmlFor={id}>{label}</Label>
      <div className="flex gap-2">
        <Popover open={open} onOpenChange={setOpen}>
          <PopoverTrigger asChild>
            <Button id={id} type="button" variant="outline" className="flex-1 justify-between font-normal">
              {date ? formatDay(localDateToIso(date), 'long') : t('Rules:PickDate')}
              <ChevronDownIcon className="text-muted-foreground" />
            </Button>
          </PopoverTrigger>
          <PopoverContent className="w-auto overflow-hidden p-0" align="start">
            <Calendar
              mode="single"
              selected={date}
              defaultMonth={date}
              onSelect={(next) => {
                onDateChange(next)
                setOpen(false)
              }}
            />
          </PopoverContent>
        </Popover>
        <Input
          type="time"
          className="w-28 font-mono"
          aria-label={timeLabel}
          value={time}
          onChange={(e) => onTimeChange(e.target.value)}
        />
      </div>
    </div>
  )
}

/** Midnight unless a time was picked — the common case is a whole-day closure. */
function combine(date: Date | undefined, time: string): Date | null {
  if (!date) return null
  const [hours, minutes] = time ? time.split(':').map(Number) : [0, 0]
  const combined = new Date(date)
  combined.setHours(hours || 0, minutes || 0, 0, 0)
  return combined
}

// Dated closures (or special openings) — a real, repeatable list per scope, not the
// mock's single hardcoded oosFrom/oosUntil/oosReason field. Closures union rather than
// override (see CONSTRAINTS.md): any level can block, and a Closed override at any level
// always wins over an overlapping Open one — so a level's own closures are editable here,
// but its ancestors' closures are shown read-only, tagged by which level they came from.
//
// Condensed on purpose: this used to render a mandatory 2-line block per closure plus an
// always-visible 5-field add form below the list, even when there was nothing to show —
// a lot of surface area for what's usually a rarely-touched, empty section. Now each
// closure is one line (a color dot + reason + compact date range, full detail in a title
// tooltip), the add form hides behind a button, and the whole section collapses to a
// one-line summary when there's genuinely nothing in it.

const EFFECT_LABELS: Record<OverrideEffect, keyof TextKeys> = {
  [OverrideEffect.Closed]: 'Rules:EffectClosed',
  [OverrideEffect.Open]: 'Rules:EffectOpen',
}
const REASON_LABELS: Record<ReasonCategory, keyof TextKeys> = {
  [ReasonCategory.Maintenance]: 'Enum:ReasonCategory.Maintenance',
  [ReasonCategory.Holiday]: 'Enum:ReasonCategory.Holiday',
  [ReasonCategory.Event]: 'Enum:ReasonCategory.Event',
  [ReasonCategory.Other]: 'Enum:ReasonCategory.Other',
}

const SHORT_DETAIL_MAX = 40

/** Day-only formatting ("Dec 25" / "Dec 25 – 26") for the common case of a closure that
 * spans exact midnight-to-midnight day boundaries; falls back to full date+time otherwise
 * (e.g. the 9am–1pm maintenance-window shape). Checked in the *viewer's local* time, not
 * the building's own timezone — a deliberate simplification for this condensed display
 * (CONSTRAINTS.md's own timezone rules still govern the real resolution logic elsewhere);
 * it's accurate for the common single-tenant case of admins in the same timezone as the
 * buildings they manage, not guaranteed across timezones. */
export function formatWhen(startsAt: string, endsAt: string): string {
  const start = new Date(startsAt)
  const end = new Date(endsAt)
  const isMidnight = (d: Date) => d.getHours() === 0 && d.getMinutes() === 0
  const dateFmt = (d: Date) => formatDay(localDateToIso(d), 'day-month')

  if (isMidnight(start) && isMidnight(end)) {
    // endsAt is exclusive, so the last actual day is the day before it.
    const lastDay = new Date(end)
    lastDay.setDate(lastDay.getDate() - 1)
    return start.toDateString() === lastDay.toDateString() ? dateFmt(start) : `${dateFmt(start)} – ${dateFmt(lastDay)}`
  }

  const pad = (n: number) => String(n).padStart(2, '0')
  const dateTimeFmt = (d: Date) => `${dateFmt(d)} ${formatClock(`${pad(d.getHours())}:${pad(d.getMinutes())}`)}`
  // The arrow points the way the language reads: "→" in English, "←" in Arabic.
  return i18n.t('Rules:ClosureSpan', { start: dateTimeFmt(start), end: dateTimeFmt(end) })
}

export type AncestorLevel = 'Building' | 'Floor'

export interface AncestorClosure {
  override: AvailabilityOverrideDto
  level: AncestorLevel
}

interface ClosuresListProps {
  scope: OverrideScope
  scopeId: string
  /** One page of this level's own closures. */
  ownOverrides: AvailabilityOverrideDto[]
  ownTotalCount: number
  /** Zero-indexed. */
  page: number
  pageSize: number
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
  /** Past closures too (most recent first); otherwise upcoming and current only. */
  showPast: boolean
  onShowPastChange: (showPast: boolean) => void
  /** The first page of each level above's closures… */
  ancestorOverrides: AncestorClosure[]
  /** …and how many more each has, shown on its own page. */
  ancestorMore: { level: AncestorLevel; count: number }[]
  /** From what's in effect right now — not from the page shown here. */
  isCurrentlyClosed: boolean
  onCreate: (input: CreateAvailabilityOverrideDto) => void | Promise<void>
  onDelete: (id: string) => void | Promise<void>
  /** Offer "+ Add closure" — only with Overrides.Create. */
  canCreate: boolean
  /** Offer deleting this level's own closures — only with Overrides.Delete. */
  canDelete: boolean
}

export function ClosuresList({
  scope,
  scopeId,
  ownOverrides,
  ownTotalCount,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
  showPast,
  onShowPastChange,
  ancestorOverrides,
  ancestorMore,
  isCurrentlyClosed,
  onCreate,
  onDelete,
  canCreate,
  canDelete,
}: ClosuresListProps) {
  const { t } = useTranslation()
  const hasAnyClosures = ownTotalCount > 0 || ancestorOverrides.length > 0
  const [sectionOpen, setSectionOpen] = useState(hasAnyClosures)
  const [formOpen, setFormOpen] = useState(false)

  const [effect, setEffect] = useState<OverrideEffect>(OverrideEffect.Closed)
  const [reasonCategory, setReasonCategory] = useState<ReasonCategory>(ReasonCategory.Maintenance)
  const [startDate, setStartDate] = useState<Date | undefined>(undefined)
  const [startTime, setStartTime] = useState('')
  const [endDate, setEndDate] = useState<Date | undefined>(undefined)
  const [endTime, setEndTime] = useState('')
  const [reasonDetail, setReasonDetail] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const startsAt = combine(startDate, startTime)
  const endsAt = combine(endDate, endTime)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!startsAt || !endsAt) return

    setSubmitting(true)
    try {
      await onCreate({
        scope,
        scopeId,
        startsAt: startsAt.toISOString(),
        endsAt: endsAt.toISOString(),
        effect,
        reasonCategory,
        reasonDetail: reasonDetail.trim() || null,
        // The page asks what this closure would break, then sends the admin's choice.
        cancelAffectedBookings: false,
      })
      setStartDate(undefined)
      setStartTime('')
      setEndDate(undefined)
      setEndTime('')
      setReasonDetail('')
      setFormOpen(false)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="level">
      <button
        type="button"
        className="levelhead closuretoggle"
        aria-expanded={sectionOpen}
        onClick={() => setSectionOpen((v) => !v)}
      >
        <h3>{t('Rules:Closures')}</h3>
        {isCurrentlyClosed && <span className="badge blocked">{t('Rules:CurrentlyClosed')}</span>}
        {!hasAnyClosures && <span className="ovr">{t(showPast ? 'Rules:NoneSet' : 'Rules:NoneUpcoming')}</span>}
        <span className="chevicon" aria-hidden="true">
          <ChevronIcon />
        </span>
      </button>

      {sectionOpen && (
        <>
          <label className="my-2 flex w-fit items-center gap-2 text-sm text-muted-foreground">
            <input type="checkbox" checked={showPast} onChange={(e) => onShowPastChange(e.target.checked)} />
            {t('Rules:ShowPastClosures')}
          </label>

          {!hasAnyClosures && <p className="inhnote">{t(showPast ? 'Rules:NoClosures' : 'Rules:NoUpcomingClosures')}</p>}

          {ownOverrides.map((o) => {
            const detail = o.reasonDetail && o.reasonDetail.length <= SHORT_DETAIL_MAX ? o.reasonDetail : null
            return (
              <div className="closurerow" key={o.id} title={o.reasonDetail ?? undefined}>
                <span className={`closuredot ${o.effect === OverrideEffect.Closed ? 'closed' : 'open'}`} />
                <span className="closurelabel">
                  {t(EFFECT_LABELS[o.effect])} — {t(REASON_LABELS[o.reasonCategory])} · {formatWhen(o.startsAt, o.endsAt)}
                  {detail ? ` — ${detail}` : ''}
                </span>
                {canDelete && (
                  <button
                    type="button"
                    className="rowbtn"
                    title={t('Rules:DeleteClosure')}
                    aria-label={t('Rules:DeleteClosure')}
                    onClick={() => onDelete(o.id)}
                  >
                    <TrashIcon />
                  </button>
                )}
              </div>
            )
          })}

          {ownTotalCount > PAGE_SIZE_OPTIONS[0] && (
            <TablePagination
              page={page}
              pageSize={pageSize}
              totalCount={ownTotalCount}
              onPageChange={onPageChange}
              onPageSizeChange={onPageSizeChange}
            />
          )}

          {ancestorOverrides.map(({ override: o, level }) => (
            <div className="closurerow" key={o.id} title={o.reasonDetail ?? undefined}>
              <span className={`closuredot ${o.effect === OverrideEffect.Closed ? 'closed' : 'open'}`} />
              <span className="closurelabel">
                {t(EFFECT_LABELS[o.effect])} — {t(REASON_LABELS[o.reasonCategory])} · {formatWhen(o.startsAt, o.endsAt)}
              </span>
              <span className="ovr">{t('Rules:FromLevel', { level: t(`Enum:ConstraintSource.${level}`) })}</span>
            </div>
          ))}

          {ancestorMore.map(({ level, count }) => (
            <p className="inhnote" key={level}>
              {t('Rules:MoreFromLevel', { count, level: t(`Enum:ConstraintSource.${level}`) })}
            </p>
          ))}

          {canCreate && !formOpen && (
            <button type="button" className="btn sm sec" style={{ marginTop: 'var(--space-3)' }} onClick={() => setFormOpen(true)}>
              {t('Rules:AddClosure')}
            </button>
          )}

          {canCreate && formOpen && (
            <form className="fields" onSubmit={handleSubmit} style={{ marginTop: 'var(--space-3)' }}>
              <div className="grid gap-2">
                <Label htmlFor="closure-effect">{t('Rules:Effect')}</Label>
                <Select value={String(effect)} onValueChange={(v) => setEffect(Number(v) as OverrideEffect)}>
                  <SelectTrigger id="closure-effect" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={String(OverrideEffect.Closed)}>{t('Rules:EffectClosed')}</SelectItem>
                    <SelectItem value={String(OverrideEffect.Open)}>{t('Rules:EffectOpenSpecial')}</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label htmlFor="closure-reason">{t('Rules:Reason')}</Label>
                <Select value={String(reasonCategory)} onValueChange={(v) => setReasonCategory(Number(v) as ReasonCategory)}>
                  <SelectTrigger id="closure-reason" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {Object.values(ReasonCategory).map((category) => (
                      <SelectItem key={category} value={String(category)}>
                        {t(REASON_LABELS[category])}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <DateTimeField
                id="closure-starts"
                label={t('Rules:Starts')}
                timeLabel={t('Rules:StartsTime')}
                date={startDate}
                time={startTime}
                onDateChange={setStartDate}
                onTimeChange={setStartTime}
              />
              <DateTimeField
                id="closure-ends"
                label={t('Rules:Ends')}
                timeLabel={t('Rules:EndsTime')}
                date={endDate}
                time={endTime}
                onDateChange={setEndDate}
                onTimeChange={setEndTime}
              />
              <div className="grid gap-2 field stacked">
                <Label htmlFor="closure-detail">{t('Rules:ReasonDetail')}</Label>
                <Input id="closure-detail" value={reasonDetail} onChange={(e) => setReasonDetail(e.target.value)} />
              </div>
              <div className="modalfoot" style={{ justifyContent: 'flex-start' }}>
                <Button type="button" variant="outline" onClick={() => setFormOpen(false)} disabled={submitting}>
                  {t('Common:Cancel')}
                </Button>
                <Button type="submit" variant="outline" disabled={submitting || !startsAt || !endsAt}>
                  {submitting ? t('Rules:Adding') : t('Rules:SaveClosure')}
                </Button>
              </div>
            </form>
          )}
        </>
      )}
    </div>
  )
}
