import { useConfirm } from '@/components/ConfirmDialog'

// Clears every overridable field on the current level back to Inherit in one click, rather
// than toggling each InheritOverrideField row individually. The reset logic itself (which
// fields, set to what) is the caller's — this is just the button + confirmation.

interface ResetToParentButtonProps {
  onReset: () => void
  disabled?: boolean
}

export function ResetToParentButton({ onReset, disabled }: ResetToParentButtonProps) {
  const { confirm, dialog } = useConfirm()

  async function handleClick() {
    const yes = await confirm({
      title: 'Reset every field on this level to Inherit?',
      description: "This discards all of this level's own overrides. Nothing is saved until you press Save.",
      confirmLabel: 'Reset to parent',
    })
    if (yes) onReset()
  }

  return (
    <>
      {dialog}
      <button type="button" className="btn sec" onClick={handleClick} disabled={disabled}>
        Reset to parent
      </button>
    </>
  )
}
