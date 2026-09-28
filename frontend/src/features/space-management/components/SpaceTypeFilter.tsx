import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import type { SpaceTypeDto } from '../api/spaceManagementApi'
import { ICONS, iconKeyToIconName } from './spaceTypeIcons'

// Radix Select reserves '' for "nothing selected", so "All types" needs a real value.
const ALL = 'all'

interface SpaceTypeFilterProps {
  spaceTypes: SpaceTypeDto[]
  /** Selected space type id, or '' for all types. */
  value: string
  onChange: (spaceTypeId: string) => void
}

/** Space-type filter for the spaces list — a shadcn dropdown with each type's icon. */
export function SpaceTypeFilter({ spaceTypes, value, onChange }: SpaceTypeFilterProps) {
  return (
    <Select value={value || ALL} onValueChange={(v) => onChange(v === ALL ? '' : v)}>
      <SelectTrigger size="sm" className="w-44" aria-label="Filter by space type">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={ALL}>All types</SelectItem>
        {spaceTypes.map((st) => (
          <SelectItem key={st.id} value={st.id}>
            {ICONS[iconKeyToIconName(st.iconKey)]}
            {st.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
