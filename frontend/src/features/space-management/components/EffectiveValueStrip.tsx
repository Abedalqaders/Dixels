import { useTranslation } from 'react-i18next'

// The "What applies here" strip — the resolved value for each field, and which level
// decided it. Port of the mock's #effGrid/renderEffective (admin-constraints.html /
// js/admin-constraints.js), driven by real ResolvedConstraintsDto data instead of a
// sessionStorage model.

export type EffectiveSource = 'Building' | 'Floor' | 'Space' | 'None'

export interface EffectiveItem {
  label: string
  value: string
  source: EffectiveSource
}

export function EffectiveValueStrip({ items }: { items: EffectiveItem[] }) {
  const { t } = useTranslation()
  return (
    <div className="effective">
      <div className="effhead">
        <h3>{t('Rules:WhatApplies')}</h3>
        <span className="sc">{t('Rules:WhatAppliesHint')}</span>
      </div>
      <div className="effgrid">
        {items.map((item) => (
          <div className="effitem" key={item.label}>
            <span className="k">{item.label}</span>
            <span className="v">{item.value}</span>
            <span className={`src ${item.source.toLowerCase()}`}>
              {item.source === 'None' ? t('Rules:NotSet') : t(`Enum:ConstraintSource.${item.source}`)}
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}
