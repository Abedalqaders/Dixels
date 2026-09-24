import '@testing-library/jest-dom/vitest'
import { afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'

// vitest.config's `test.globals` is left off (no implicit global test APIs), so
// Testing Library's own auto-cleanup — which detects globals — never registers.
// Do it explicitly instead, once, for every test file: without it, renders (and
// the effects/listeners they mount) leak across tests within the same file.
afterEach(() => {
  cleanup()
})
