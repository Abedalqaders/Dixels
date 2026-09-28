import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { addDays, nowInZone } from '@/lib/time/buildingTime'
import type { HhMm, IsoDate } from '@/lib/time/buildingTime'
import type { BookableBuildingDto } from '@/features/bookings/api/bookingsApi'
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
 * narrowing; the floor is picked with the chips above the results. From only offers times that haven't passed, and To only times after
 * From. Dragging on a room's bar in the results picks any other time. Every control is a real picker on the building's clock and slot
 * grid, so there's nothing to type in a wrong format.
 */
export function SearchBar({ building, value, onChange }: SearchBarProps) {
  const slot = building.slotMinutes
  const now = nowInZone(building.timezone)
  const today = now.date
  const lastDate = addDays(today, building.maxHorizonDays)

  const spaceTypes = new Map<string, string>()
  for (const floor of building.floors) {
    for (const space of floor.spaces) spaceTypes.set(space.spaceTypeId, space.spaceTypeName)
  }

  return (
    <Card className="mt-6 p-4">
      <form
        role="search"
        aria-label="Find a free space"
        onSubmit={(e) => e.preventDefault()}
        className="grid grid-cols-2 items-end gap-3 md:grid-cols-3 xl:grid-cols-[1.3fr_1fr_1fr_0.7fr_1.2fr]"
      >
        <div className="col-span-2 grid gap-2 md:col-span-1">
          <Label htmlFor="fs-date">Date</Label>
          <DatePicker id="fs-date" value={value.date} min={today} max={lastDate} onChange={(date) => onChange({ date })} />
        </div>

        <FromToFields
          idPrefix="fs"
          start={value.start}
          end={value.end}
          slotMinutes={slot}
          minStart={value.date === today ? now.minutes + building.minLeadMinutes : 0}
          onChange={onChange}
        />

        <div className="grid gap-2">
          <Label htmlFor="fs-people">People</Label>
          <Input
            id="fs-people"
            type="number"
            min={1}
            className="font-mono"
            value={value.people}
            onChange={(e) => {
              const n = e.target.valueAsNumber
              if (Number.isInteger(n) && n >= 1) onChange({ people: n })
            }}
          />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="fs-type">Type</Label>
          <Select
            value={value.spaceTypeId || ANY}
            onValueChange={(v) => onChange({ spaceTypeId: v === ANY ? '' : v })}
          >
            <SelectTrigger id="fs-type" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Any type</SelectItem>
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
