import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import type { BookableFloorDto, SpaceAvailabilityDto } from '../api/bookingsApi'

interface FloorFilterProps {
  /** The building's floors, in its own order. */
  floors: BookableFloorDto[]
  /** The whole building's search results — each option counts its floor's free rooms from these. */
  spaces: SpaceAvailabilityDto[]
  /** The picked floor's id, or '' for all floors. */
  value: string
  onChange: (floorId: string) => void
}

// Radix Select can't use an empty string as an item value, so "all floors" has its own token.
const ALL = 'all'

/**
 * Narrows the results to one floor. A dropdown rather than chips so it stays one control
 * however many floors the building has; each option says how many rooms that floor has
 * free for the searched time, so the employee sees where to look before picking.
 */
export function FloorFilter({ floors, spaces, value, onChange }: FloorFilterProps) {
  const freeOn = (floorId: string) => spaces.filter((s) => s.floorId === floorId && s.isAvailable).length
  const allFree = spaces.filter((s) => s.isAvailable).length

  return (
    <div className="mb-4 flex items-center gap-2">
      <Label htmlFor="fs-floor" className="text-muted-foreground">
        Floor
      </Label>
      <Select value={value || ALL} onValueChange={(v) => onChange(v === ALL ? '' : v)}>
        <SelectTrigger id="fs-floor" className="w-56">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={ALL}>All floors · {allFree} free</SelectItem>
          {floors.map((f) => (
            <SelectItem key={f.id} value={f.id}>
              {f.name} · {freeOn(f.id)} free
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
}
