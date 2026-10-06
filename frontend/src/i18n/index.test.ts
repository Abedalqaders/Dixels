// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'
import i18n, { loadLocalization, setLanguage } from '@/i18n'

// The backend as it is now: English has a text the copies cached on an earlier visit lack.
const FRESH: Record<string, Record<string, string>> = {
  en: { 'Old:Key': 'Old', 'New:Key': 'Security' },
  ar: { 'Old:Key': 'قديم', 'New:Key': 'الأمان' },
}

function backend() {
  return vi.fn(async (url: string) => {
    const body = url.includes('application-configuration')
      ? {
          localization: { languages: [{ cultureName: 'en', displayName: 'English' }, { cultureName: 'ar', displayName: 'العربية' }] },
          setting: { values: { 'Abp.Localization.DefaultLanguage': 'en' } },
        }
      : { resources: { Dixels: { texts: FRESH[new URL(url).searchParams.get('cultureName')!] } } }
    return new Response(JSON.stringify(body), { status: 200 })
  })
}

describe('localization cache', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('refreshes a cached fallback language, so switching to it shows no raw keys', async () => {
    // A returning visitor, in Arabic, with both languages cached from before New:Key existed.
    localStorage.setItem('dixels.language', 'ar')
    localStorage.setItem('dixels.l10n.config', JSON.stringify({ languages: [{ code: 'en', name: 'English' }, { code: 'ar', name: 'العربية' }], defaultLanguage: 'en' }))
    localStorage.setItem('dixels.l10n.texts.ar', JSON.stringify({ 'Old:Key': 'قديم' }))
    localStorage.setItem('dixels.l10n.texts.en', JSON.stringify({ 'Old:Key': 'Old' }))
    const fetch = backend()
    vi.stubGlobal('fetch', fetch)

    await loadLocalization()
    // The background refresh of both languages.
    await vi.waitFor(() => expect(fetch).toHaveBeenCalledWith(expect.stringContaining('cultureName=en'), expect.anything()))
    await vi.waitFor(() => expect(JSON.parse(localStorage.getItem('dixels.l10n.texts.en')!)).toEqual(FRESH.en))

    await setLanguage('en')

    expect(i18n.t('New:Key' as never)).toBe('Security')
  })
})
