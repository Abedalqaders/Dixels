import logo from '@/assets/logo.png'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/login.css'

interface AuthStatusScreenProps {
  /** busy: a spinner while a redirect or token exchange runs; done: a check once it has;
   * error: something went wrong and the user needs a way out. */
  state: 'busy' | 'done' | 'error'
  title: string
  detail?: string
  /** Shown under the text, e.g. a "Back to sign in" button on an error. */
  children?: React.ReactNode
}

/**
 * The full-screen card shown around signing in and out: every step of the OIDC round trip
 * (off to the login page, back with a code, signed in; signing out) says what's happening
 * instead of flashing a blank page or a bare line of text.
 */
export function AuthStatusScreen({ state, title, detail, children }: AuthStatusScreenProps) {
  return (
    <div className="authscreen">
      <div className="authcard" role={state === 'error' ? 'alert' : 'status'} aria-live="polite">
        <img className="authlogo" src={logo} alt="Dixels" />
        <div className={`authicon ${state}`} aria-hidden="true">
          {state === 'busy' && <span className="authspinner" />}
          {state === 'done' && (
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
              <path d="M5 12.5 10 17.5 19 7.5" />
            </svg>
          )}
          {state === 'error' && (
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round">
              <path d="M12 7v6" />
              <path d="M12 17h.01" />
            </svg>
          )}
        </div>
        <h1 className="authtitle">{title}</h1>
        {detail && <p className="authdetail">{detail}</p>}
        {children}
      </div>
    </div>
  )
}
