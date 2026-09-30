import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import { API_BASE_URL } from '@/lib/api/baseUrl'

/**
 * The web app has no texts or languages of its own: both come from ABP on the backend.
 * - the language list and the default language from /api/abp/application-configuration
 *   (AbpLocalizationOptions.Languages in DixelsDomainModule);
 * - the texts from /api/abp/application-localization (Localization/Dixels/<code>.json).
 * Adding a language is therefore a backend-only change.
 *
 * Both are kept in localStorage, so a returning visitor sees the app at once — the cached
 * copy renders and a fresh one replaces it in the background. Only a first visit waits.
 */

export interface LanguageOption {
  /** ABP culture name: "en", "ar", "fr"… */
  code: string
  /** In its own language: "English", "العربية". */
  name: string
  dir: 'ltr' | 'rtl'
  /** For Intl formatting. Western digits everywhere ("10:30", not "١٠:٣٠"). */
  intl: string
}

type Texts = Record<string, string>

interface LocalizationConfig {
  languages: { code: string; name: string }[]
  defaultLanguage: string
}

const PREFERENCE_KEY = 'dixels.language'
const CONFIG_KEY = 'dixels.l10n.config'
const textsKey = (code: string) => `dixels.l10n.texts.${code}`

/** Scripts written right to left, for engines without Intl.Locale text info. */
const RTL_LANGUAGES = new Set(['ar', 'fa', 'he', 'ur', 'ps', 'ku', 'dv', 'yi', 'sd', 'ug'])

function directionOf(code: string): 'ltr' | 'rtl' {
  try {
    const locale = new Intl.Locale(code) as Intl.Locale & {
      getTextInfo?: () => { direction?: string }
      textInfo?: { direction?: string }
    }
    const direction = locale.getTextInfo?.().direction ?? locale.textInfo?.direction
    if (direction === 'rtl' || direction === 'ltr') return direction
  } catch {
    // Not a valid tag for this engine: fall back to the list.
  }
  return RTL_LANGUAGES.has(code.split('-')[0].toLowerCase()) ? 'rtl' : 'ltr'
}

function toOption(language: { code: string; name: string }): LanguageOption {
  return { ...language, dir: directionOf(language.code), intl: `${language.code}-u-nu-latn` }
}

// Until the backend has answered, the app knows only English (used for the boot screen).
let languages: LanguageOption[] = [toOption({ code: 'en', name: 'English' })]
let defaultLanguage = 'en'

void i18n.use(initReactI18next).init({
  resources: {},
  lng: defaultLanguage,
  fallbackLng: defaultLanguage,
  // ABP keys are flat and contain ':' ("Nav:Hierarchy"): no nesting, no namespaces in keys.
  keySeparator: false,
  nsSeparator: false,
  // ABP's placeholder style — "Signed in as {name}" — the same as the server's own messages.
  interpolation: { prefix: '{', suffix: '}', escapeValue: false },
  initAsync: false,
  // Re-render when texts arrive (the background refresh), not only on a language switch.
  react: { bindI18nStore: 'added' },
})

export function availableLanguages(): readonly LanguageOption[] {
  return languages
}

export function getDefaultLanguage(): string {
  return defaultLanguage
}

/** The language showing right now. */
export function currentLanguage(): string {
  return i18n.language || defaultLanguage
}

export function languageInfo(code: string = currentLanguage()): LanguageOption {
  return languages.find((l) => l.code === code) ?? toOption({ code, name: code })
}

/** <html lang dir> drive the browser's own behaviour (text direction, fonts, screen readers). */
function applyToDocument(code: string) {
  if (typeof document === 'undefined') return
  document.documentElement.lang = code
  document.documentElement.dir = languageInfo(code).dir
}

i18n.on('languageChanged', (code) => {
  applyToDocument(code)
  write(PREFERENCE_KEY, code)
})

// ---- storage (never fatal: private mode or blocked storage just means no cache) ----

function read<T>(key: string): T | undefined {
  try {
    const raw = localStorage.getItem(key)
    return raw ? (JSON.parse(raw) as T) : undefined
  } catch {
    return undefined
  }
}

function write(key: string, value: unknown) {
  try {
    localStorage.setItem(key, typeof value === 'string' ? value : JSON.stringify(value))
  } catch {
    // Not cached; still works for this visit.
  }
}

