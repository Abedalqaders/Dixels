import type { ReactNode } from 'react'

// The Inherit/Override switch, one per overridable Floor/Space field — the UI form of
// the nullable column behind it: "Inherit" stores null and follows the level above.
// Port of the mock's .inh block (admin-constraints.html / js/admin-constraints.js).

interface InheritOverrideFieldProps {
  label: string
  isOverridden: boolean
  onToggle: () => void
  note: string
  /** Defaults to "Override"/"Inherit" — pass e.g. "Set"/"Not set" for a field like
   * minimum attendees that isn't really "inherited" from anywhere. */
  onLabel?: string
  offLabel?: string
  children: ReactNode
}

export function InheritOverrideField({
  label,
  isOverridden,
  onToggle,
  note,
  onLabel = 'Override',
  offLabel = 'Inherit',
  children,
}: InheritOverrideFieldProps) {
  return (
    <div className={`inh${isOverridden ? ' on' : ''}`}>
      <div className="inh-head">
        <span className="lbl">{label}</span>
        <button type="button" className="inhbtn" onClick={onToggle}>
          <span className="sw2" />
          <span className="inhstate">{isOverridden ? onLabel : offLabel}</span>
        </button>
      </div>
      {children}
      <span className="inhnote">{note}</span>
    </div>
  )
}
