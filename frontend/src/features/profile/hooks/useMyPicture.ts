import { useEffect, useMemo } from 'react'
import { useAuth } from 'react-oidc-context'
import { useApiQuery } from '@/hooks/useApiQuery'
import { queryKeys } from '@/lib/api/queryKeys'
import { getMyPicture } from '@/features/profile/api/profileApi'

/**
 * The signed-in person's picture as an address an <img> can show, or null when they have
 * none (or it hasn't loaded yet) — then their initials stand in. Shared by the sidebar and
 * My profile through the query cache, so a new picture shows in both at once.
 */
export function useMyPictureUrl(): string | null {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { data } = useApiQuery(queryKeys.profile.picture(), () => getMyPicture(token), { enabled: Boolean(token) })

  // The picture's bytes live in the browser; the address points at them until it's let go.
  const url = useMemo(() => (data ? URL.createObjectURL(data) : null), [data])
  useEffect(() => {
    return () => {
      if (url) URL.revokeObjectURL(url)
    }
  }, [url])

  return url
}
