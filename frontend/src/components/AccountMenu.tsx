import { useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { useTranslation } from 'react-i18next'
import { ChevronsUpDownIcon, UserRoundIcon } from 'lucide-react'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { getDisplayName } from '@/features/auth/roles'
import { useAuthRole } from '@/features/auth/hooks/useAuthRole'
import { fullNameOf } from '@/features/profile/api/profileApi'
import { useMyProfile } from '@/features/profile/hooks/useMyProfile'
import { useMyPictureUrl } from '@/features/profile/hooks/useMyPicture'
import { SignOutIcon } from './icons'
import { UserAvatar } from './UserAvatar'

/**
 * Who's signed in, at the foot of the sidebar: their picture (or initials), name and role. A click opens
 * My profile and Sign out. The name is the saved profile's, so a change on My profile shows
 * here at once; until it has loaded, the sign-in token's stands in.
 */
export function AccountMenu() {
  const { t } = useTranslation()
  const auth = useAuth()
  const navigate = useNavigate()
  const { isAdmin } = useAuthRole()
  const { data: profile } = useMyProfile()
  const pictureUrl = useMyPictureUrl()
  const displayName = profile ? fullNameOf(profile) : getDisplayName(auth.user)

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button type="button" className="acct" aria-label={t('Account:MenuLabel', { name: displayName })}>
          <UserAvatar name={displayName} pictureUrl={pictureUrl} />
          <span className="acctwho">
            <span className="nm">{displayName}</span>
            <span className="rl">{isAdmin ? t('Nav:RoleAdministrator') : t('Nav:RoleEmployee')}</span>
          </span>
          <ChevronsUpDownIcon className="acctchev" aria-hidden />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent side="top" align="start" className="w-(--radix-dropdown-menu-trigger-width) min-w-56">
        <DropdownMenuItem onSelect={() => navigate('/profile')}>
          <UserRoundIcon aria-hidden />
          {t('Account:MyProfile')}
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => navigate('/signing-out')}>
          <SignOutIcon />
          {t('Nav:SignOut')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
