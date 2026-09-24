import { OperatingDays } from '../domain/operatingDays'
import { OperatingWindow } from '../domain/operatingWindow'
import { DayChipPicker } from './DayChipPicker'
import { HoursRangeInput } from './HoursRangeInput'
import { DurationPicker, minutesToHours, hoursToMinutes } from './DurationPicker'
import { AttendeesStepper, attendeesStepperNote } from './AttendeesStepper'
import { InheritOverrideField } from './InheritOverrideField'

// Port of the mock's #spaceLevelBlock — a Space inherits from its Floor first, and only
// then from the Building (see CONSTRAINTS.md), so the narrow-only check and the "inherited
// from" notes are both against the Floor's *resolved* value, not the Building directly.

export interface SpaceDraft {
  days: OperatingDays | null
  hours: OperatingWindow | null
  maxDurationMinutes: number | null
  minAttendees: number | null
}

interface SpaceLevelFieldsProps {
  draft: SpaceDraft
  spaceName: string
  capacity: number
  parentDays: OperatingDays
  parentDaysSource: 'Building' | 'Floor'
  parentHours: OperatingWindow
  parentHoursSource: 'Building' | 'Floor'
  parentMaxDurationMinutes: number
  parentMaxDurationSource: 'Building' | 'Floor'
  onChange: (next: SpaceDraft) => void
}

// A Space inherits from its Floor first, then the Building — but columns don't travel in
// groups (CONSTRAINTS.md): a Floor might override hours without overriding days, so each
// field's "inherited from" provenance is tracked independently, not as one blended label.
export function SpaceLevelFields({
  draft,
  spaceName,
  capacity,
  parentDays,
  parentDaysSource,
  parentHours,
  parentHoursSource,
  parentMaxDurationMinutes,
  parentMaxDurationSource,
  onChange,
}: SpaceLevelFieldsProps) {
  const overrideCount = [draft.days, draft.hours, draft.maxDurationMinutes, draft.minAttendees].filter(
    (v) => v !== null,
  ).length

  return (
    <div className="level">
      <div className="levelhead">
        <h3>
          Space level — <span>{spaceName}</span>
        </h3>
        <span className="ovr">{overrideCount ? `${overrideCount} override${overrideCount === 1 ? '' : 's'}` : 'Inherits everything'}</span>
      </div>

      <InheritOverrideField
        label="Operating days"
        isOverridden={draft.days !== null}
        onToggle={() => onChange({ ...draft, days: draft.days !== null ? null : parentDays })}
        note={
          draft.days !== null
            ? `Overrides ${parentDaysSource} (${describeDays(parentDays)})`
            : `Inherited from ${parentDaysSource} — ${describeDays(parentDays)}`
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
            ? `Overrides ${parentHoursSource} (${describeHours(parentHours)})`
            : `Inherited from ${parentHoursSource} — ${describeHours(parentHours)}`
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
            ? `Overrides ${parentMaxDurationSource} (${minutesToHours(parentMaxDurationMinutes)}h)`
            : `Inherited from ${parentMaxDurationSource} — ${minutesToHours(parentMaxDurationMinutes)}h`
        }
      >
        <DurationPicker
          hours={minutesToHours(draft.maxDurationMinutes ?? parentMaxDurationMinutes)}
          disabled={draft.maxDurationMinutes === null}
          onChange={(hours) => onChange({ ...draft, maxDurationMinutes: hoursToMinutes(hours) })}
        />
      </InheritOverrideField>

      <InheritOverrideField
        label="Minimum attendees"
        isOverridden={draft.minAttendees !== null}
        onToggle={() => onChange({ ...draft, minAttendees: draft.minAttendees !== null ? null : 2 })}
        onLabel="Set"
        offLabel="Not set"
        note={attendeesStepperNote(draft.minAttendees, capacity)}
      >
        <AttendeesStepper
          value={draft.minAttendees}
          capacity={capacity}
          disabled={draft.minAttendees === null}
          onChange={(minAttendees) => onChange({ ...draft, minAttendees })}
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
