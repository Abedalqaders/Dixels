import { useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useFieldErrors } from '@/components/FieldError'
import { ApiError, changeMyPassword, MAX_PASSWORD_LENGTH } from '@/features/profile/api/profileApi'
import { ProfileSection } from './ProfileSection'

interface PasswordCardProps {
  /** Whether the account has a password already — if not, there's no current one to ask for. */
  hasPassword: boolean
  token: string
  onChanged: () => void
}

type Field = 'current' | 'new' | 'confirm'

/** DixelsDomainErrorCodes.WrongCurrentPassword (DixelsProfileAppService): the current password is wrong. */
const WRONG_CURRENT_PASSWORD = 'Dixels:Users:WrongCurrentPassword'

/**
 * My profile's password change, right on the card. The rules a new password must meet
 * (length, digits, …) are ABP Identity's settings and only the server checks them, so its
 * message — in the reader's language — goes under the new password, and a wrong current one
 * under that box.
 */
export function PasswordCard({ hasPassword, token, onChanged }: PasswordCardProps) {
  const { t } = useTranslation()
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [saving, setSaving] = useState(false)
  const f = useFieldErrors<Field>('password')

  function validate(): Partial<Record<Field, string>> {
    const errors: Partial<Record<Field, string>> = {}
    if (hasPassword && !current) errors.current = t('Profile:CurrentPasswordRequired')
    if (!next) errors.new = t('Profile:NewPasswordRequired')
    else if (confirm !== next) errors.confirm = t('Profile:PasswordsDontMatch')
    return errors
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    const errors = validate()
    if (Object.keys(errors).length > 0) {
      f.setErrors(errors)
      return
    }
    f.clear()

    setSaving(true)
    try {
      await changeMyPassword(token, { currentPassword: hasPassword ? current : undefined, newPassword: next })
      setCurrent('')
      setNext('')
      setConfirm('')
      onChanged()
    } catch (err) {
      const message = err instanceof ApiError ? err.message : t('Error:Generic')
      f.setErrors(err instanceof ApiError && err.code === WRONG_CURRENT_PASSWORD ? { current: message } : { new: message })
    } finally {
      setSaving(false)
    }
  }

  const passwordBox = (field: Field, label: string, value: string, onChange: (value: string) => void, autoComplete: string) => (
    <div className="grid gap-1.5">
      <Label htmlFor={f.id(field)}>{label}</Label>
      <Input
        {...f.field(field)}
        type="password"
        autoComplete={autoComplete}
        maxLength={MAX_PASSWORD_LENGTH}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
      {f.error(field)}
    </div>
  )

  return (
    <form {...f.form} onSubmit={handleSubmit} noValidate>
      <ProfileSection
        title={t('Profile:Password')}
        description={hasPassword ? t('Profile:PasswordLead') : t('Profile:NoPasswordLead')}
        footer={
          <Button type="submit" variant="outline" disabled={saving || (!current && !next && !confirm)}>
            {saving ? t('Common:Saving') : hasPassword ? t('Profile:ChangePassword') : t('Profile:SetPassword')}
          </Button>
        }
      >
        <div className="grid gap-x-4 gap-y-[18px] sm:grid-cols-2">
          {hasPassword && (
            <>
              {passwordBox('current', t('Profile:CurrentPassword'), current, setCurrent, 'current-password')}
              {/* Keeps the two new-password boxes side by side on the next line. */}
              <div className="hidden sm:block" />
            </>
          )}
          {passwordBox('new', t('Profile:NewPassword'), next, setNext, 'new-password')}
          {passwordBox('confirm', t('Profile:ConfirmPassword'), confirm, setConfirm, 'new-password')}
        </div>
      </ProfileSection>
    </form>
  )
}
