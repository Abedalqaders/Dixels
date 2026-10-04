import '@testing-library/jest-dom/vitest'
import { afterEach } from 'vitest'
import { cleanup, configure } from '@testing-library/react'
import { clearToasts } from '@/components/Toast'
import { configureLanguages, registerTexts, setLanguage } from '@/i18n'
import { backendLanguageFiles } from './backendLocalization'

// The app gets its texts from the backend at startup; tests read the same backend files
// from disk instead, so pages render their real texts with no network.
const files = backendLanguageFiles()
configureLanguages({
  languages: files.map((f) => ({ code: f.code, name: f.code })),
  defaultLanguage: 'en',
})
for (const f of files) registerTexts(f.code, f.texts)

// Tests read English text. Start every file in English (whatever the machine's own
// language) and put it back after a test that switched to another one.
await setLanguage('en')
afterEach(async () => {
  await setLanguage('en')
})

// findBy*/waitFor give up after 1s by default. The first test in a page's file pays for
// the page's imports and first render, which under a full parallel run can take longer
// than that on its own — so those waits were failing on timing, not on behaviour.
configure({ asyncUtilTimeout: 4000 })

// vitest.config's `test.globals` is left off (no implicit global test APIs), so
// Testing Library's own auto-cleanup — which detects globals — never registers.
// Do it explicitly instead, once, for every test file: without it, renders (and
// the effects/listeners they mount) leak across tests within the same file.
// Toasts live in a module-level store, so they are cleared here too.
afterEach(() => {
  cleanup()
  clearToasts()
})
