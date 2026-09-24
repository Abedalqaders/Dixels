import type { ReactNode } from 'react'
import { AuthProvider as OidcAuthProvider } from 'react-oidc-context'
import type { AuthProviderProps } from 'react-oidc-context'

// This config describes our OpenIddict client ("Dixels_App") to the OIDC
// library. It mirrors exactly what we registered on the backend in
// OpenIddictDataSeedContributor.cs. Overridable per environment via
// VITE_OIDC_* (see .env.example) — the fallbacks below are the local dev
// values, so `npm run dev` still works with no .env file present.
const oidcConfig: AuthProviderProps = {
  authority: import.meta.env.VITE_OIDC_AUTHORITY ?? 'https://localhost:44334',
  client_id: import.meta.env.VITE_OIDC_CLIENT_ID ?? 'Dixels_App',
  redirect_uri: import.meta.env.VITE_OIDC_REDIRECT_URI ?? 'http://localhost:5173/callback',
  post_logout_redirect_uri: import.meta.env.VITE_OIDC_POST_LOGOUT_REDIRECT_URI ?? 'http://localhost:5173/',
  response_type: 'code', // Authorization Code flow (the library adds PKCE automatically)
  scope: import.meta.env.VITE_OIDC_SCOPE ?? 'openid profile email roles Dixels offline_access',
  automaticSilentRenew: true,
  // After the library exchanges the ?code=...&state=... for a token, strip
  // those params back out of the URL bar so a refresh doesn't replay them.
  onSigninCallback: () => {
    window.history.replaceState({}, document.title, window.location.pathname)
  },
}

export function AuthProvider({ children }: { children: ReactNode }) {
  return <OidcAuthProvider {...oidcConfig}>{children}</OidcAuthProvider>
}
