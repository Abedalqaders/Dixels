import { Route, Routes } from 'react-router-dom'
import { RequireAuth } from '../auth/RequireAuth'
import { RequireAdmin } from '../auth/RequireAdmin'
import { CallbackPage } from '../pages/CallbackPage'
import { DashboardPage } from '../pages/DashboardPage'
import { BuildingsListPage } from '../features/space-management/routes/BuildingsListPage'
import { FloorsListPage } from '../features/space-management/routes/FloorsListPage'
import { SpacesListPage } from '../features/space-management/routes/SpacesListPage'
import { AllFloorsPage } from '../features/space-management/routes/AllFloorsPage'
import { AllSpacesPage } from '../features/space-management/routes/AllSpacesPage'
import { AdminConstraintsPage } from '../features/space-management/routes/AdminConstraintsPage'
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
          <RequireAdmin>
            <BuildingsListPage />
          </RequireAdmin>
        }
      />
      <Route
        path="/admin/buildings/:buildingId/floors"
        element={
          <RequireAdmin>
            <FloorsListPage />
          </RequireAdmin>
        }
      />
      <Route
        path="/admin/buildings/:buildingId/floors/:floorId/spaces"
        element={
          <RequireAdmin>
            <SpacesListPage />
          </RequireAdmin>
        }
      />
      <Route
        path="/admin/floors"
        element={
          <RequireAdmin>
            <AllFloorsPage />
          </RequireAdmin>
        }
      />
      <Route
        path="/admin/spaces"
        element={
          <RequireAdmin>
            <AllSpacesPage />
          </RequireAdmin>
        }
      />
      <Route
        path="/admin/constraints/:level/:id"
        element={
          <RequireAdmin>
            <AdminConstraintsPage />
          </RequireAdmin>
        }
      />
    </Routes>
  )
}

export default App
