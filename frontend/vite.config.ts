import { fileURLToPath } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { loadEnv } from 'vite'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // '' prefix = also read non-VITE_ keys. DEV_PORT lets another worktree run its dev server
  // beside the main one; it stays out of the app bundle since it isn't VITE_-prefixed.
  const env = loadEnv(mode, process.cwd(), '')

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      // "@/components/ui/button" — the import style shadcn/ui components are written in.
      alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
    },
    server: {
      port: Number(env.DEV_PORT) || 5173,
      // Fail instead of drifting to another port: sign-in only redirects back to
      // addresses the backend's Dixels_App client lists.
      strictPort: true,
    },
    test: {
      // Default stays 'node' — fast, and the existing pure-logic domain tests don't need a
      // DOM. Component tests opt into jsdom per-file via a `// @vitest-environment jsdom`
      // docblock instead of paying the jsdom cost for every test in the suite.
      environment: 'node',
      include: ['src/**/*.test.ts', 'src/**/*.test.tsx'],
      setupFiles: ['src/test/setupTests.ts'],
    },
  }
})
