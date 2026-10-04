import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { loadLocalization } from '@/i18n'
import '@/styles/ui.css'
import App from '@/app/App.tsx'
import { AppErrorBoundary } from '@/components/AppErrorBoundary'
import { AppQueryProvider } from '@/components/AppQueryProvider'
import { LocaleRoot } from '@/components/LocaleRoot'
import { Toaster } from '@/components/Toast'
import { AuthProvider } from '@/features/auth/components/AuthProvider.tsx'
import { AuthStatusScreen } from '@/features/auth/components/AuthStatusScreen'

const root = createRoot(document.getElementById('root')!)

// LocaleRoot sits inside the auth and query providers: a language change re-renders the
// pages, but keeps the signed-in session and the cached data (which it refetches).
function renderApp() {
  root.render(
    <StrictMode>
      <AppErrorBoundary scope="app">
        <AuthProvider>
          <AppQueryProvider>
            <LocaleRoot>
              <App />
              <Toaster />
            </LocaleRoot>
          </AppQueryProvider>
        </AuthProvider>
      </AppErrorBoundary>
    </StrictMode>,
  )
}

// The texts come from the backend (see src/i18n). Only a first visit with the backend down
// has none at all — the one screen that can't be translated, and before any language has
// been picked. So it's written here in English and Arabic, both at once.
function renderUnreachable() {
  root.render(
    <AuthStatusScreen state="error" title="Couldn't reach the server" detail="Check your connection, then try again.">
      <div lang="ar" dir="rtl" className="mt-5">
        <p className="authtitle">تعذّر الوصول إلى الخادم</p>
        <p className="authdetail">تحقق من اتصالك، ثم حاول مرة أخرى.</p>
      </div>
      <button type="button" className="btn" onClick={boot}>
        Retry · <span lang="ar">إعادة المحاولة</span>
      </button>
    </AuthStatusScreen>,
  )
}

function boot() {
  loadLocalization().then(renderApp, renderUnreachable)
}

boot()
