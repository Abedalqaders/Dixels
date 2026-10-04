import { useAuth } from 'react-oidc-context'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { Card, CardContent } from '@/components/ui/card'
import { TopBar } from '@/components/TopBar'
import { FormSkeleton } from '@/components/LoadingSkeletons'
import { useToast } from '@/components/Toast'
import { useAuthRole } from '@/features/auth/hooks/useAuthRole'
import { queryKeys } from '@/lib/api/queryKeys'
import { fullNameOf } from '@/features/profile/api/profileApi'
import type { ProfileDto } from '@/features/profile/api/profileApi'
import { useMyProfile } from '@/features/profile/hooks/useMyProfile'
import { PersonalInfoCard } from '@/features/profile/components/PersonalInfoCard'
import { PasswordCard } from '@/features/profile/components/PasswordCard'
import { ProfilePhoto } from '@/features/profile/components/ProfilePhoto'

/**
 * My profile: who you are in Dixels. Anyone signed in can open it (from the account menu at
 * the foot of the sidebar), set their picture, change their own name and phone, and their password —
 * all typed straight into the page. Username and email are shown but only an admin changes them.
 */
export function ProfilePage() {
  const { t } = useTranslation()
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { isAdmin } = useAuthRole()
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const { status, data: profile, error } = useMyProfile()

  function handleSaved(saved: ProfileDto) {
    // What the save returned is the profile now: the sidebar's name changes with it.
    queryClient.setQueryData(queryKeys.profile.me(), saved)
    showToast(t('Profile:Saved'))
  }

  function handleError(message: string) {
    showToast(message, 'error')
    // Most likely changed elsewhere since it loaded (a stale concurrency stamp): reload it.
    void queryClient.invalidateQueries({ queryKey: queryKeys.profile.me() })
  }

  function handlePasswordChanged() {
    showToast(t('Profile:PasswordChanged'))
    // An account that had no password now has one.
    if (profile && !profile.hasPassword) void queryClient.invalidateQueries({ queryKey: queryKeys.profile.me() })
  }

  return (
    <>
      <TopBar crumbs={[{ label: t('Account:MyProfile') }]} />
      <div className="content">
        <div className="mx-auto flex w-full max-w-[760px] flex-col gap-5">
          <div>
            <h1 className="pagetitle">{t('Account:MyProfile')}</h1>
            <p className="lead">{t('Profile:Lead')}</p>
          </div>

          {status === 'loading' && <FormSkeleton label={t('Profile:Loading')} />}
          {status === 'error' && <p className="treeempty">{t('Profile:LoadFailed', { error: error.message })}</p>}

          {status === 'success' && (
            <>
              <Card className="rounded-2xl py-0 shadow-md">
                <CardContent className="flex items-center gap-5 p-6">
                  <ProfilePhoto name={fullNameOf(profile)} token={token} />
                  <div className="flex min-w-0 flex-col gap-1">
                    <p className="truncate text-xl font-semibold">{fullNameOf(profile)}</p>
                    <p className="truncate text-sm text-muted-foreground">
                      <span dir="ltr">{profile.email}</span>
                    </p>
                    <span className="mt-1 self-start rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-semibold text-primary">
                      {isAdmin ? t('Nav:RoleAdministrator') : t('Nav:RoleEmployee')}
                    </span>
                  </div>
                </CardContent>
              </Card>

              {/* Keyed by the stamp: after a save or a reload, the boxes start from what's stored. */}
              <PersonalInfoCard key={profile.concurrencyStamp} profile={profile} token={token} onSaved={handleSaved} onError={handleError} />
              <PasswordCard hasPassword={profile.hasPassword} token={token} onChanged={handlePasswordChanged} />
            </>
          )}
        </div>
      </div>
    </>
  )
}
