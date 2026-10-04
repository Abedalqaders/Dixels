import { useTranslation } from 'react-i18next'
import { describeDays, OperatingDays } from '@/features/space-management/operatingDays'
import { describeHours, OperatingWindow } from '@/features/space-management/operatingWindow'
import { formatDuration } from '@/features/bookings/format'
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
  const { t } = useTranslation()
  const overrideCount = [draft.days, draft.hours, draft.maxDurationMinutes].filter((v) => v !== null).length
  // "Overrides Building (Mon, Tue)" / "Inherited from Building — Mon, Tue"
  const note = (overridden: boolean, value: string) =>
    overridden
      ? t('Rules:OverridesParent', { level: t('Enum:ConstraintSource.Building'), value })
      : t('Rules:InheritedFrom', { level: t('Enum:ConstraintSource.Building'), value })

  return (
    <div className="level">
      <div className="levelhead">
        <h3>
          {t('Rules:FloorLevel')} — <span>{floorName}</span>
        </h3>
        <span className="ovr">{overrideCount ? t('Rules:OverrideCount', { count: overrideCount }) : t('Rules:InheritsEverything')}</span>
      </div>

      <InheritOverrideField
        label={t('Rules:OperatingDays')}
        isOverridden={draft.days !== null}
        onToggle={() => onChange({ ...draft, days: draft.days !== null ? null : parentDays })}
        note={note(draft.days !== null, describeDays(parentDays))}
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
        note={note(draft.hours !== null, describeHours(parentHours))}
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
        note={note(draft.maxDurationMinutes !== null, formatDuration(parentMaxDurationMinutes))}
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
