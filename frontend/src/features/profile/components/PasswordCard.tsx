import { useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { FieldError } from '@/components/FieldError'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { ApiError, changeMyPassword, MAX_PASSWORD_LENGTH } from '@/features/profile/api/profileApi'
import { getPasswordRules, unmetRules } from '@/features/profile/passwordRules'
import { PasswordChecklist } from './PasswordChecklist'
import { PasswordInput } from './PasswordInput'
import { ProfileSection } from './ProfileSection'

interface PasswordCardProps {
  token: string
  onChanged: () => void
}

type Field = 'current' | 'new' | 'confirm'

/** DixelsDomainErrorCodes.WrongCurrentPassword (DixelsProfileAppService): the current password is wrong. */
const WRONG_CURRENT_PASSWORD = 'Dixels:Users:WrongCurrentPassword'

const FIELDS: Field[] = ['current', 'new', 'confirm']
const boxId = (field: Field) => `password-${field}`

/**
 * Changing your password, on My profile's Security tab. Each box says what's wrong right
 * under it, Facebook style: not while you're still typing, but once you leave the box (or
 * press Change), and the message goes as soon as it's fixed. Under the new password a
 * checklist of the admin's password rules ticks off as you type.
 *
 * The server stays the judge. Its answer — a wrong current password, or a rule the checklist
 * didn't know about — goes under the box it's about, and goes once that box is changed.
 */
export function PasswordCard({ token, onChanged }: PasswordCardProps) {
  const { t } = useTranslation()
  const [values, setValues] = useState<Record<Field, string>>({ current: '', new: '', confirm: '' })
  const [left, setLeft] = useState<Partial<Record<Field, boolean>>>({})
  const [submitted, setSubmitted] = useState(false)
  const [serverErrors, setServerErrors] = useState<Partial<Record<Field, string>>>({})
  const [saving, setSaving] = useState(false)
  // Without the rules (still loading, or the request failed) there's no checklist and only
  // the server checks them.
  const rules = useApiQuery(queryKeys.profile.passwordRules(), () => getPasswordRules(token), { enabled: Boolean(token) })
  const unmet = rules.status === 'success' ? unmetRules(values.new, rules.data) : []

  const shown = (field: Field) => submitted || Boolean(left[field])

  // What's wrong with each box as it stands, whether or not it's shown yet.
  const problems: Record<Field, string | undefined> = {
    current: !values.current ? t('Profile:CurrentPasswordRequired') : undefined,
    new: !values.new ? t('Profile:NewPasswordRequired') : unmet.length > 0 ? t('Profile:PasswordRulesUnmet') : undefined,
    confirm: !values.confirm
      ? t('Profile:ConfirmPasswordRequired')
      : values.confirm !== values.new
        ? t('Profile:PasswordsDontMatch')
        : undefined,
  }

  function errorFor(field: Field): string | undefined {
    if (serverErrors[field]) return serverErrors[field]
    if (!shown(field)) return undefined
    // Leaving the confirm box before typing a new password isn't a mistake yet.
    if (field === 'confirm' && !submitted && !values.new && !values.confirm) return undefined
    return problems[field]
  }

  function change(field: Field, value: string) {
    setValues((v) => ({ ...v, [field]: value }))
    // The server's word was about what was typed before.
    if (serverErrors[field]) setServerErrors((e) => ({ ...e, [field]: undefined }))
  }

  function focusFirst(has: (field: Field) => boolean) {
    const first = FIELDS.find(has)
    if (first) document.getElementById(boxId(first))?.focus()
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setSubmitted(true)
    if (FIELDS.some((field) => problems[field])) {
      focusFirst((field) => Boolean(problems[field]))
      return
    }

    setSaving(true)
    try {
      await changeMyPassword(token, { currentPassword: values.current, newPassword: values.new })
      setValues({ current: '', new: '', confirm: '' })
      setLeft({})
      setSubmitted(false)
      setServerErrors({})
      onChanged()
    } catch (err) {
      const message = err instanceof ApiError ? err.message : t('Error:Generic')
      const field: Field = err instanceof ApiError && err.code === WRONG_CURRENT_PASSWORD ? 'current' : 'new'
      setServerErrors({ [field]: message })
      focusFirst((f) => f === field)
    } finally {
      setSaving(false)
    }
  }

  function passwordBox(field: Field, label: string, autoComplete: string) {
    const error = errorFor(field)
    const checklist = field === 'new' && rules.status === 'success'
    const describedBy = [error && `${boxId(field)}-error`, checklist && 'password-rules'].filter(Boolean).join(' ') || undefined
    return (
      <div className="grid gap-1.5">
        <Label htmlFor={boxId(field)}>{label}</Label>
        <PasswordInput
          id={boxId(field)}
          autoComplete={autoComplete}
          maxLength={MAX_PASSWORD_LENGTH}
          value={values[field]}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          onChange={(e) => change(field, e.target.value)}
          onBlur={() => setLeft((l) => ({ ...l, [field]: true }))}
        />
        {error && <FieldError id={`${boxId(field)}-error`} message={error} />}
        {checklist && (
          <PasswordChecklist id="password-rules" rules={rules.data} unmet={unmet} showMissing={shown('new') && values.new !== ''} />
        )}
      </div>
    )
  }

  const empty = !values.current && !values.new && !values.confirm

  return (
    <form onSubmit={handleSubmit} noValidate>
      <ProfileSection
        title={t('Profile:ChangePassword')}
        description={t('Profile:PasswordLead')}
        footer={
          <Button type="submit" disabled={saving || empty}>
            {saving ? t('Common:Saving') : t('Profile:ChangePassword')}
          </Button>
        }
      >
        <div className="grid max-w-md gap-y-[18px]">
          {passwordBox('current', t('Profile:CurrentPassword'), 'current-password')}
          {passwordBox('new', t('Profile:NewPassword'), 'new-password')}
          {passwordBox('confirm', t('Profile:ConfirmPassword'), 'new-password')}
        </div>
      </ProfileSection>
    </form>
  )
}
