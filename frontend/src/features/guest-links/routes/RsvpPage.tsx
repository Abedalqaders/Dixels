import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams, useSearchParams } from 'react-router-dom'
import { Logo } from '@/components/Logo'
import { LanguageSwitcher } from '@/components/LanguageSwitcher'
import { InviteeResponseStatus } from '@/features/bookings/api/bookingsApi'
import { bookingTitle } from '@/features/bookings/format'
import { describeRecurrence } from '@/features/bookings/recurrence'
import { availableLanguages, currentLanguage, setLanguage } from '@/i18n'
import { ApiError } from '@/lib/api/httpClient'
import { dateOf, timeOf } from '@/lib/time/buildingTime'
import { formatClockRange, formatDay } from '@/lib/time/format'
import { answerInvitation, lookupInvitation } from '@/features/guest-links/api'
import type { GuestAnswer, GuestInvitationDto } from '@/features/guest-links/api'
import '@/styles/tokens.css'
import '@/styles/base.css'
import '@/styles/login.css'

type View =
  | { kind: 'loading' }
  | { kind: 'saving' }
  | { kind: 'shown'; invitation: GuestInvitationDto }
  | { kind: 'closed' }
  | { kind: 'notFound' }
  /** Couldn't reach the server: `again` is what to repeat (an answer, or null to just look again). */
  | { kind: 'failed'; again: GuestAnswer | null }

const CLOSED_CODE = 'Dixels:Bookings:ResponseClosed'

/** `?answer=accepted|declined` from the email's buttons; anything else just opens the page. */
function answerFrom(value: string | null): GuestAnswer | null {
  if (value === 'accepted') return InviteeResponseStatus.Accepted
  if (value === 'declined') return InviteeResponseStatus.Declined
  return null
}

/**
 * Link scanners (Outlook Safe Links and the like) open every link in an email, some in a browser
 * that runs scripts. A plain GET of this page never answers; the answer is saved by a POST, and
 * only from a page someone is looking at, in a browser not driven by automation.
 */
function looksLikeAPerson(): boolean {
  return document.visibilityState === 'visible' && !navigator.webdriver
}

/**
 * The public answer page behind a guest's link (no sign-in): the email's Accept or Decline
 * saves here, then the guest sees what they answered, the meeting, and a way to change their
 * mind. It opens in the booker's language (the invite's), with a switcher.
 */
