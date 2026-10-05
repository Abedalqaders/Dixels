import { useAuth } from 'react-oidc-context'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { getMyProfile } from '@/features/profile/api/profileApi'

/**
 * The signed-in person's profile, shared by the sidebar and My profile: one request however
 * many ask, and a save on My profile (which refreshes this key) updates the sidebar too.
 * Nothing is fetched without a token to send.
 */
export function useMyProfile() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  return useApiQuery(queryKeys.profile.me(), () => getMyProfile(token), { enabled: Boolean(token) })
}
