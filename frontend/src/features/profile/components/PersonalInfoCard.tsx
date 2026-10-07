import { useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { useBlocker } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ConfirmDialog } from '@/components/ConfirmDialog'
import { useFieldErrors } from '@/components/FieldError'
import { useUnsavedChangesWarning } from '@/hooks/useUnsavedChangesWarning'
import { ApiError, MAX_NAME_LENGTH, MAX_PHONE_LENGTH, updateMyProfile } from '@/features/profile/api/profileApi'
import type { ProfileDto } from '@/features/profile/api/profileApi'
import { ProfileSection } from './ProfileSection'

interface PersonalInfoCardProps {
  profile: ProfileDto
  token: string
  onSaved: (saved: ProfileDto) => void
  onError: (message: string) => void
}

type Field = 'name' | 'surname' | 'phone'

/** Digits, spaces, and the + ( ) - people write phone numbers with. */
const PHONE_PATTERN = /^[+\d\s()-]*$/

/**
 * My profile's details, edited where they're shown: first name, last name and phone are
 * boxes to type in; Save changes and Cancel wake up once something has changed. Username and
 * email are shown greyed out — only an admin changes those.
 *
 * The page keys this card by the profile's concurrency stamp, so after a save (or a reload)
 * the boxes start again from what's stored.
 */
export function PersonalInfoCard({ profile, token, onSaved, onError }: PersonalInfoCardProps) {
  const { t } = useTranslation()
  const [name, setName] = useState(profile.name ?? '')
  const [surname, setSurname] = useState(profile.surname ?? '')
  const [phone, setPhone] = useState(profile.phoneNumber ?? '')
  const [saving, setSaving] = useState(false)
  const f = useFieldErrors<Field>('profile')

  const isDirty =
    name.trim() !== (profile.name ?? '') || surname.trim() !== (profile.surname ?? '') || phone.trim() !== (profile.phoneNumber ?? '')

  useUnsavedChangesWarning(isDirty)
  // Leaving for another page with changes unsaved: ask first.
  const blocker = useBlocker(({ currentLocation, nextLocation }) => isDirty && currentLocation.pathname !== nextLocation.pathname)

  function reset() {
    setName(profile.name ?? '')
    setSurname(profile.surname ?? '')
    setPhone(profile.phoneNumber ?? '')
    f.clear()
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!PHONE_PATTERN.test(phone.trim())) {
      f.setErrors({ phone: t('Profile:PhoneInvalid') })
      return
    }
    f.clear()

    setSaving(true)
    try {
      const saved = await updateMyProfile(token, {
        userName: profile.userName,
        email: profile.email,
        name: name.trim() || null,
        surname: surname.trim() || null,
        phoneNumber: phone.trim() || null,
        concurrencyStamp: profile.concurrencyStamp,
      })
      onSaved(saved)
    } catch (err) {
      onError(err instanceof ApiError ? err.message : t('Error:Generic'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form {...f.form} onSubmit={handleSubmit} noValidate>
      <ProfileSection
        title={t('Profile:PersonalInfo')}
        description={t('Profile:PersonalInfoLead')}
        footer={
          <>
            <Button type="button" variant="outline" onClick={reset} disabled={!isDirty || saving}>
              {t('Common:Cancel')}
            </Button>
            <Button type="submit" disabled={!isDirty || saving}>
              {saving ? t('Common:Saving') : t('Profile:SaveChanges')}
            </Button>
          </>
        }
      >
        <div className="grid gap-x-4 gap-y-[18px] sm:grid-cols-2">
          <Box label={t('Profile:FirstName')} id={f.id('name')} error={f.error('name')}>
            <Input {...f.field('name')} autoComplete="given-name" maxLength={MAX_NAME_LENGTH} value={name} onChange={(e) => setName(e.target.value)} />
          </Box>
          <Box label={t('Profile:LastName')} id={f.id('surname')} error={f.error('surname')}>
            <Input {...f.field('surname')} autoComplete="family-name" maxLength={MAX_NAME_LENGTH} value={surname} onChange={(e) => setSurname(e.target.value)} />
          </Box>
          <Box label={t('Profile:Phone')} id={f.id('phone')} error={f.error('phone')}>
            <Input
              {...f.field('phone')}
              type="tel"
              dir="ltr"
              autoComplete="tel"
              inputMode="tel"
              maxLength={MAX_PHONE_LENGTH}
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              className="text-start rtl:text-end"
            />
          </Box>
          <Box label={t('Profile:Email')} id="profile-email">
            <Input id="profile-email" dir="ltr" value={profile.email} readOnly disabled className="text-start rtl:text-end" />
          </Box>
          <Box label={t('Profile:UserName')} id="profile-username">
            <Input id="profile-username" dir="ltr" value={profile.userName} readOnly disabled className="text-start rtl:text-end" />
          </Box>
          <p className="self-end text-sm text-muted-foreground sm:mb-2.5">{t('Profile:SignInDetailsNote')}</p>
        </div>
      </ProfileSection>

      {blocker.state === 'blocked' && (
        <ConfirmDialog
          title={t('Rules:DiscardTitle')}
          description={t('Profile:DiscardDetail')}
          confirmLabel={t('Rules:DiscardConfirm')}
          cancelLabel={t('Rules:KeepEditing')}
          destructive
          onAnswer={(discard) => (discard ? blocker.proceed() : blocker.reset())}
        />
      )}
    </form>
  )
}

/** A box under its label, with its message (if any) under it. */
function Box({ label, id, error, children }: { label: string; id: string; error?: ReactNode; children: ReactNode }) {
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error}
    </div>
  )
}
