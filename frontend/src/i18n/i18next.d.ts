import 'i18next'
import type { TextKeys } from './keys'

// The backend's English texts are the source of truth for keys (see scripts/i18n-types.mjs):
// t('Nav:Hierarchy') is checked at compile time, and a typo or a removed key fails the build.
declare module 'i18next' {
  interface CustomTypeOptions {
    resources: { translation: TextKeys }
    keySeparator: false
    nsSeparator: false
    interpolationPrefix: '{'
    interpolationSuffix: '}'
  }
}
