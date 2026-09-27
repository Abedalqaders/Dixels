import type { SpaceTypeDto } from '../api/spaceManagementApi'
import { ICONS, iconKeyToIconName } from './spaceTypeIcons'

interface SpaceTypeChipsProps {
  spaceTypes: SpaceTypeDto[]
  /** Selected space type id, or '' for all types. */
  value: string
  onChange: (spaceTypeId: string) => void
}

// One-click space-type filter shown above the spaces list. Space types are a short,
// admin-configured list (not hundreds like buildings), so every option fits on screen as a
// chip instead of hiding behind a dropdown — and the chip icons double as the legend.
export function SpaceTypeChips({ spaceTypes, value, onChange }: SpaceTypeChipsProps) {
  return (
    <div className="chipset typechips" role="group" aria-label="Filter by space type">
      <button type="button" className={value === '' ? 'on' : ''} aria-pressed={value === ''} onClick={() => onChange('')}>
        All types
      </button>
      {spaceTypes.map((st) => (
        <button
          key={st.id}
          type="button"
          className={value === st.id ? 'on' : ''}
          aria-pressed={value === st.id}
          onClick={() => onChange(value === st.id ? '' : st.id)}
        >
          {ICONS[iconKeyToIconName(st.iconKey)]}
          {st.name}
        </button>
      ))}
    </div>
  )
}
