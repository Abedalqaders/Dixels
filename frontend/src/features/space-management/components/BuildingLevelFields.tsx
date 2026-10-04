import { useTranslation } from 'react-i18next'
import { OperatingDays } from '@/features/space-management/operatingDays'
import { OperatingWindow } from '@/features/space-management/operatingWindow'
import { DayChipPicker } from './DayChipPicker'
import { HoursRangeInput } from './HoursRangeInput'
import { DurationPicker, minutesToHours, hoursToMinutes } from './DurationPicker'
import type { OwnOverlapPolicy } from '@/features/space-management/api/spaceManagementApi'
import { OWN_OVERLAP_OPTIONS } from '@/features/space-management/ownOverlapPolicy'

// Building is the base layer — no Inherit/Override switches here, every value is
// required (see CONSTRAINTS.md). Port of the mock's #buildingLevelBlock — minus Timezone,
// which the mock put on this same screen but which the backend treats as an identity field
// (UpdateBuildingDto), not a constraint (UpdateBuildingConstraintsDto). Putting it here saved
// nothing on this page's own Save call, so it silently never persisted; it now lives in
// EditDetailsModal instead, alongside Name/Building number, where it actually gets saved.

export interface BuildingDraft {
  days: OperatingDays
  hours: OperatingWindow
  maxDurationMinutes: number
  maxHorizonDays: number
  maxSeriesHorizonDays: number
  minLeadMinutes: number
  ownOverlapPolicy: OwnOverlapPolicy
}

interface BuildingLevelFieldsProps {
  draft: BuildingDraft
  buildingName: string
  onChange: (next: BuildingDraft) => void
}

export function BuildingLevelFields({ draft, buildingName, onChange }: BuildingLevelFieldsProps) {
  const { t } = useTranslation()
  const overlapHint = OWN_OVERLAP_OPTIONS.find((o) => o.value === draft.ownOverlapPolicy)?.hintKey

  return (
    <div className="level">
      <div className="levelhead">
        <h3>
          {t('Rules:BuildingLevel')} — <span>{buildingName}</span>
        </h3>
        <span className="sc">{t('Rules:BaseLayer')}</span>
      </div>
      <div className="fields">
        <div className="field">
          <span className="lbl">{t('Rules:MaxBookingHorizon')}</span>
          <div className="pair">
            <input
              className="ctrl mono"
              type="number"
              min={1}
              value={draft.maxHorizonDays}
              onChange={(e) => {
                // The recurring horizon can't be the shorter one — it moves up with this one.
                const maxHorizonDays = Number(e.target.value)
                onChange({ ...draft, maxHorizonDays, maxSeriesHorizonDays: Math.max(draft.maxSeriesHorizonDays, maxHorizonDays) })
              }}
            />
            <span className="unit">{t('Rules:UnitDays')}</span>
          </div>
        </div>
        <div className="field">
          <label className="lbl" htmlFor="series-horizon">
            {t('Rules:SeriesHorizon')}
          </label>
          <div className="pair">
            <input
              id="series-horizon"
              className="ctrl mono"
              type="number"
              min={draft.maxHorizonDays}
              value={draft.maxSeriesHorizonDays}
              onChange={(e) => onChange({ ...draft, maxSeriesHorizonDays: Number(e.target.value) })}
              aria-describedby="series-horizon-hint"
            />
            <span className="unit">{t('Rules:UnitDays')}</span>
          </div>
          <span id="series-horizon-hint" className={draft.maxSeriesHorizonDays < draft.maxHorizonDays ? 'hint text-destructive' : 'hint'}>
            {draft.maxSeriesHorizonDays < draft.maxHorizonDays
              ? t('Rules:SeriesHorizonTooShort', { days: t('Rules:DayCount', { count: draft.maxHorizonDays }) })
              : t('Rules:SeriesHorizonHint')}
          </span>
        </div>
        <div className="field">
          <span className="lbl">{t('Rules:MinLeadTime')}</span>
          <div className="pair">
            <input
              className="ctrl mono"
              type="number"
              min={0}
              value={draft.minLeadMinutes}
              onChange={(e) => onChange({ ...draft, minLeadMinutes: Number(e.target.value) })}
            />
            <span className="unit">{t('Rules:UnitMinutes')}</span>
          </div>
        </div>
        <div className="field">
          <span className="lbl">{t('Rules:MaxDuration')}</span>
          <DurationPicker
            hours={minutesToHours(draft.maxDurationMinutes)}
            disabled={false}
            onChange={(hours) => onChange({ ...draft, maxDurationMinutes: hoursToMinutes(hours) })}
          />
        </div>
      </div>
      <div className="field stacked">
        <label className="lbl" htmlFor="own-overlap-policy">
          {t('Rules:OverlapPerPerson')}
        </label>
        <select
          id="own-overlap-policy"
          className="ctrl"
          value={draft.ownOverlapPolicy}
          onChange={(e) => onChange({ ...draft, ownOverlapPolicy: Number(e.target.value) as OwnOverlapPolicy })}
        >
          {OWN_OVERLAP_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {t(o.labelKey)}
            </option>
          ))}
        </select>
        <span className="hint">{overlapHint && t(overlapHint)}</span>
      </div>
      <div className="field stacked">
        <span className="lbl">{t('Rules:OperatingDays')}</span>
        <DayChipPicker
          value={draft.days}
          parent={OperatingDays.Everyday}
          disabled={false}
          onChange={(days) => onChange({ ...draft, days })}
        />
      </div>
      <div className="field stacked">
        <span className="lbl">{t('Rules:OperatingHours')}</span>
        <HoursRangeInput
          value={draft.hours}
          parent={OperatingWindow.FullDay}
          disabled={false}
          onChange={(hours) => onChange({ ...draft, hours })}
        />
      </div>
    </div>
  )
}
