// The signed-in person's own account: ABP's profile endpoints (/api/account/my-profile), not a
// Dixels one. Dixels turns off username and email changes there (DixelsSettingDefinitionProvider),
// so only the name and phone a person sends are saved. The password rules are ABP Identity's
// own settings, checked on the server. Same request/ApiError plumbing as usersApi.ts.

import { request, requestBlob } from '@/lib/api/httpClient'

export { ApiError } from '@/lib/api/httpClient'

/** ABP's ProfileDto (the fields this app uses). */
export interface ProfileDto {
  userName: string
  email: string
  name: string | null
  surname: string | null
  phoneNumber: string | null
  /** False for an account that has never had a password: setting one needs no current one. */
  hasPassword: boolean
  concurrencyStamp: string
}

/** ABP's ChangePasswordInput. */
export interface ChangePasswordInput {
  /** Left out when the account has no password yet (see ProfileDto.hasPassword). */
  currentPassword?: string
  newPassword: string
}

/** ABP's UpdateProfileDto. Username and email go back as they came; the server ignores changes. */
export interface UpdateProfileDto {
  userName: string
  email: string
  name: string | null
  surname: string | null
  phoneNumber: string | null
  concurrencyStamp: string
}

/** IdentityUserConsts on the backend: MaxNameLength, MaxSurnameLength, MaxPhoneNumberLength. */
export const MAX_NAME_LENGTH = 64
export const MAX_PHONE_LENGTH = 16
/** IdentityUserConsts.MaxPasswordLength. */
export const MAX_PASSWORD_LENGTH = 128

export function getMyProfile(token: string) {
  return request<ProfileDto>('/api/account/my-profile', token)
}

export function updateMyProfile(token: string, input: UpdateProfileDto) {
  return request<ProfileDto>('/api/account/my-profile', token, { method: 'PUT', body: JSON.stringify(input) })
}

export function changeMyPassword(token: string, input: ChangePasswordInput) {
  return request<void>('/api/account/my-profile/change-password', token, { method: 'POST', body: JSON.stringify(input) })
}

/** The person's picture as the server keeps it, or null when they have none. */
export function getMyPicture(token: string) {
  return requestBlob('/api/app/profile-picture', token)
}

/** Replaces the picture. Send it already shrunk (shrinkPicture): the server keeps up to 1 MB. */
export function setMyPicture(token: string, picture: Blob) {
  const form = new FormData()
  form.append('file', picture, 'profile-picture.jpg')
  return request<void>('/api/app/profile-picture', token, { method: 'PUT', body: form })
}

export function removeMyPicture(token: string) {
  return request<void>('/api/app/profile-picture', token, { method: 'DELETE' })
}

/** "Sara Haddad" — or the username when no name is set. */
export function fullNameOf(profile: Pick<ProfileDto, 'name' | 'surname' | 'userName'>): string {
  return [profile.name, profile.surname].filter((part) => part?.trim()).join(' ') || profile.userName
}
