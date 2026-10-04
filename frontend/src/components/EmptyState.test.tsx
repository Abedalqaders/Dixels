// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router-dom'
import { NoResults } from './EmptyState'
import { useListParams } from '@/hooks/useListParams'

// A list page in miniature: a search box, a filter in the URL, and the no-results state
// whenever either is set.
function FilteredList() {
  const list = useListParams()
  const location = useLocation()
  const filtered = list.search || list.getFilter('type')
  return (
    <>
      <input aria-label="Search" value={list.searchInput} onChange={(e) => list.setSearchInput(e.target.value)} />
      <span data-testid="query">{location.search}</span>
      {filtered && <NoResults onClear={() => list.clearFilters(['type'])} />}
    </>
  )
}

describe('NoResults', () => {
  it('says nothing matched, and Clear filters empties the search box and the filters', async () => {
    render(
      <MemoryRouter initialEntries={['/list?q=tower&type=desk&size=50']}>
        <FilteredList />
      </MemoryRouter>,
    )

    expect(screen.getByRole('status')).toHaveTextContent('No results found')
    expect(screen.getByLabelText('Search')).toHaveValue('tower')

    await userEvent.click(screen.getByRole('button', { name: 'Clear filters' }))

    expect(screen.getByLabelText('Search')).toHaveValue('')
    // Page size isn't a filter: it stays.
    expect(screen.getByTestId('query')).toHaveTextContent(/^\?size=50$/)
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('offers the given action in place of Clear filters', () => {
    render(<NoResults onClear={() => {}} action={<button>Add building</button>} />)

    expect(screen.getByRole('status')).toHaveTextContent("add it if it isn't here yet")
    expect(screen.getByRole('button', { name: 'Add building' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Clear filters' })).not.toBeInTheDocument()
  })
})
