import { useAuth } from 'react-oidc-context'
import { getDisplayName } from '../auth/roles'
import { useAuthRole } from '../auth/useAuthRole'
import { CalendarIcon, ClockIcon } from '../components/icons'
import logo from '../assets/logo.png'
import '../styles/tokens.css'
import '../styles/base.css'
import '../styles/login.css'

// Root route. Also where signoutRedirect() sends the user back to, so it
// doubles as the "signed out" landing page from the mock.
//
// This is NOT a real login form - there's no email/password fields here.
// Credentials are handled entirely by the backend's own login page (see
// Step 2 in the plan): clicking "Sign in" just kicks off the OIDC redirect,
// same as clicking "Sign in with Google" anywhere else.
export function HomePage() {
  const auth = useAuth()
  const { landingPath } = useAuthRole()

  if (auth.isAuthenticated) {
    return (
      <div style={{ padding: 24 }}>
        <p>You're signed in as {getDisplayName(auth.user)}.</p>
        <a href={landingPath}>Go to your dashboard</a>
      </div>
    )
  }

  return (
    <div className="page">
      <div className="brandpanel">
        <div className="mark"><img className="logo-img" src={logo} alt="Dixels" /></div>
        <p className="tag">
          Find a desk, a focus pod or a meeting room across every building — and know it is really yours.
        </p>
        <div className="pts">
          <div className="pt">
            <CalendarIcon />
            Single and recurring bookings, validated one occurrence at a time
          </div>
          <div className="pt">
            <ClockIcon />
            Every time shown in the building's own timezone
          </div>
          <div className="pt">
            <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M10 2.5 16.5 5v5c0 4-3 6.3-6.5 7.5C6.5 16.3 3.5 14 3.5 10V5z" /><path d="M7.2 10.1 9.2 12l3.6-3.8" /></svg>
            No double bookings — a slot is yours the moment it commits
          </div>
        </div>
      </div>

      <div className="formwrap">
        <div className="logincard">
          <h1>Sign in</h1>
          <p className="loginsub">Use your Email to continue.</p>

          <button className="btn loginbtn" onClick={() => auth.signinRedirect()}>
            Sign in with your Email
          </button>

          <div className="foot">
            You'll be taken to your organization's sign-in page. Nothing is typed here.
          </div>
        </div>
      </div>
    </div>
  )
}
