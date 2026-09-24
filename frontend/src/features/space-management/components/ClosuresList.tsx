import { useState } from 'react'
import type { FormEvent } from 'react'
import { OverrideEffect, ReasonCategory } from '../api/spaceManagementApi'
import type { AvailabilityOverrideDto, CreateAvailabilityOverrideDto, OverrideScope } from '../api/spaceManagementApi'
import { ChevronIcon, TrashIcon } from './actionIcons'

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
  const dateFmt = (d: Date) => d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })

  if (isMidnight(start) && isMidnight(end)) {
    // endsAt is exclusive, so the last actual day is the day before it.
    const lastDay = new Date(end)
    lastDay.setDate(lastDay.getDate() - 1)
    return start.toDateString() === lastDay.toDateString() ? dateFmt(start) : `${dateFmt(start)} – ${dateFmt(lastDay)}`
  }

  const dateTimeFmt = (d: Date) => d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })
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
}

export function ClosuresList({
  scope,
  scopeId,
  ownOverrides,
  ancestorOverrides,
  isCurrentlyClosed,
  onCreate,
  onDelete,
}: ClosuresListProps) {
  const hasAnyClosures = ownOverrides.length > 0 || ancestorOverrides.length > 0
  const [sectionOpen, setSectionOpen] = useState(hasAnyClosures)
  const [formOpen, setFormOpen] = useState(false)

  const [effect, setEffect] = useState<OverrideEffect>(OverrideEffect.Closed)
  const [reasonCategory, setReasonCategory] = useState<ReasonCategory>(ReasonCategory.Maintenance)
  const [startsAt, setStartsAt] = useState('')
  const [endsAt, setEndsAt] = useState('')
  const [reasonDetail, setReasonDetail] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!startsAt || !endsAt) return

    setSubmitting(true)
    try {
      await onCreate({
        scope,
        scopeId,
        startsAt: new Date(startsAt).toISOString(),
        endsAt: new Date(endsAt).toISOString(),
        effect,
        reasonCategory,
        reasonDetail: reasonDetail.trim() || null,
      })
      setStartsAt('')
      setEndsAt('')
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
                <button type="button" className="rowbtn" title="Delete closure" aria-label="Delete closure" onClick={() => onDelete(o.id)}>
                  <TrashIcon />
                </button>
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

          {!formOpen && (
            <button type="button" className="btn sm sec" style={{ marginTop: 'var(--space-3)' }} onClick={() => setFormOpen(true)}>
              + Add closure
            </button>
          )}

          {formOpen && (
            <form className="fields" onSubmit={handleSubmit} style={{ marginTop: 'var(--space-3)' }}>
              <div className="field">
                <span className="lbl">Effect</span>
                <select className="ctrl" value={effect} onChange={(e) => setEffect(Number(e.target.value) as OverrideEffect)}>
                  <option value={OverrideEffect.Closed}>Closed</option>
                  <option value={OverrideEffect.Open}>Open (special opening)</option>
                </select>
              </div>
              <div className="field">
                <span className="lbl">Reason</span>
                <select
                  className="ctrl"
                  value={reasonCategory}
                  onChange={(e) => setReasonCategory(Number(e.target.value) as ReasonCategory)}
                >
                  <option value={ReasonCategory.Maintenance}>Maintenance</option>
                  <option value={ReasonCategory.Holiday}>Holiday</option>
                  <option value={ReasonCategory.Event}>Event</option>
                  <option value={ReasonCategory.Other}>Other</option>
                </select>
              </div>
              <div className="field">
                <span className="lbl">Starts</span>
                <input className="ctrl mono" type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} />
              </div>
              <div className="field">
                <span className="lbl">Ends</span>
                <input className="ctrl mono" type="datetime-local" value={endsAt} onChange={(e) => setEndsAt(e.target.value)} />
              </div>
              <div className="field stacked">
                <span className="lbl">Reason detail (shown to staff)</span>
                <input className="ctrl" value={reasonDetail} onChange={(e) => setReasonDetail(e.target.value)} />
              </div>
              <div className="modalfoot" style={{ justifyContent: 'flex-start' }}>
                <button type="button" className="btn sec" onClick={() => setFormOpen(false)} disabled={submitting}>
                  Cancel
                </button>
                <button type="submit" className="btn sec" disabled={submitting || !startsAt || !endsAt}>
                  {submitting ? 'Adding…' : 'Save closure'}
                </button>
              </div>
            </form>
          )}
        </>
      )}
    </div>
  )
}
