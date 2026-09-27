import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { addDays, nowInZone } from '../../../lib/time/buildingTime'
import type { HhMm, IsoDate } from '../../../lib/time/buildingTime'
import type { BookableBuildingDto } from '../api/bookingsApi'
import { closingMinute } from '../preferences'
import { DatePicker } from './DatePicker'
import { TimeRangeFields } from './TimeRangeFields'

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
 * optional narrowing. Every control is a real picker on the building's clock and slot
 * grid, so there's nothing to type in a wrong format.
 */
export function SearchBar({ building, value, onChange }: SearchBarProps) {
  const slot = building.slotMinutes
  const now = nowInZone(building.timezone)
  const today = now.date
  const lastDate = addDays(today, building.maxHorizonDays)

  // "Until closing" in a search means the latest any matching space stays open — the rooms
  // that close earlier then show as unavailable, with their own hours as the reason.
  const matching = building.floors
    .filter((f) => !value.floorId || f.id === value.floorId)
    .flatMap((f) => f.spaces)
    .filter((sp) => !value.spaceTypeId || sp.spaceTypeId === value.spaceTypeId)
  const latestClosing = matching.length ? Math.max(...matching.map(closingMinute)) : undefined

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
        className="grid grid-cols-2 items-end gap-3 md:grid-cols-3 xl:grid-cols-[1.2fr_1fr_1.3fr_0.6fr_1fr_1fr]"
      >
        <div className="col-span-2 grid gap-2 md:col-span-1">
          <Label htmlFor="fs-date">Date</Label>
          <DatePicker id="fs-date" value={value.date} min={today} max={lastDate} onChange={(date) => onChange({ date })} />
        </div>

        <TimeRangeFields
          idPrefix="fs"
          start={value.start}
          end={value.end}
          slotMinutes={slot}
          minStartMinute={value.date === today ? now.minutes + building.minLeadMinutes : 0}
          closingMinute={latestClosing}
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
