import { useTranslation } from 'react-i18next'
import { describeDays, OperatingDays } from '@/features/space-management/operatingDays'
import { describeHours, OperatingWindow } from '@/features/space-management/operatingWindow'
import { formatDuration } from '@/features/bookings/format'
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

type ParentLevel = 'Building' | 'Floor'

interface SpaceLevelFieldsProps {
  draft: SpaceDraft
  spaceName: string
  capacity: number
  parentDays: OperatingDays
  parentDaysSource: ParentLevel
  parentHours: OperatingWindow
  parentHoursSource: ParentLevel
  parentMaxDurationMinutes: number
  parentMaxDurationSource: ParentLevel
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
  const { t } = useTranslation()
  const overrideCount = [draft.days, draft.hours, draft.maxDurationMinutes, draft.minAttendees].filter(
    (v) => v !== null,
  ).length
  // "Overrides Floor (Mon, Tue)" / "Inherited from Building — Mon, Tue"
  const note = (overridden: boolean, source: ParentLevel, value: string) =>
    overridden
      ? t('Rules:OverridesParent', { level: t(`Enum:ConstraintSource.${source}`), value })
      : t('Rules:InheritedFrom', { level: t(`Enum:ConstraintSource.${source}`), value })

  return (
    <div className="level">
      <div className="levelhead">
        <h3>
          {t('Rules:SpaceLevel')} — <span>{spaceName}</span>
        </h3>
        <span className="ovr">{overrideCount ? t('Rules:OverrideCount', { count: overrideCount }) : t('Rules:InheritsEverything')}</span>
      </div>

      <InheritOverrideField
        label={t('Rules:OperatingDays')}
        isOverridden={draft.days !== null}
        onToggle={() => onChange({ ...draft, days: draft.days !== null ? null : parentDays })}
        note={note(draft.days !== null, parentDaysSource, describeDays(parentDays))}
      >
        <DayChipPicker
          value={draft.days ?? parentDays}
          parent={parentDays}
          disabled={draft.days === null}
          onChange={(days) => onChange({ ...draft, days })}
        />
      </InheritOverrideField>

      <InheritOverrideField
        label={t('Rules:OperatingHours')}
        isOverridden={draft.hours !== null}
        onToggle={() => onChange({ ...draft, hours: draft.hours !== null ? null : parentHours })}
        note={note(draft.hours !== null, parentHoursSource, describeHours(parentHours))}
      >
        <HoursRangeInput
          value={draft.hours ?? parentHours}
          parent={parentHours}
          disabled={draft.hours === null}
          onChange={(hours) => onChange({ ...draft, hours })}
        />
      </InheritOverrideField>

      <InheritOverrideField
        label={t('Rules:MaxDuration')}
        isOverridden={draft.maxDurationMinutes !== null}
        onToggle={() =>
          onChange({ ...draft, maxDurationMinutes: draft.maxDurationMinutes !== null ? null : parentMaxDurationMinutes })
        }
        note={note(draft.maxDurationMinutes !== null, parentMaxDurationSource, formatDuration(parentMaxDurationMinutes))}
      >
        <DurationPicker
          hours={minutesToHours(draft.maxDurationMinutes ?? parentMaxDurationMinutes)}
          disabled={draft.maxDurationMinutes === null}
          onChange={(hours) => onChange({ ...draft, maxDurationMinutes: hoursToMinutes(hours) })}
        />
      </InheritOverrideField>

      <InheritOverrideField
        label={t('Rules:MinAttendees')}
        isOverridden={draft.minAttendees !== null}
        onToggle={() => onChange({ ...draft, minAttendees: draft.minAttendees !== null ? null : 2 })}
        onLabel={t('Rules:Set')}
        offLabel={t('Rules:NotSet')}
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
