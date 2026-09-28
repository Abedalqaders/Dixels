import { Navigate, Route, Routes } from 'react-router-dom'
import { RequireAuth } from '../auth/RequireAuth'
import { RequireAdmin } from '../auth/RequireAdmin'
import { CallbackPage } from '../pages/CallbackPage'
import { DashboardPage } from '../pages/DashboardPage'
import { BuildingsListPage } from '../features/space-management/routes/BuildingsListPage'
import { FloorsListPage } from '../features/space-management/routes/FloorsListPage'
import { SpacesListPage } from '../features/space-management/routes/SpacesListPage'
import { SpaceManagementLayout } from '../features/space-management/routes/SpaceManagementLayout'
import { SpaceTypesPage } from '../features/space-management/routes/SpaceTypesPage'
import { AdminConstraintsPage } from '../features/space-management/routes/AdminConstraintsPage'
import { AdminUsersPage } from '../features/users/routes/AdminUsersPage'
import { HomePage } from '../pages/HomePage'
import { EmployeeLayout } from '../components/EmployeeLayout'
import { FindSpacePage } from '../features/bookings/routes/FindSpacePage'
import { MyCalendarPage } from '../features/calendar/routes/MyCalendarPage'

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
        element={
          <RequireAuth>
            <EmployeeLayout />
          </RequireAuth>
        }
      >
        <Route path="/my-calendar" element={<MyCalendarPage />} />
        <Route path="/find-space" element={<FindSpacePage />} />
      </Route>
      <Route
        element={
          <RequireAdmin>
            <SpaceManagementLayout />
          </RequireAdmin>
        }
      >
        <Route path="/admin/buildings" element={<BuildingsListPage />} />
        <Route path="/admin/buildings/:buildingId/floors" element={<FloorsListPage />} />
        <Route path="/admin/buildings/:buildingId/floors/:floorId/spaces" element={<SpacesListPage />} />
      </Route>
      {/* Old admin URLs that have since moved — keep bookmarks working. */}
      <Route path="/admin/floors" element={<Navigate to="/admin/buildings" replace />} />
      <Route path="/admin/employees" element={<Navigate to="/admin/users" replace />} />
      <Route
        path="/admin/space-types"
        element={
          <RequireAdmin>
            <SpaceTypesPage />
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
      <Route
        path="/admin/users"
        element={
          <RequireAdmin>
            <AdminUsersPage />
          </RequireAdmin>
        }
      />
    </Routes>
  )
}

export default App
