import { useState } from 'react'
import type { ComponentProps } from 'react'
import { useTranslation } from 'react-i18next'
import { EyeIcon, EyeOffIcon } from 'lucide-react'
import { Input } from '@/components/ui/input'

/**
 * A password box with an eye button at its end that shows what was typed, so a long
 * password can be checked before it's sent. Showing it is per box and forgotten on reload.
 */
export function PasswordInput(props: Omit<ComponentProps<typeof Input>, 'type'>) {
  const { t } = useTranslation()
  const [visible, setVisible] = useState(false)

  return (
    <div className="relative">
      <Input {...props} type={visible ? 'text' : 'password'} className="pe-10" />
      <button
        type="button"
        className="absolute inset-y-px end-px flex w-9 items-center justify-center rounded-e-md border-0 bg-transparent p-0 text-muted-foreground shadow-none hover:text-foreground focus-visible:outline-2 focus-visible:outline-ring"
        aria-label={visible ? t('Profile:HidePassword') : t('Profile:ShowPassword')}
        aria-pressed={visible}
        onClick={() => setVisible((v) => !v)}
      >
        {visible ? <EyeOffIcon className="size-4" aria-hidden /> : <EyeIcon className="size-4" aria-hidden />}
      </button>
    </div>
  )
}