function readPreference(): string | undefined {
  try {
    return localStorage.getItem(PREFERENCE_KEY) ?? undefined
  } catch {
    return undefined
  }
}

// ---- backend ----

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, { headers: { Accept: 'application/json' } })
  if (!response.ok) throw new Error(`${path} answered ${response.status}`)
  return (await response.json()) as T
}

async function fetchConfig(): Promise<LocalizationConfig> {
  const config = await getJson<{
    localization: { languages: { cultureName: string; displayName: string }[] }
    setting?: { values?: Record<string, string> }
  }>('/api/abp/application-configuration?includeLocalizationResources=false')
  return {
    languages: config.localization.languages.map((l) => ({ code: l.cultureName, name: l.displayName })),
    defaultLanguage: config.setting?.values?.['Abp.Localization.DefaultLanguage'] ?? 'en',
  }
}

async function fetchTexts(code: string): Promise<Texts> {
  const localization = await getJson<{ resources: Record<string, { texts: Texts }> }>(
    `/api/abp/application-localization?cultureName=${encodeURIComponent(code)}&onlyDynamics=false`,
  )
  return localization.resources.Dixels?.texts ?? {}
}

// ---- applying ----

/** Sets the known languages. Exported for tests, which configure them without a backend. */
export function configureLanguages(config: LocalizationConfig) {
  languages = config.languages.map(toOption)
  defaultLanguage = config.defaultLanguage
  i18n.options.fallbackLng = defaultLanguage
}

/** Adds (or replaces) one language's texts. Exported for tests. */
export function registerTexts(code: string, texts: Texts) {
  i18n.addResourceBundle(code, 'translation', texts, false, true)
}

function pickLanguage(): string {
  const known = (code: string | undefined) => (code && languages.some((l) => l.code === code) ? code : undefined)
  return (
    known(readPreference()) ??
    known(globalThis.navigator?.language) ??
    known(globalThis.navigator?.language?.split('-')[0]) ??
    defaultLanguage
  )
}

/** Fetches and applies fresh config + texts; caches them. */
async function refresh(code: string) {
  const config = await fetchConfig()
  configureLanguages(config)
  write(CONFIG_KEY, config)
  const language = languages.some((l) => l.code === code) ? code : pickLanguage()
  const texts = await fetchTexts(language)
  registerTexts(language, texts)
  write(textsKey(language), texts)
  // The fallback language must be loaded too, for any key a language hasn't translated yet.
  if (language !== defaultLanguage && !i18n.hasResourceBundle(defaultLanguage, 'translation')) {
    const fallback = await fetchTexts(defaultLanguage)
    registerTexts(defaultLanguage, fallback)
    write(textsKey(defaultLanguage), fallback)
  }
  return language
}

/**
 * Called once before the app renders. Resolves as soon as texts are available — from the
 * cache (then refreshed in the background) or from the backend. Rejects only when neither
 * has them: a first visit with the backend down.
 */
export async function loadLocalization(): Promise<void> {
  const cachedConfig = read<LocalizationConfig>(CONFIG_KEY)
  if (cachedConfig) {
    configureLanguages(cachedConfig)
    const language = pickLanguage()
    const cachedTexts = read<Texts>(textsKey(language))
    const cachedFallback = read<Texts>(textsKey(defaultLanguage))
    if (cachedTexts) {
      registerTexts(language, cachedTexts)
      if (cachedFallback) registerTexts(defaultLanguage, cachedFallback)
      await i18n.changeLanguage(language)
      void refresh(language)
        .then((fresh) => (fresh !== currentLanguage() ? i18n.changeLanguage(fresh) : undefined))
        .catch(() => {
          // Offline or the backend is restarting: the cached texts stay.
        })
      return
    }
  }

  const language = await refresh(pickLanguage())
  await i18n.changeLanguage(language)
}

/** Switches language, fetching its texts first if this browser hasn't got them yet. */
export async function setLanguage(code: string): Promise<void> {
  if (!i18n.hasResourceBundle(code, 'translation')) {
    const cached = read<Texts>(textsKey(code))
    if (cached) {
      registerTexts(code, cached)
    } else {
      const texts = await fetchTexts(code)
      registerTexts(code, texts)
      write(textsKey(code), texts)
    }
  }
  await i18n.changeLanguage(code)
}

export default i18n
