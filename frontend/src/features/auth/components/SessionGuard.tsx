import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { useAuth } from 'react-oidc-context'
import { setTokenRefresher, setUnauthorizedHandler } from '@/lib/api/httpClient'
import { AuthStatusScreen } from './AuthStatusScreen'

const STORAGE_KEY = 'dixels:forced-sign-outs'
/** Two forced sign-outs inside this window means signing in again won't help. */
const LOOP_WINDOW_MS = 60_000
const LOOP_LIMIT = 2

/**
 * Wires the API client's "401" handling to the OIDC session:
 *
 * 1. A 401 first tries a silent renew (the refresh token, through the hidden iframe or
 *    refresh-token grant the library uses) and the request is retried with the new token.
 *    An expired access token is the everyday cause and this makes it invisible.
 * 2. If that can't produce a token, or the fresh token is refused too, the session really is
 *    gone (revoked, account removed): the stored user is dropped, <RequireAuth> sees nobody
 *    signed in and sends the tab to the login page, coming back here afterwards.
 * 3. Sign-in → 401 → sign-in again is a loop, not a fix — a misconfigured audience, clock
 *    skew, or an API that no longer accepts this issuer. After two forced sign-outs in a
 *    minute the loop stops here with an explanation and a manual way to try again.
 */
export function SessionGuard({ children }: { children: ReactNode }) {
  const { removeUser, signinSilent, signinRedirect } = useAuth()
  const [broken, setBroken] = useState(false)

  useEffect(() => {
    // Several requests can answer 401 at the same moment; one renew serves all of them.
    let renewing: Promise<string | null> | null = null
    setTokenRefresher(() => {
      renewing ??= signinSilent()
        .then((user) => user?.access_token ?? null)
        .catch(() => null)
        .finally(() => {
          renewing = null
        })
      return renewing
    })

    let clearing = false
    setUnauthorizedHandler(() => {
      if (clearing) return
      clearing = true
      if (recordForcedSignOut() >= LOOP_LIMIT) {
        setBroken(true)
        return
      }
      void removeUser().finally(() => {
        clearing = false
      })
    })

    return () => {
      setTokenRefresher(undefined)
      setUnauthorizedHandler(undefined)
    }
  }, [removeUser, signinSilent])

  if (broken) {
    return (
      <AuthStatusScreen
        state="error"
        title="Signed in, but the server keeps refusing"
        detail="Your sign-in works, yet every request is answered with “not authorized”. Signing in again won't change that — it usually means the app and the API are configured for different servers. Tell an administrator; you can retry once that's fixed."
      >
        <button
          type="button"
          className="btn"
          onClick={() => {
            clearForcedSignOuts()
            setBroken(false)
            void removeUser().then(() => signinRedirect())
          }}
        >
          Try signing in again
        </button>
      </AuthStatusScreen>
    )
  }

  return <>{children}</>
}

/** Remembers this forced sign-out and returns how many happened in the last minute. */
function recordForcedSignOut(): number {
  const now = Date.now()
  let recent: number[] = []
  try {
    const stored = sessionStorage.getItem(STORAGE_KEY)
    recent = stored ? (JSON.parse(stored) as number[]).filter((t) => now - t < LOOP_WINDOW_MS) : []
    recent.push(now)
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(recent))
  } catch {
    // No session storage (private mode, blocked): count only this one.
    recent = [now]
  }
  return recent.length
}

function clearForcedSignOuts(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // Nothing stored, nothing to clear.
  }
}