export function RsvpPage() {
  // Re-renders on a language change (the switcher), which reads the invitation again below.
  useTranslation()
  const { token = '' } = useParams()
  const [params] = useSearchParams()
  const [view, setView] = useState<View>({ kind: 'loading' })
  const pickedLanguage = useRef(false)
  const language = currentLanguage()

  const fail = useCallback((error: unknown, again: GuestAnswer | null) => {
    if (error instanceof ApiError && error.status === 404) setView({ kind: 'notFound' })
    else if (error instanceof ApiError && error.code === CLOSED_CODE) setView({ kind: 'closed' })
    else setView({ kind: 'failed', again })
  }, [])

  const show = useCallback((invitation: GuestInvitationDto) => {
    // The first time only: open in the invite's language, then leave the choice to the guest.
    if (!pickedLanguage.current) {
      pickedLanguage.current = true
      if (invitation.language !== currentLanguage() && availableLanguages().some((l) => l.code === invitation.language)) {
        void setLanguage(invitation.language)
      }
    }
    setView(invitation.isOpen ? { kind: 'shown', invitation } : { kind: 'closed' })
  }, [])

  const save = useCallback(
    (value: GuestAnswer) => {
      answerInvitation(token, value).then(show, (error: unknown) => fail(error, value))
    },
    [token, show, fail],
  )

  const answer = useCallback(
    (value: GuestAnswer) => {
      setView({ kind: 'saving' })
      save(value)
    },
    [save],
  )

  const lookup = useCallback(() => {
    lookupInvitation(token).then(show, (error: unknown) => fail(error, null))
  }, [token, show, fail])

  // Opening: save the email's answer when a person is looking, else just show the invitation.
  const opened = useRef(false)
  useEffect(() => {
    if (opened.current) return
    opened.current = true
    const fromEmail = answerFrom(params.get('answer'))
    if (fromEmail === null || navigator.webdriver) {
      lookup()
      return
    }
    if (looksLikeAPerson()) {
      save(fromEmail)
      return
    }
    // Opened in a background tab: answer once it's actually looked at.
    const onVisible = () => {
      if (!looksLikeAPerson()) return
      document.removeEventListener('visibilitychange', onVisible)
      save(fromEmail)
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => document.removeEventListener('visibilitychange', onVisible)
  }, [params, save, lookup])

  // Another language picked: read the invitation again, for the room's names in it.
  const shownLanguage = useRef(language)
  useEffect(() => {
    if (shownLanguage.current === language) return
    shownLanguage.current = language
    if (view.kind === 'shown') lookup()
  }, [language, view.kind, lookup])

  return (
    <div className="authscreen">
      <div className="authcard rsvpcard" aria-live="polite">
        <div className="rsvptop">
          <Logo className="authlogo rsvplogo" />
          <LanguageSwitcher compact align="end" />
        </div>
        <RsvpBody view={view} onAnswer={answer} onRetry={() => (view.kind === 'failed' && view.again !== null ? answer(view.again) : lookup())} />
      </div>
    </div>
  )
}

function RsvpBody({ view, onAnswer, onRetry }: { view: View; onAnswer: (value: GuestAnswer) => void; onRetry: () => void }) {
  const { t } = useTranslation()
  switch (view.kind) {
    case 'loading':
    case 'saving':
      return (
        <div role="status">
          <div className="authicon busy" aria-hidden="true">
            <span className="authspinner" />
          </div>
          {view.kind === 'saving' && <p className="authdetail">{t('Rsvp:Saving')}</p>}
        </div>
      )
    case 'closed':
    case 'notFound':
      return (
        <div role="alert">
          <div className="authicon warn" aria-hidden="true">
            ?
          </div>
          <h1 className="authtitle">{t(view.kind === 'closed' ? 'Rsvp:ClosedTitle' : 'Rsvp:NotFoundTitle')}</h1>
          <p className="authdetail">{t(view.kind === 'closed' ? 'Rsvp:ClosedDetail' : 'Rsvp:NotFoundDetail')}</p>
        </div>
      )
    case 'failed':
      return (
        <div role="alert">
          <div className="authicon error" aria-hidden="true">
            !
          </div>
          <h1 className="authtitle">{t('Rsvp:SaveFailed')}</h1>
          <button type="button" className="btn" onClick={onRetry}>
            {t('Rsvp:TryAgain')}
          </button>
        </div>
      )
    case 'shown':
      return <Invitation invitation={view.invitation} onAnswer={onAnswer} />
  }
}

function Invitation({ invitation, onAnswer }: { invitation: GuestInvitationDto; onAnswer: (value: GuestAnswer) => void }) {
  const { t } = useTranslation()
  const answer = invitation.myResponse
  const heading =
    answer === InviteeResponseStatus.Accepted ? 'Rsvp:Accepted' : answer === InviteeResponseStatus.Declined ? 'Rsvp:Declined' : 'Rsvp:Pending'
  const change = answer === InviteeResponseStatus.Accepted ? 'Rsvp:ChangeToDecline' : 'Rsvp:ChangeToAccept'
  const date = dateOf(invitation.localStart)
  const where = [invitation.spaceName, invitation.floorName, invitation.buildingName].filter(Boolean).join(' · ')

  return (
    <>
      {answer === InviteeResponseStatus.Accepted && (
        <div className="authicon done rsvpbig" aria-hidden="true">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
            <path d="M5 12.5 10 17.5 19 7.5" />
          </svg>
        </div>
      )}
      {answer === InviteeResponseStatus.Declined && (
        <div className="authicon error rsvpbig" aria-hidden="true">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round">
            <path d="M7 7l10 10M17 7 7 17" />
          </svg>
        </div>
      )}
      <h1 className="authtitle">{t(heading)}</h1>
      <p className="authdetail">{t('Rsvp:InvitedBy', { name: invitation.invitedBy })}</p>

      <p className="rsvptitle">{bookingTitle(invitation.title ?? '')}</p>
      <dl className="rsvpfacts">
        <div>
          <dt>{t('Rsvp:When')}</dt>
          <dd>
            {formatDay(date, 'long')}
            <br />
            {formatClockRange(timeOf(invitation.localStart), timeOf(invitation.localEnd))}
          </dd>
        </div>
        {invitation.recurrence && (
          <div>
            <dt>{t('Rsvp:Repeats')}</dt>
            <dd>{describeRecurrence(invitation.recurrence, date)}</dd>
          </div>
        )}
        <div>
          <dt>{t('Rsvp:Where')}</dt>
          <dd>
            {where}
            {invitation.address && (
              <>
                <br />
                <span className="rsvpmuted">{invitation.address}</span>
              </>
            )}
          </dd>
        </div>
      </dl>
      {invitation.recurrence && <p className="authdetail">{t('Rsvp:AllDates')}</p>}

      {answer === InviteeResponseStatus.Pending ? (
        <div className="rsvpbuttons">
          <button type="button" className="btn" onClick={() => onAnswer(InviteeResponseStatus.Accepted)}>
            {t('Rsvp:Accept')}
          </button>
          <button type="button" className="btn sec" onClick={() => onAnswer(InviteeResponseStatus.Declined)}>
            {t('Rsvp:Decline')}
          </button>
        </div>
      ) : (
        <button
          type="button"
          className="rsvpchange"
          onClick={() => onAnswer(answer === InviteeResponseStatus.Accepted ? InviteeResponseStatus.Declined : InviteeResponseStatus.Accepted)}
        >
          {t(change)}
        </button>
      )}
    </>
  )
}
