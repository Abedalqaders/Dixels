import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Direction } from 'radix-ui'
import { currentLanguage, languageInfo } from '@/i18n'

/**
 * Everything below follows the current language:
 * - Radix parts (menus, selects, popovers) open and move their focus for its direction;
 * - the tree is re-mounted when the language changes, so text worked out once and kept —
 *   a formatted date in a memo, a label in state — is worked out again in the new one.
 *   Switching language is rare and deliberate, so a fresh render is a fair price for
 *   never showing half a screen in the old language.
 */
export function LocaleRoot({ children }: { children: ReactNode }) {
  // Subscribes to language changes.
  useTranslation()
  const language = currentLanguage()

  return (
    <Direction.Provider dir={languageInfo(language).dir}>
      <div key={language} className="contents">
        {children}
      </div>
    </Direction.Provider>
  )
}
