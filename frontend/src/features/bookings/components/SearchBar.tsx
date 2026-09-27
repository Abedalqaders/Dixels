import { addDays, fromMinutes, nowInZone, slotTimes, toMinutes } from '../../../lib/time/buildingTime'
import type { HhMm, IsoDate } from '../../../lib/time/buildingTime'
import type { BookableBuildingDto } from '../api/bookingsApi'

export interface SearchValues {
  date: IsoDate
  start: HhMm
  end: HhMm
  people: number
  floorId: string
  spaceTypeId: string
}

interface SearchBarProps {
  building: BookableBuildingDto
  value: SearchValues
  onChange: (patch: Partial<SearchValues>) => void
}

const DAY_MINUTES = 24 * 60

/**
 * The question Find a space answers: when, and for how many people. Floor and type are
 * optional narrowing. Every control is a real form control on the building's clock and
 * slot grid, so there's nothing to type in a wrong format.
 */
export function SearchBar({ building, value, onChange }: SearchBarProps) {
  const slot = building.slotMinutes
  const today = nowInZone(building.timezone).date
  const lastDate = addDays(today, building.maxHorizonDays)

  const spaceTypes = new Map<string, string>()
  for (const floor of building.floors) {
    for (const space of floor.spaces) spaceTypes.set(space.spaceTypeId, space.spaceTypeName)
  }

  function changeStart(next: HhMm) {
    // Moving the start keeps the length, so "an hour later" is one change, not two.
    const length = Math.max(toMinutes(value.end) - toMinutes(value.start), slot)
    onChange({ start: next, end: fromMinutes(Math.min(toMinutes(next) + length, DAY_MINUTES)) })
  }

  return (
    <form className="searchbar card" role="search" aria-label="Find a free space" onSubmit={(e) => e.preventDefault()}>
      <div className="field">
        <label className="lbl" htmlFor="fs-date">
          Date
        </label>
        <input
          id="fs-date"
          className="ctrl mono"
          type="date"
          min={today}
          max={lastDate}
          value={value.date}
          onChange={(e) => e.target.value && onChange({ date: e.target.value })}
        />
      </div>
      <div className="field">
        <label className="lbl" htmlFor="fs-from">
          From
        </label>
        <select id="fs-from" className="ctrl mono" value={value.start} onChange={(e) => changeStart(e.target.value)}>
          {slotTimes(slot, 0, DAY_MINUTES - slot).map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
      </div>
      <div className="field">
        <label className="lbl" htmlFor="fs-to">
          To
        </label>
        <select id="fs-to" className="ctrl mono" value={value.end} onChange={(e) => onChange({ end: e.target.value })}>
          {slotTimes(slot, toMinutes(value.start) + slot, DAY_MINUTES).map((t) => (
            <option key={t} value={t}>
              {t === '24:00' ? '24:00 (midnight)' : t}
            </option>
          ))}
        </select>
      </div>
      <div className="field people">
        <label className="lbl" htmlFor="fs-people">
          People
        </label>
        <input
          id="fs-people"
          className="ctrl mono"
          type="number"
          min={1}
          value={value.people}
          onChange={(e) => {
            const n = e.target.valueAsNumber
            if (Number.isInteger(n) && n >= 1) onChange({ people: n })
          }}
        />
      </div>
      <div className="field">
        <label className="lbl" htmlFor="fs-type">
          Type
        </label>
        <select id="fs-type" className="ctrl" value={value.spaceTypeId} onChange={(e) => onChange({ spaceTypeId: e.target.value })}>
          <option value="">Any type</option>
          {[...spaceTypes].map(([id, name]) => (
            <option key={id} value={id}>
              {name}
            </option>
          ))}
        </select>
      </div>
      {building.floors.length > 1 && (
        <div className="field">
          <label className="lbl" htmlFor="fs-floor">
            Floor
          </label>
          <select id="fs-floor" className="ctrl" value={value.floorId} onChange={(e) => onChange({ floorId: e.target.value })}>
            <option value="">Any floor</option>
            {building.floors.map((f) => (
              <option key={f.id} value={f.id}>
                {f.name}
              </option>
            ))}
          </select>
        </div>
      )}
    </form>
  )
}
