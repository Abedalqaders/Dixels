import { useEffect, useRef } from 'react'
import { useAuth } from 'react-oidc-context'
import { useTranslation } from 'react-i18next'
import { saveMyLanguage } from './myLanguageApi'

/**
 * Tells the backend which language the signed-in user is using — once per sign-in and again
 * whenever they switch — so the emails Dixels sends them (booking confirmed, cancelled…) are
 * in it too. Renders nothing.
 *
 * Best effort: if the call fails, the next token renew or visit sends it again; meanwhile
 * their emails are in the last language that did reach the backend.
 */
export function LanguageSync() {
  const auth = useAuth()
  const { i18n } = useTranslation()
  const userId = auth.user?.profile.sub
  const token = auth.user?.access_token
  const language = i18n.language
  // What was last sent, so a token renew (a new token, same user and language) sends nothing.
  const sent = useRef<string | null>(null)

  useEffect(() => {
    if (!userId || !token || !language) return
    const key = `${userId}:${language}`
    if (sent.current === key) return
    sent.current = key
    saveMyLanguage(token, language).catch(() => {
      if (sent.current === key) sent.current = null
    })
  }, [userId, token, language])

  return null
}
