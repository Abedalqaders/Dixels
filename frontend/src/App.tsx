import { Route, Routes } from 'react-router-dom'
import { RequireAuth } from './auth/RequireAuth'
import { Callback } from './routes/Callback'
import { Dashboard } from './routes/Dashboard'
import { AdminBuildings } from './routes/AdminBuildings'
import { Home } from './routes/Home'

function App() {
  return (
    <Routes>
      <Route path="/" element={<Home />} />
      <Route path="/callback" element={<Callback />} />
      <Route
        path="/dashboard"
        element={
          <RequireAuth>
            <Dashboard />
          </RequireAuth>
        }
      />
      <Route
        path="/admin/buildings"
        element={
          <RequireAuth>
            <AdminBuildings />
          </RequireAuth>
        }
      />
    </Routes>
  )
}

export default App
