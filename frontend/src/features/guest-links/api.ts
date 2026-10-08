import { requestAnonymous } from '@/lib/api/httpClient'
import type { ApiDto } from '@/lib/api/schemaTypes'

export type GuestInvitationDto = ApiDto<'Dixels.Bookings.GuestInvitationDto'>
export type GuestAnswerInput = ApiDto<'Dixels.Bookings.GuestAnswerInput'>
export type GuestAnswer = GuestAnswerInput['answer']

// The public answer page behind a guest's link: no sign-in, the link's token is the proof.
// Both are POSTs, so the token never sits in a URL that proxies log.

/** The invitation and the guest's answer so far; changes nothing. */
export function lookupInvitation(token: string): Promise<GuestInvitationDto> {
  return requestAnonymous<GuestInvitationDto>('/api/app/rsvp/lookup', {
    method: 'POST',
    body: JSON.stringify({ token }),
  })
}

/** Saves the guest's answer (a series' link: every upcoming date) and returns the invitation as it now stands. */
export function answerInvitation(token: string, answer: GuestAnswer): Promise<GuestInvitationDto> {
  return requestAnonymous<GuestInvitationDto>('/api/app/rsvp', {
    method: 'POST',
    body: JSON.stringify({ token, answer } satisfies GuestAnswerInput),
  })
}
