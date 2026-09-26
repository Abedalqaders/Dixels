import { useAuth } from 'react-oidc-context'
import { Sidebar } from '../../../components/Sidebar'
import { SearchIcon } from '../../../components/icons'
import { Pager } from '../../../components/Pager'
import { Toast, useToast } from '../../../components/Toast'
import { useAsync } from '../../../hooks/useAsync'
import { useListParams } from '../../../hooks/useListParams'
import { ApiError, assignEmployeeBuilding, getEmployees } from '../api/employeesApi'
import type { EmployeeDto } from '../api/employeesApi'
import { BuildingPicker } from '../../space-management/components/BuildingPicker'
import '../../../styles/tokens.css'
import '../../../styles/base.css'
import '../../../styles/admin.css'

const UNASSIGNED = ''

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

  const list = useListParams()
  const buildingFilter = list.getFilter('building')

  const { status, data, error, isRefreshing, refetch } = useAsync(
    async () => {
      const employeesResult = await getEmployees(token, {
        filter: list.search || undefined,
        buildingId: buildingFilter || undefined,
        skipCount: list.page * list.pageSize,
        maxResultCount: list.pageSize,
      })
      return { employees: employeesResult.items, totalCount: employeesResult.totalCount }
    },
    [token, list.search, buildingFilter, list.page, list.pageSize],
    { keepPreviousData: true },
  )

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
                    value={list.searchInput}
                    onChange={(e) => list.setSearchInput(e.target.value)}
                  />
                </div>
                <BuildingPicker
                  token={token}
                  value={buildingFilter}
                  noneLabel="All buildings"
                  ariaLabel="Filter by building"
                  onChange={(id) => list.setFilter('building', id)}
                />
              </div>
            </div>

            <div className={`pad${isRefreshing ? ' refreshing' : ''}`} aria-busy={isRefreshing}>
              {status === 'loading' && <p className="treeempty">Loading employees…</p>}
              {status === 'error' && <p className="treeempty">Couldn't load employees: {error.message}</p>}
              {status === 'success' && data.employees.length === 0 && (
                <p className="treeempty">
                  {list.search || buildingFilter
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
                            <BuildingPicker
                              token={token}
                              value={employee.assignedBuildingId ?? UNASSIGNED}
                              selectedName={employee.assignedBuildingName}
                              noneLabel="Not assigned"
                              ariaLabel={`Building for ${label}`}
                              onChange={(id) => handleAssign(employee.id, label, id)}
                            />
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              )}
            </div>

            {status === 'success' && (
              <Pager
                page={list.page}
                pageSize={list.pageSize}
                totalCount={data.totalCount}
                onPageChange={list.setPage}
                onPageSizeChange={list.setPageSize}
              />
            )}
          </section>
        </div>
      </div>

      <Toast toast={toast} />
    </div>
  )
}
