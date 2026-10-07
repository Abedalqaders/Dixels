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
    build: {
      rolldownOptions: {
        output: {
          // Libraries in their own files, apart from the app's code: they change far less
          // often, so after a deploy the browser still has them cached and only downloads the
          // app files that changed. Grouped by what loads together; a group only some pages
          // use (the date picker) downloads with the first of those pages.
          codeSplitting: {
            groups: [
              { name: 'react', test: /[\\/]node_modules[\\/](react|react-dom|scheduler|react-router|react-router-dom|cookie|set-cookie-parser)[\\/]/ },
              { name: 'auth', test: /[\\/]node_modules[\\/](oidc-client-ts|react-oidc-context|jwt-decode)[\\/]/ },
              { name: 'i18n', test: /[\\/]node_modules[\\/](i18next|react-i18next|html-parse-stringify|void-elements)[\\/]/ },
              { name: 'query', test: /[\\/]node_modules[\\/]@tanstack[\\/]/ },
              {
                name: 'ui',
                test: /[\\/]node_modules[\\/](radix-ui|@radix-ui|@floating-ui|react-remove-scroll|react-remove-scroll-bar|react-style-singleton|use-callback-ref|use-sidecar|aria-hidden|get-nonce|tslib|detect-node-es|lucide-react|tailwind-merge|clsx|class-variance-authority)[\\/]/,
              },
              { name: 'dates', test: /[\\/]node_modules[\\/](react-day-picker|date-fns|@date-fns)[\\/]/ },
            ],
          },
        },
      },
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
