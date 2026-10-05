import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { useTranslation } from 'react-i18next'
import { ArrowRightIcon, CalendarDaysIcon, LockIcon, MailIcon, ShieldCheckIcon, SparklesIcon } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { HOME_PATH } from '@/features/auth/landing'
import { AuthStatusScreen } from '@/features/auth/components/AuthStatusScreen'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { ThemeToggle } from '@/components/ThemeToggle'
import type { TextKeys } from '@/i18n/keys'
import { Logo } from '@/components/Logo'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/login.css'
import { signInExtras } from '@/features/auth/signIn'

// The Dixels product family, in the groups dixels.ai shows them in. The names are brands,
// so they aren't translated. `live` marks the ones this deployment actually runs (this app
// is SpaceOS); flip a Dixel to live when it ships and its chip turns dark.
const DIXEL_GROUPS: { title: keyof TextKeys; dixels: { name: string; live?: boolean }[] }[] = [
  {
    title: 'SignIn:GroupSpaces',
    dixels: [{ name: 'SpaceOS', live: true }, { name: 'Atmosphere' }, { name: 'ParkFlow' }, { name: 'Pathfinder' }, { name: 'Flux' }],
  },
  {
    title: 'SignIn:GroupPeople',
    dixels: [{ name: 'VisitFlow' }, { name: 'Gather' }, { name: 'Tribes' }, { name: 'Ticksense' }],
  },
  {
    title: 'SignIn:GroupServices',
    dixels: [{ name: 'Nourish' }, { name: 'OmniServe' }, { name: 'Resolve' }, { name: 'TaskFlow' }, { name: 'LiveCanvas' }],
  },
]

// The four layers along the bottom of the brand panel, as on dixels.ai.
const FOUNDATION: { title: keyof TextKeys; detail: keyof TextKeys }[] = [
  { title: 'SignIn:FoundationPeople', detail: 'SignIn:FoundationPeopleDetail' },
  { title: 'SignIn:FoundationSpaces', detail: 'SignIn:FoundationSpacesDetail' },
  { title: 'SignIn:FoundationSystems', detail: 'SignIn:FoundationSystemsDetail' },
  { title: 'SignIn:FoundationExperiences', detail: 'SignIn:FoundationExperiencesDetail' },
]

// "What's new" under the sign-in card: the latest release, newest first. Update the items
// (and SignIn:WhatsNewWhen) with each release; the texts live in the language files.
const WHATS_NEW: { icon: LucideIcon; title: keyof TextKeys; detail: keyof TextKeys }[] = [
  { icon: ShieldCheckIcon, title: 'SignIn:NewSecurityTitle', detail: 'SignIn:NewSecurityDetail' },
  { icon: CalendarDaysIcon, title: 'SignIn:NewDayBarsTitle', detail: 'SignIn:NewDayBarsDetail' },
]

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
    auth.signinRedirect(signInExtras()).catch(() => setRedirecting(false))
  }

  return (
    <div className="page">
      <div className="brandpanel">
        <div className="mark"><Logo className="logo-img" /></div>

        <p className="eyebrow">{t('SignIn:Eyebrow')}</p>
        <h2 className="headline">{t('SignIn:Headline')}</h2>
        <p className="tag">{t('SignIn:Tagline')}</p>

        <section className="dixels" aria-labelledby="meet-dixels">
          <div className="dixelshead">
            <span id="meet-dixels" className="eyebrow">{t('SignIn:MeetTitle')}</span>
            <span className="dixelsnote">{t('SignIn:MeetNote')}</span>
          </div>
          {DIXEL_GROUPS.map((group) => (
            <div key={group.title} className="dixelgroup">
              <div className="dixelgrouptitle">{t(group.title)}</div>
              <ul className="chips">
                {group.dixels.map((dixel) => (
                  <li key={dixel.name} className={dixel.live ? 'chip live' : 'chip'}>
                    {dixel.live && <span className="livedot" aria-hidden="true" />}
                    {dixel.name}
                    {dixel.live && <span className="livetag">{t('SignIn:Live')}</span>}
                  </li>
                ))}
              </ul>
            </div>
          ))}
          <div className="cortex">
            <span className="cortexmark" aria-hidden="true"><SparklesIcon /></span>
            <b>Cortex</b>
            <span>{t('SignIn:CortexNote')}</span>
          </div>
        </section>

        <ol className="foundation" aria-label={t('SignIn:FoundationLabel')}>
          {FOUNDATION.map((layer, i) => (
            <li key={layer.title}>
              <span className="foundationnum">{String(i + 1).padStart(2, '0')}</span>
              <b>{t(layer.title)}</b>
              <span>{t(layer.detail)}</span>
            </li>
          ))}
        </ol>
      </div>

      <div className="formwrap">
        <div className="logintools">
          <LanguageSwitcher className="loginlang" />
          <ThemeToggle />
        </div>
        <div className="formcol">
          <div className="logincard">
            <p className="eyebrow">{t('SignIn:WelcomeBack')}</p>
            <h1>{t('SignIn:Title')}</h1>
            <p className="loginsub">{t('SignIn:Subtitle')}</p>

            <button className="btn loginbtn" onClick={signIn}>
              <MailIcon aria-hidden="true" />
              <span>{t('SignIn:Button')}</span>
              <ArrowRightIcon className="arrow" aria-hidden="true" />
            </button>

            <div className="foot">
              <LockIcon aria-hidden="true" />
              {t('SignIn:Foot')}
            </div>
          </div>

          <section className="whatsnew" aria-labelledby="whats-new">
            <div className="whatsnewhead">
              <span id="whats-new" className="eyebrow">{t('SignIn:WhatsNew')}</span>
              <span>{t('SignIn:WhatsNewWhen')}</span>
            </div>
            <ul>
              {WHATS_NEW.map(({ icon: Icon, title, detail }) => (
                <li key={title}>
                  <span className="whatsnewicon" aria-hidden="true"><Icon /></span>
                  <span>
                    <b>{t(title)}</b>
                    <span>{t(detail)}</span>
                  </span>
                </li>
              ))}
            </ul>
          </section>

          <p className="loginhelp">
            {t('SignIn:HelpPrompt')} <b>{t('SignIn:HelpAction')}</b>
          </p>
        </div>

        <footer className="loginfooter">© {new Date().getFullYear()} Dixels</footer>
      </div>
    </div>
  )
}
