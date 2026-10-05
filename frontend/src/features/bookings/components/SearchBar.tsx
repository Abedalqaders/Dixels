import { useTranslation } from 'react-i18next'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { addDays, fromMinutes, nowInZone, toMinutes } from '@/lib/time/buildingTime'
import type { HhMm, IsoDate } from '@/lib/time/buildingTime'
import type { BookableBuildingDto } from '@/features/bookings/api/bookingsApi'
import { biggestRoom, buildingRules } from '@/features/bookings/buildingRules'
import { fitsFreeTime, hasFreeTime, nearestFreeRange } from '@/features/bookings/freeTimes'
import { DatePicker } from './DatePicker'
import { FromToFields } from './FromToFields'

export interface SearchValues {
  date: IsoDate
  start: HhMm
  end: HhMm
  people: number
  spaceTypeId: string
}

interface SearchBarProps {
  building: BookableBuildingDto
  value: SearchValues
  onChange: (patch: Partial<SearchValues>) => void
}

// Radix Select can't use an empty string as an item value, so "no filter" has its own token.
const ANY = 'any'

/**
 * The question Find a space answers: when, and for how many people. Type is optional
 * narrowing; the floor is picked with the chips above the results. Only what some room could
 * take is offered: dates the building is open, From/To inside its hours and not yet passed,
 * no longer than the longest room allows, and no more people than the biggest room seats.
 * Dragging on a room's bar in the results picks any other time. Every control is a real
 * picker on the building's clock and slot grid, so there's nothing to type in a wrong format.
 */
export function SearchBar({ building, value, onChange }: SearchBarProps) {
  const { t } = useTranslation()
  const slot = building.slotMinutes
  const now = nowInZone(building.timezone)
  const today = now.date
  const lastDate = addDays(today, building.maxHorizonDays)
  const rulesOn = (date: IsoDate) => buildingRules(building, date, now)
  const maxPeople = biggestRoom(building)

  // A new date keeps the time if the building is open then; otherwise it moves to the
  // nearest open time that day, keeping the length where it fits.
  function changeDate(date: IsoDate) {
    const rules = rulesOn(date)
    const start = toMinutes(value.start)
    const end = toMinutes(value.end)
    const range = fitsFreeTime(start, end, rules) ? null : nearestFreeRange(start, end - start, rules)
    onChange(range ? { date, start: fromMinutes(range.start), end: fromMinutes(range.end) } : { date })
  }

  const spaceTypes = new Map<string, string>()
  for (const floor of building.floors) {
    for (const space of floor.spaces) spaceTypes.set(space.spaceTypeId, space.spaceTypeName)
  }

  return (
    <Card className="mt-6 p-4">
      <form
        role="search"
        aria-label={t('FindSpace:SearchLabel')}
        onSubmit={(e) => e.preventDefault()}
        className="grid grid-cols-1 items-end gap-3 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-[1.3fr_1fr_1fr_0.7fr_1.2fr]"
      >
        <div className="col-span-2 grid gap-2 md:col-span-1">
          <Label htmlFor="fs-date">{t('Booking:Date')}</Label>
          <DatePicker
            id="fs-date"
            value={value.date}
            min={today}
            max={lastDate}
            isDisabled={(d) => !hasFreeTime(rulesOn(d))}
            onChange={changeDate}
          />
        </div>

        <FromToFields
          idPrefix="fs"
          start={value.start}
          end={value.end}
          slotMinutes={slot}
          minStart={value.date === today ? now.minutes + building.minLeadMinutes : 0}
          rules={rulesOn(value.date)}
          onChange={onChange}
        />

        <div className="grid gap-2">
          <Label htmlFor="fs-people">{t('Booking:People')}</Label>
          <Input
            id="fs-people"
            type="number"
            min={1}
            max={maxPeople}
            className="tabular-nums"
            value={value.people}
            onChange={(e) => {
              // No room seats more than the biggest one, so a bigger group isn't offered.
              const n = e.target.valueAsNumber
              if (Number.isInteger(n) && n >= 1) onChange({ people: Math.min(n, maxPeople) })
            }}
          />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="fs-type">{t('FindSpace:Type')}</Label>
          <Select
            value={value.spaceTypeId || ANY}
            onValueChange={(v) => onChange({ spaceTypeId: v === ANY ? '' : v })}
          >
            <SelectTrigger id="fs-type" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>{t('FindSpace:AnyType')}</SelectItem>
              {[...spaceTypes].map(([id, name]) => (
                <SelectItem key={id} value={id}>
                  {name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

      </form>
    </Card>
  )
}
