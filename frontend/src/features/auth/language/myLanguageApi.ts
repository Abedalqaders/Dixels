import { request } from '@/lib/api/httpClient'

/** ABP UpdateMyLanguageDto. */
export interface UpdateMyLanguageDto {
  /** One of the app's languages, by culture name ("en", "ar"). */
  language: string
}

/** Saves the language the signed-in user is using; the emails Dixels sends them use it. */
export function saveMyLanguage(token: string, language: string): Promise<void> {
  const body: UpdateMyLanguageDto = { language }
  return request<void>('/api/app/my-preferences/language', token, { method: 'PUT', body: JSON.stringify(body) })
}
