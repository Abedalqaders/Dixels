import { useState } from 'react'
import type { FormEvent } from 'react'
import { ChevronDownIcon } from 'lucide-react'
import { formatClock, formatDay, localDateToIso } from '@/lib/time/format'
import { Button } from '@/components/ui/button'
import { Calendar } from '@/components/ui/calendar'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { OverrideEffect, ReasonCategory } from '@/features/space-management/api/spaceManagementApi'
import type { AvailabilityOverrideDto, CreateAvailabilityOverrideDto, OverrideScope } from '@/features/space-management/api/spaceManagementApi'
import { ChevronIcon, TrashIcon } from './actionIcons'

/** A calendar popover for the date, plus a plain time input — closures aren't bound to a
 * booking horizon or a slot grid, so there's no min/max or slotMinutes to honour here. */
function DateTimeField({
  id,
  label,
  date,
  time,
  onDateChange,
  onTimeChange,
}: {
  id: string
  label: string
  date: Date | undefined
  time: string
  onDateChange: (date: Date | undefined) => void
  onTimeChange: (time: string) => void
}) {
  const [open, setOpen] = useState(false)

  return (
    <div className="grid gap-2">
      <Label htmlFor={id}>{label}</Label>
      <div className="flex gap-2">
        <Popover open={open} onOpenChange={setOpen}>
          <PopoverTrigger asChild>
            <Button id={id} type="button" variant="outline" className="flex-1 justify-between font-normal">
              {date ? formatDay(localDateToIso(date), 'long') : 'Pick a date'}
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
          aria-label={`${label} time`}
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

const EFFECT_LABELS: Record<OverrideEffect, string> = { [OverrideEffect.Closed]: 'Closed', [OverrideEffect.Open]: 'Open' }
const REASON_LABELS: Record<ReasonCategory, string> = {
  [ReasonCategory.Maintenance]: 'Maintenance',
  [ReasonCategory.Holiday]: 'Holiday',
  [ReasonCategory.Event]: 'Event',
  [ReasonCategory.Other]: 'Other',
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
  return `${dateTimeFmt(start)} → ${dateTimeFmt(end)}`
}

interface AncestorClosure {
  override: AvailabilityOverrideDto
  levelLabel: string
}

interface ClosuresListProps {
  scope: OverrideScope
  scopeId: string
  ownOverrides: AvailabilityOverrideDto[]
  ancestorOverrides: AncestorClosure[]
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
  ancestorOverrides,
  isCurrentlyClosed,
  onCreate,
  onDelete,
  canCreate,
  canDelete,
}: ClosuresListProps) {
  const hasAnyClosures = ownOverrides.length > 0 || ancestorOverrides.length > 0
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
        <h3>Closures</h3>
        {isCurrentlyClosed && <span className="badge blocked">Currently closed</span>}
        {!hasAnyClosures && <span className="ovr">None set</span>}
        <span className="chevicon" aria-hidden="true">
          <ChevronIcon />
        </span>
      </button>

      {sectionOpen && (
        <>
          {!hasAnyClosures && <p className="inhnote">No closures set — in service.</p>}

          {ownOverrides.map((o) => {
            const detail = o.reasonDetail && o.reasonDetail.length <= SHORT_DETAIL_MAX ? o.reasonDetail : null
            return (
              <div className="closurerow" key={o.id} title={o.reasonDetail ?? undefined}>
                <span className={`closuredot ${o.effect === OverrideEffect.Closed ? 'closed' : 'open'}`} />
                <span className="closurelabel">
                  {EFFECT_LABELS[o.effect]} — {REASON_LABELS[o.reasonCategory]} · {formatWhen(o.startsAt, o.endsAt)}
                  {detail ? ` — ${detail}` : ''}
                </span>
                {canDelete && (
                  <button type="button" className="rowbtn" title="Delete closure" aria-label="Delete closure" onClick={() => onDelete(o.id)}>
                    <TrashIcon />
                  </button>
                )}
              </div>
            )
          })}

          {ancestorOverrides.map(({ override: o, levelLabel }) => (
            <div className="closurerow" key={o.id} title={o.reasonDetail ?? undefined}>
              <span className={`closuredot ${o.effect === OverrideEffect.Closed ? 'closed' : 'open'}`} />
              <span className="closurelabel">
                {EFFECT_LABELS[o.effect]} — {REASON_LABELS[o.reasonCategory]} · {formatWhen(o.startsAt, o.endsAt)}
              </span>
              <span className="ovr">From {levelLabel}</span>
            </div>
          ))}

          {canCreate && !formOpen && (
            <button type="button" className="btn sm sec" style={{ marginTop: 'var(--space-3)' }} onClick={() => setFormOpen(true)}>
              + Add closure
            </button>
          )}

          {canCreate && formOpen && (
            <form className="fields" onSubmit={handleSubmit} style={{ marginTop: 'var(--space-3)' }}>
              <div className="grid gap-2">
                <Label htmlFor="closure-effect">Effect</Label>
                <Select value={String(effect)} onValueChange={(v) => setEffect(Number(v) as OverrideEffect)}>
                  <SelectTrigger id="closure-effect" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={String(OverrideEffect.Closed)}>Closed</SelectItem>
                    <SelectItem value={String(OverrideEffect.Open)}>Open (special opening)</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label htmlFor="closure-reason">Reason</Label>
                <Select value={String(reasonCategory)} onValueChange={(v) => setReasonCategory(Number(v) as ReasonCategory)}>
                  <SelectTrigger id="closure-reason" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={String(ReasonCategory.Maintenance)}>Maintenance</SelectItem>
                    <SelectItem value={String(ReasonCategory.Holiday)}>Holiday</SelectItem>
                    <SelectItem value={String(ReasonCategory.Event)}>Event</SelectItem>
                    <SelectItem value={String(ReasonCategory.Other)}>Other</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <DateTimeField id="closure-starts" label="Starts" date={startDate} time={startTime} onDateChange={setStartDate} onTimeChange={setStartTime} />
              <DateTimeField id="closure-ends" label="Ends" date={endDate} time={endTime} onDateChange={setEndDate} onTimeChange={setEndTime} />
              <div className="grid gap-2 field stacked">
                <Label htmlFor="closure-detail">Reason detail (shown to staff)</Label>
                <Input id="closure-detail" value={reasonDetail} onChange={(e) => setReasonDetail(e.target.value)} />
              </div>
              <div className="modalfoot" style={{ justifyContent: 'flex-start' }}>
                <Button type="button" variant="outline" onClick={() => setFormOpen(false)} disabled={submitting}>
                  Cancel
                </Button>
                <Button type="submit" variant="outline" disabled={submitting || !startsAt || !endsAt}>
                  {submitting ? 'Adding…' : 'Save closure'}
                </Button>
              </div>
            </form>
          )}
        </>
      )}
    </div>
  )
}
