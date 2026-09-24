// Clears every overridable field on the current level back to Inherit in one click, rather
// than toggling each InheritOverrideField row individually. The reset logic itself (which
// fields, set to what) is the caller's — this is just the button + confirmation.

interface ResetToParentButtonProps {
  onReset: () => void
  disabled?: boolean
}

export function ResetToParentButton({ onReset, disabled }: ResetToParentButtonProps) {
  function handleClick() {
    if (window.confirm('Reset every field on this level back to Inherit? This discards all of this level\'s own overrides.')) {
      onReset()
    }
  }

  return (
    <button type="button" className="btn sec" onClick={handleClick} disabled={disabled}>
      Reset to parent
    </button>
  )
}
