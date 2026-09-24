import { useState } from 'react'
import { useAuth } from 'react-oidc-context'
import { Sidebar } from '../../../components/Sidebar'
import { SearchIcon } from '../../../components/icons'
import { Pager } from '../../../components/Pager'
import { Toast, useToast } from '../../../components/Toast'
import { useAsync } from '../../../hooks/useAsync'
import { useDebouncedValue } from '../../../hooks/useDebouncedValue'
import { ApiError, assignEmployeeBuilding, getEmployees } from '../api/employeesApi'
import type { EmployeeDto } from '../api/employeesApi'
import { getBuildings } from '../../space-management/api/spaceManagementApi'
import '../../../styles/tokens.css'
import '../../../styles/base.css'
import '../../../styles/admin.css'

const UNASSIGNED = ''
const ANY_BUILDING = ''
const PAGE_SIZE = 10

function labelFor(employee: EmployeeDto): string {
  return [employee.name, employee.surname].filter(Boolean).join(' ') || employee.userName
}

function initialsOf(label: string): string {
  return label
    .split(/[\s._-]+/)
    .filter(Boolean)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase()
}

export function AdminEmployeesPage() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { toast, showToast } = useToast()

  const [search, setSearch] = useState('')
  const debouncedSearch = useDebouncedValue(search)
  const [buildingFilter, setBuildingFilter] = useState(ANY_BUILDING)
  const [page, setPage] = useState(0)

  const { status, data, error, refetch } = useAsync(async () => {
    const [employeesResult, buildingsResult] = await Promise.all([
      getEmployees(token, {
        filter: debouncedSearch.trim() || undefined,
        buildingId: buildingFilter || undefined,
        skipCount: page * PAGE_SIZE,
        maxResultCount: PAGE_SIZE,
      }),
      // Every building, for the filter dropdown and the per-row assignment picker — not
      // paged, since an admin needs the whole list to choose from either way.
      getBuildings(token, { maxResultCount: 1000 }),
    ])
    return { employees: employeesResult.items, totalCount: employeesResult.totalCount, buildings: buildingsResult.items }
  }, [token, debouncedSearch, buildingFilter, page])

  function handleSearchChange(value: string) {
    setSearch(value)
    setPage(0)
  }

  function handleBuildingFilterChange(value: string) {
    setBuildingFilter(value)
    setPage(0)
  }

  async function handleAssign(employeeUserId: string, employeeLabel: string, buildingId: string) {
    try {
      await assignEmployeeBuilding(token, employeeUserId, { buildingId: buildingId === UNASSIGNED ? null : buildingId })
      showToast(buildingId === UNASSIGNED ? `${employeeLabel} unassigned.` : `${employeeLabel} assigned to a building.`)
      refetch()
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Something went wrong — please try again.', 'error')
    }
  }

  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <div className="content">
          <div>
            <h1 className="pagetitle">Employees</h1>
            <p className="lead">
              {status === 'success' ? `${data.totalCount} employee${data.totalCount === 1 ? '' : 's'}. ` : ''}
              Choose which single building each employee can see — they never see any other.
            </p>
          </div>

          <section className="card" id="employees">
            <div className="cardhead">
              <h2 className="sectiontitle">All employees</h2>
              <div className="treetools">
                <div className="searchbox">
                  <SearchIcon />
                  <input
                    type="text"
                    placeholder="Search by name, username or email…"
                    autoComplete="off"
                    aria-label="Search employees"
                    value={search}
                    onChange={(e) => handleSearchChange(e.target.value)}
                  />
                </div>
                <select
                  className="ctrl"
                  style={{ width: 'auto' }}
                  aria-label="Filter by building"
                  value={buildingFilter}
                  onChange={(e) => handleBuildingFilterChange(e.target.value)}
                >
                  <option value={ANY_BUILDING}>All buildings</option>
                  {data?.buildings.map((building) => (
                    <option key={building.id} value={building.id}>
                      {building.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="pad">
              {status === 'loading' && <p className="treeempty">Loading employees…</p>}
              {status === 'error' && <p className="treeempty">Couldn't load employees: {error.message}</p>}
              {status === 'success' && data.employees.length === 0 && (
                <p className="treeempty">
                  {debouncedSearch.trim() || buildingFilter
                    ? 'No employees match the current search and filter.'
                    : 'No employee accounts yet.'}
                </p>
              )}

              {status === 'success' && data.employees.length > 0 && (
                <table className="employeetable">
                  <thead>
                    <tr>
                      <th>Employee</th>
                      <th>Email</th>
                      <th className="col-status">Status</th>
                      <th className="col-building">Building</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.employees.map((employee) => {
                      const label = labelFor(employee)
                      const isAssigned = Boolean(employee.assignedBuildingId)
                      return (
                        <tr key={employee.id}>
                          <td>
                            <div className="employee-identity">
                              <span className="av">{initialsOf(label)}</span>
                              <span>
                                <div className="employee-name">{label}</div>
                                <div className="employee-username">{employee.userName}</div>
                              </span>
                            </div>
                          </td>
                          <td className="m">{employee.email ?? '—'}</td>
                          <td className="col-status">
                            <span className={`badge ${isAssigned ? 'confirmed' : 'blocked'}`}>
                              {isAssigned ? 'Assigned' : 'Not assigned'}
                            </span>
                          </td>
                          <td className="col-building">
                            <select
                              className="ctrl"
                              aria-label={`Building for ${label}`}
                              value={employee.assignedBuildingId ?? UNASSIGNED}
                              onChange={(e) => handleAssign(employee.id, label, e.target.value)}
                            >
                              <option value={UNASSIGNED}>Not assigned</option>
                              {(data?.buildings ?? []).map((building) => (
                                <option key={building.id} value={building.id}>
                                  {building.name}
                                </option>
                              ))}
                            </select>
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              )}
            </div>

            {status === 'success' && (
              <Pager page={page} pageSize={PAGE_SIZE} totalCount={data.totalCount} onPageChange={setPage} />
            )}
          </section>
        </div>
      </div>

      <Toast toast={toast} />
    </div>
  )
}
