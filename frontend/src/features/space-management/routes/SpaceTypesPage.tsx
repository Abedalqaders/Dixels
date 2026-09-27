import { useAuth } from 'react-oidc-context'
import { Sidebar } from '../../../components/Sidebar'
import { Toast, useToast } from '../../../components/Toast'
import { useAsync } from '../../../hooks/useAsync'
import { getSpaceTypes } from '../api/spaceManagementApi'
import { SpaceTypesManager } from '../components/SpaceTypesManager'
import '../../../styles/tokens.css'
import '../../../styles/base.css'
import '../../../styles/admin.css'

// Space types aren't part of the Building → Floor hierarchy, so this page sits outside
// SpaceManagementLayout — no explorer tree beside it, just the app nav.
export function SpaceTypesPage() {
  const auth = useAuth()
  const token = auth.user?.access_token ?? ''
  const { toast, showToast } = useToast()

  const { status, data, error, isRefreshing, refetch } = useAsync(
    async () => (await getSpaceTypes(token)).items,
    [token],
    { keepPreviousData: true },
  )

  return (
    <div className="app">
      <Sidebar />
      <div className="main">
        <div className="content">
          <div>
            <h1 className="pagetitle">Space types</h1>
            <p className="lead">
              Shared across every building — renaming or re-icon-ing a type updates it everywhere it's used.
            </p>
          </div>

          <section className={`card typespage${isRefreshing ? ' refreshing' : ''}`} aria-busy={isRefreshing}>
            {status === 'loading' && <p className="treeempty">Loading space types…</p>}
            {status === 'error' && <p className="treeempty">Couldn't load space types: {error.message}</p>}
            {status === 'success' && (
              <SpaceTypesManager
                token={token}
                spaceTypes={data}
                onChanged={refetch}
                onError={(message) => showToast(message, 'error')}
                onSuccess={(message) => showToast(message)}
              />
            )}
          </section>
        </div>
      </div>
      <Toast toast={toast} />
    </div>
  )
}
