import { useTranslation } from 'react-i18next'
import { CheckIcon, CircleIcon, XIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { activeRules } from '@/features/profile/passwordRules'
import type { PasswordRule, PasswordRules } from '@/features/profile/passwordRules'

interface PasswordChecklistProps {
  id: string
  rules: PasswordRules
  unmet: PasswordRule[]
  /** Whether to show what's missing in red: once the box has been left or the form sent. */
  showMissing: boolean
}

/**
 * Under the new password, what it still needs: each rule ticks green as it's met. Before
 * the box is left a missing rule is a plain grey dot; after, a red cross. The new password
 * box points at this list (aria-describedby), so a screen reader reads it on focus rather
 * than on every keystroke.
 */
export function PasswordChecklist({ id, rules, unmet, showMissing }: PasswordChecklistProps) {
  const { t } = useTranslation()

  const label: Record<PasswordRule, string> = {
    length: t('Profile:Rule:Length', { count: rules.requiredLength }),
    uppercase: t('Profile:Rule:Uppercase'),
    lowercase: t('Profile:Rule:Lowercase'),
    digit: t('Profile:Rule:Digit'),
    symbol: t('Profile:Rule:Symbol'),
    uniqueChars: t('Profile:Rule:UniqueChars', { count: rules.requiredUniqueChars }),
  }

  return (
    <div id={id} className="mt-1.5 grid gap-1 text-sm">
      <p className="text-muted-foreground">{t('Profile:PasswordNeeds')}</p>
      <ul className="grid gap-1">
        {activeRules(rules).map((rule) => {
          const met = !unmet.includes(rule)
          const missing = !met && showMissing
          return (
            <li
              key={rule}
              className={cn(
                'flex items-center gap-2',
                met ? 'text-[var(--state-confirmed-ink)]' : missing ? 'text-destructive' : 'text-muted-foreground',
              )}
            >
              {met ? (
                <CheckIcon className="size-4 flex-none" aria-hidden />
              ) : missing ? (
                <XIcon className="size-4 flex-none" aria-hidden />
              ) : (
                <CircleIcon className="size-2 flex-none mx-1" aria-hidden />
              )}
              <span>{label[rule]}</span>
              <span className="sr-only">{met ? t('Profile:RuleMet') : t('Profile:RuleNotMet')}</span>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
