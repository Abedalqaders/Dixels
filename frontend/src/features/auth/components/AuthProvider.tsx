import type { ReactNode } from 'react'
import { AuthProvider as OidcAuthProvider } from 'react-oidc-context'
import { userManager } from '@/features/auth/userManager'
import { SessionGuard } from './SessionGuard'
import { PermissionsProvider } from '@/features/auth/permissions/PermissionsProvider'
import { LanguageSync } from '@/features/auth/language/LanguageSync'

// After the library exchanges the ?code=...&state=... for a token, strip
// those params back out of the URL bar so a refresh doesn't replay them.
function onSigninCallback() {
  window.history.replaceState({}, document.title, window.location.pathname)
}

export function AuthProvider({ children }: { children: ReactNode }) {
  return (
    <OidcAuthProvider userManager={userManager} onSigninCallback={onSigninCallback}>
      <SessionGuard>
        <PermissionsProvider>
          <LanguageSync />
          {children}
        </PermissionsProvider>
      </SessionGuard>
    </OidcAuthProvider>
  )
}
