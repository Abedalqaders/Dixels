import { currentLanguage, languageInfo } from '@/i18n'

/**
 * A count as the reader's language groups it ("1,240" in English), in Latin digits like every
 * other number in the app.
 */
export function formatCount(n: number): string {
  return new Intl.NumberFormat(languageInfo(currentLanguage()).intl).format(n)
}
