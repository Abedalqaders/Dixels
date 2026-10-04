import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { useTranslation } from 'react-i18next'
import { HOME_PATH } from '@/features/auth/landing'
import { AuthStatusScreen } from '@/features/auth/components/AuthStatusScreen'
import { CalendarIcon, ClockIcon } from '@/components/icons'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import logo from '@/assets/logo.png'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/login.css'

// Root route. Also where signoutRedirect() sends the user back to, so it
// doubles as the "signed out" landing page from the mock.
//
// This is NOT a real login form - there's no email/password fields here.
// Credentials are handled entirely by the backend's own login page (see
// Step 2 in the plan): clicking "Sign in" just kicks off the OIDC redirect,
// same as clicking "Sign in with Google" anywhere else.
export function HomePage() {
  const { t } = useTranslation()
  const auth = useAuth()
  const [redirecting, setRedirecting] = useState(false)

  // Already signed in (e.g. opened the site again): go straight to their home page.
  if (auth.isAuthenticated) {
    return <Navigate to={HOME_PATH} replace />
  }

  if (redirecting) {
    return <AuthStatusScreen state="busy" title={t('Auth:Redirecting')} detail={t('Auth:RedirectingDetail')} />
  }

  function signIn() {
    setRedirecting(true)
    auth.signinRedirect().catch(() => setRedirecting(false))
  }

  return (
    <div className="page">
      <div className="brandpanel">
        <div className="mark"><img className="logo-img" src={logo} alt="Dixels" /></div>
        <p className="tag">{t('SignIn:Tagline')}</p>
        <div className="pts">
          <div className="pt">
            <CalendarIcon />
            {t('SignIn:PointRecurring')}
          </div>
          <div className="pt">
            <ClockIcon />
            {t('SignIn:PointTimezone')}
          </div>
          <div className="pt">
            <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M10 2.5 16.5 5v5c0 4-3 6.3-6.5 7.5C6.5 16.3 3.5 14 3.5 10V5z" /><path d="M7.2 10.1 9.2 12l3.6-3.8" /></svg>
            {t('SignIn:PointNoDoubleBooking')}
          </div>
        </div>
      </div>

      <div className="formwrap">
        <LanguageSwitcher className="loginlang" />
        <div className="logincard">
          <h1>{t('SignIn:Title')}</h1>
          <p className="loginsub">{t('SignIn:Subtitle')}</p>

          <button className="btn loginbtn" onClick={signIn}>
            {t('SignIn:Button')}
          </button>

          <div className="foot">{t('SignIn:Foot')}</div>
        </div>
      </div>
    </div>
  )
}
