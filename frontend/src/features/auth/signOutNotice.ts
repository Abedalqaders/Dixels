// The trip through the backend's logout page is a full-page redirect, so "you just signed
// out" has to survive it. sessionStorage does: it's per-tab and gone once the tab closes.
// (A query string on post_logout_redirect_uri won't do — OpenIddict matches it exactly.)
const KEY = 'dixels.signedOut'

export function markSignedOut(): void {
  try {
    globalThis.sessionStorage?.setItem(KEY, '1')
  } catch {
    // Storage blocked: the sign-out still works, there's just no notice afterwards.
  }
}

export function wasJustSignedOut(): boolean {
  try {
    return globalThis.sessionStorage?.getItem(KEY) === '1'
  } catch {
    return false
  }
}

export function clearSignedOut(): void {
  try {
    globalThis.sessionStorage?.removeItem(KEY)
  } catch {
    // ignore
  }
}
