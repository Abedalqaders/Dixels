import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  test: {
    // Default stays 'node' — fast, and the existing pure-logic domain tests don't need a
    // DOM. Component tests opt into jsdom per-file via a `// @vitest-environment jsdom`
    // docblock instead of paying the jsdom cost for every test in the suite.
    environment: 'node',
    include: ['src/**/*.test.ts', 'src/**/*.test.tsx'],
    setupFiles: ['src/test/setupTests.ts'],
  },
})
