import { useTranslation } from 'react-i18next'
import { useConfirm } from '@/components/ConfirmDialog'

// Clears every overridable field on the current level back to Inherit in one click, rather
// than toggling each InheritOverrideField row individually. The reset logic itself (which
// fields, set to what) is the caller's — this is just the button + confirmation.

interface ResetToParentButtonProps {
  onReset: () => void
  disabled?: boolean
}

export function ResetToParentButton({ onReset, disabled }: ResetToParentButtonProps) {
  const { t } = useTranslation()
  const { confirm, dialog } = useConfirm()

  async function handleClick() {
    const yes = await confirm({
      title: t('Rules:ResetTitle'),
      description: t('Rules:ResetDetail'),
      confirmLabel: t('Rules:ResetToParent'),
    })
    if (yes) onReset()
  }

  return (
    <>
      {dialog}
      <button type="button" className="btn sec" onClick={handleClick} disabled={disabled}>
        {t('Rules:ResetToParent')}
      </button>
    </>
  )
}
