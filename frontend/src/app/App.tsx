import { Route, Routes } from 'react-router-dom'
import { RequireAuth } from '../auth/RequireAuth'
import { CallbackPage } from '../pages/CallbackPage'
import { DashboardPage } from '../pages/DashboardPage'
import { AdminBuildingsPage } from '../features/space-management/routes/AdminBuildingsPage'
import { HomePage } from '../pages/HomePage'

function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/callback" element={<CallbackPage />} />
      <Route
        path="/dashboard"
        element={
          <RequireAuth>
            <DashboardPage />
          </RequireAuth>
        }
      />
      <Route
        path="/admin/buildings"
        element={
          <RequireAuth>
            <AdminBuildingsPage />
          </RequireAuth>
        }
      />
    </Routes>
  )
}

export default App
