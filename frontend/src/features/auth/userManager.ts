import { UserManager } from 'oidc-client-ts'

// This config describes our OpenIddict client ("Dixels_App") to the OIDC
// library. It mirrors exactly what we registered on the backend in
// OpenIddictDataSeedContributor.cs. Overridable per environment via
// VITE_OIDC_* (see .env.example) — the fallbacks below are the local dev
// values, so `npm run dev` still works with no .env file present.
// The redirect URIs default to the address the app was opened on: the sign-in
// state is kept in that origin's storage, so returning to a different host
// (localhost vs. the LAN IP) would fail with "No matching state found".
//
// One instance for the app, made here rather than inside <AuthProvider>: a route loader
// runs outside React and reads the signed-in user from it too (see myBuildingLoader).
export const userManager = new UserManager({
  authority: import.meta.env.VITE_OIDC_AUTHORITY ?? 'https://localhost:44334',
  client_id: import.meta.env.VITE_OIDC_CLIENT_ID ?? 'Dixels_App',
  redirect_uri: import.meta.env.VITE_OIDC_REDIRECT_URI ?? `${window.location.origin}/callback`,
  post_logout_redirect_uri: import.meta.env.VITE_OIDC_POST_LOGOUT_REDIRECT_URI ?? `${window.location.origin}/`,
  response_type: 'code', // Authorization Code flow (the library adds PKCE automatically)
  // PKCE needs crypto.subtle, which browsers only offer over https (or on localhost).
  // VITE_OIDC_DISABLE_PKCE=true is solely for a LAN test server on plain http — it
  // removes the protection against a stolen authorization code being redeemed.
  disablePKCE: import.meta.env.VITE_OIDC_DISABLE_PKCE === 'true',
  scope: import.meta.env.VITE_OIDC_SCOPE ?? 'openid profile email roles Dixels offline_access',
  automaticSilentRenew: true,
})
