import { OperatingDays } from '../domain/operatingDays'
import { OperatingWindow } from '../domain/operatingWindow'
import { DayChipPicker } from './DayChipPicker'
import { HoursRangeInput } from './HoursRangeInput'
import { DurationPicker, minutesToHours, hoursToMinutes } from './DurationPicker'
import { InheritOverrideField } from './InheritOverrideField'

// Port of the mock's #floorLevelBlock — each field is Inherit/Override, narrowed against
// the Building's own values (a Floor's parent is always the Building; see CONSTRAINTS.md).

export interface FloorDraft {
  days: OperatingDays | null
  hours: OperatingWindow | null
  maxDurationMinutes: number | null
}

interface FloorLevelFieldsProps {
  draft: FloorDraft
  floorName: string
  parentDays: OperatingDays
  parentHours: OperatingWindow
  parentMaxDurationMinutes: number
  onChange: (next: FloorDraft) => void
}

export function FloorLevelFields({
  draft,
  floorName,
  parentDays,
  parentHours,
  parentMaxDurationMinutes,
  onChange,
}: FloorLevelFieldsProps) {
  const overrideCount = [draft.days, draft.hours, draft.maxDurationMinutes].filter((v) => v !== null).length

  return (
    <div className="level">
      <div className="levelhead">
        <h3>
          Floor level — <span>{floorName}</span>
        </h3>
        <span className="ovr">{overrideCount ? `${overrideCount} override${overrideCount === 1 ? '' : 's'}` : 'Inherits everything'}</span>
      </div>

      <InheritOverrideField
        label="Operating days"
        isOverridden={draft.days !== null}
        onToggle={() => onChange({ ...draft, days: draft.days !== null ? null : parentDays })}
        note={
          draft.days !== null
            ? `Overrides Building (${describeDays(parentDays)})`
            : `Inherited from Building — ${describeDays(parentDays)}`
        }
      >
        <DayChipPicker
          value={draft.days ?? parentDays}
          parent={parentDays}
          disabled={draft.days === null}
          onChange={(days) => onChange({ ...draft, days })}
        />
      </InheritOverrideField>

      <InheritOverrideField
        label="Operating hours"
        isOverridden={draft.hours !== null}
        onToggle={() => onChange({ ...draft, hours: draft.hours !== null ? null : parentHours })}
        note={
          draft.hours !== null
            ? `Overrides Building (${describeHours(parentHours)})`
            : `Inherited from Building — ${describeHours(parentHours)}`
        }
      >
        <HoursRangeInput
          value={draft.hours ?? parentHours}
          parent={parentHours}
          disabled={draft.hours === null}
          onChange={(hours) => onChange({ ...draft, hours })}
        />
      </InheritOverrideField>

      <InheritOverrideField
        label="Maximum duration"
        isOverridden={draft.maxDurationMinutes !== null}
        onToggle={() =>
          onChange({ ...draft, maxDurationMinutes: draft.maxDurationMinutes !== null ? null : parentMaxDurationMinutes })
        }
        note={
          draft.maxDurationMinutes !== null
            ? `Overrides Building (${minutesToHours(parentMaxDurationMinutes)}h)`
            : `Inherited from Building — ${minutesToHours(parentMaxDurationMinutes)}h`
        }
      >
        <DurationPicker
          hours={minutesToHours(draft.maxDurationMinutes ?? parentMaxDurationMinutes)}
          disabled={draft.maxDurationMinutes === null}
          onChange={(hours) => onChange({ ...draft, maxDurationMinutes: hoursToMinutes(hours) })}
        />
      </InheritOverrideField>
    </div>
  )
}

function describeDays(days: OperatingDays): string {
  const names = days.toDayNames()
  if (names.length === 7) return 'Every day'
  if (names.length === 0) return 'None'
  return names.map((n) => n.slice(0, 3)).join(', ')
}

function describeHours(hours: OperatingWindow): string {
  return hours.isOpen24Hours ? '24 hours' : `${hours.open} – ${hours.close}`
}
