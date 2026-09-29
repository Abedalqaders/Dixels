import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@/styles/ui.css'
import App from '@/app/App.tsx'
import { AppErrorBoundary } from '@/components/AppErrorBoundary'
import { AppQueryProvider } from '@/components/AppQueryProvider'
import { Toaster } from '@/components/Toast'
import { AuthProvider } from '@/features/auth/components/AuthProvider.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppErrorBoundary scope="app">
      <AuthProvider>
        <AppQueryProvider>
          <App />
          <Toaster />
        </AppQueryProvider>
      </AuthProvider>
    </AppErrorBoundary>
  </StrictMode>,
)
