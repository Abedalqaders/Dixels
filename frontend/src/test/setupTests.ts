import '@testing-library/jest-dom/vitest'
import { afterEach } from 'vitest'
import { cleanup, configure } from '@testing-library/react'

// findBy*/waitFor give up after 1s by default. The first test in a page's file pays for
// the page's imports and first render, which under a full parallel run can take longer
// than that on its own — so those waits were failing on timing, not on behaviour.
configure({ asyncUtilTimeout: 4000 })

// vitest.config's `test.globals` is left off (no implicit global test APIs), so
// Testing Library's own auto-cleanup — which detects globals — never registers.
// Do it explicitly instead, once, for every test file: without it, renders (and
// the effects/listeners they mount) leak across tests within the same file.
afterEach(() => {
  cleanup()
})
