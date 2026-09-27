import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { addDays, fromMinutes, nowInZone, toMinutes } from '../../../lib/time/buildingTime'
import type { HhMm, IsoDate } from '../../../lib/time/buildingTime'
import type { BookableBuildingDto } from '../api/bookingsApi'
import { DatePicker } from './DatePicker'
import { StartTimePicker } from './StartTimePicker'

const DAY_MINUTES = 24 * 60

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

// Radix Select can't use an empty string as an item value, so "no filter" has its own token.
const ANY = 'any'

/**
 * The question Find a space answers: when, and for how many people. Floor and type are
 * optional narrowing. There's no length field — rooms are checked for the usual length,
 * and any other length is picked by dragging on a room's bar in the results. Every control is a real picker on the building's clock and slot
 * grid, so there's nothing to type in a wrong format.
 */
export function SearchBar({ building, value, onChange }: SearchBarProps) {
  const slot = building.slotMinutes
  const now = nowInZone(building.timezone)
  const today = now.date
  const lastDate = addDays(today, building.maxHorizonDays)

  // The search checks "free at this time for your usual length"; the length itself is
  // picked by dragging on a room's bar. Moving the time keeps the length.
  function changeStart(next: HhMm) {
    const length = Math.max(toMinutes(value.end) - toMinutes(value.start), slot)
    const start = toMinutes(next)
    onChange({ start: next, end: fromMinutes(Math.min(start + length, DAY_MINUTES)) })
  }

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
        className="grid grid-cols-2 items-end gap-3 md:grid-cols-3 xl:grid-cols-[1.3fr_1fr_0.7fr_1.2fr_1.2fr]"
      >
        <div className="col-span-2 grid gap-2 md:col-span-1">
          <Label htmlFor="fs-date">Date</Label>
          <DatePicker id="fs-date" value={value.date} min={today} max={lastDate} onChange={(date) => onChange({ date })} />
        </div>

        <div className="grid gap-2">
          <Label htmlFor="fs-at">At</Label>
          <StartTimePicker
            id="fs-at"
            value={value.start}
            slotMinutes={slot}
            minMinute={value.date === today ? now.minutes + building.minLeadMinutes : 0}
            onChange={changeStart}
          />
        </div>

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

        {building.floors.length > 1 && (
          <div className="grid gap-2">
            <Label htmlFor="fs-floor">Floor</Label>
            <Select value={value.floorId || ANY} onValueChange={(v) => onChange({ floorId: v === ANY ? '' : v })}>
              <SelectTrigger id="fs-floor" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ANY}>Any floor</SelectItem>
                {building.floors.map((f) => (
                  <SelectItem key={f.id} value={f.id}>
                    {f.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}
      </form>
    </Card>
  )
}
