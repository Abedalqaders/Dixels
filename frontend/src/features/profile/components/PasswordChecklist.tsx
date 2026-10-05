import { useTranslation } from 'react-i18next'
import { CircleCheckIcon, CircleIcon, CircleXIcon } from 'lucide-react'
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

type Strength = 'Weak' | 'Fair' | 'Good' | 'Strong'

/** How far along the rules the password is: all met is Strong, most Good, about half Fair. */
function strengthOf(met: number, total: number): Strength | null {
  if (met === 0) return null
  const share = met / total
  if (share === 1) return 'Strong'
  if (share > 0.6) return 'Good'
  if (share >= 0.5) return 'Fair'
  return 'Weak'
}

/**
 * Under the new password, a soft panel with what it still needs: a bar with one step per
 * rule, filled as rules are met, with a word for how far along it is, and the rules
 * themselves in two columns — a purple tick when met, an empty circle while not, a red
 * cross once the box has been left with it still missing. The new password box points at
 * this panel (aria-describedby), so a screen reader reads it on focus rather than on every
 * keystroke.
 */
export function PasswordChecklist({ id, rules, unmet, showMissing }: PasswordChecklistProps) {
  const { t } = useTranslation()
  const active = activeRules(rules)
  if (active.length === 0) return null

  const met = active.filter((rule) => !unmet.includes(rule)).length
  const strength = strengthOf(met, active.length)

  const label: Record<PasswordRule, string> = {
    length: t('Profile:Rule:Length', { count: rules.requiredLength }),
    uppercase: t('Profile:Rule:Uppercase'),
    lowercase: t('Profile:Rule:Lowercase'),
    digit: t('Profile:Rule:Digit'),
    symbol: t('Profile:Rule:Symbol'),
    uniqueChars: t('Profile:Rule:UniqueChars', { count: rules.requiredUniqueChars }),
  }

  return (
    <div id={id} className="mt-1.5 grid gap-2.5 rounded-lg bg-[var(--accent-soft)] p-3 text-[13px]">
      <div className="flex items-center justify-between gap-3">
        <p className="text-muted-foreground">{t('Profile:PasswordNeeds')}</p>
        {strength && <p className="text-xs font-semibold text-primary">{t(`Profile:Strength:${strength}`)}</p>}
      </div>
      <div className="flex gap-1" aria-hidden>
        {active.map((rule, i) => (
          <span key={rule} className={cn('h-1 flex-1 rounded-full', i < met ? 'bg-primary' : 'bg-primary/15')} />
        ))}
      </div>
      <ul className="m-0 grid list-none gap-x-4 gap-y-1.5 p-0 sm:grid-cols-2">
        {active.map((rule) => {
          const ok = !unmet.includes(rule)
          const missing = !ok && showMissing
          return (
            <li
              key={rule}
              className={cn(
                'flex items-center gap-2',
                ok ? 'font-medium text-foreground' : missing ? 'text-destructive' : 'text-muted-foreground',
              )}
            >
              {ok ? (
                <CircleCheckIcon className="size-4 flex-none text-primary" aria-hidden />
              ) : missing ? (
                <CircleXIcon className="size-4 flex-none" aria-hidden />
              ) : (
                <CircleIcon className="size-4 flex-none" aria-hidden />
              )}
              <span>{label[rule]}</span>
              <span className="sr-only">{ok ? t('Profile:RuleMet') : t('Profile:RuleNotMet')}</span>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
