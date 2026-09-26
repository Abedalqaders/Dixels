import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useDebouncedValue } from './useDebouncedValue'

export const PAGE_SIZE_OPTIONS = [10, 25, 50, 100]
export const DEFAULT_PAGE_SIZE = 25

type ParamValue = string | number | boolean | null

/**
 * Page, page size, search and filters for a paged list, stored in the URL query string
 * rather than component state — so Back from a drill-down, a refresh, or a shared link all
 * land on the same page of the same filtered results. Page is 1-based in the URL (?page=3)
 * and 0-based here. Every change except paging itself resets to the first page.
 */
export function useListParams() {
  const [params, setParams] = useSearchParams()

  const page = Math.max(0, (parseInt(params.get('page') ?? '', 10) || 1) - 1)
  const requestedSize = parseInt(params.get('size') ?? '', 10)
  const pageSize = PAGE_SIZE_OPTIONS.includes(requestedSize) ? requestedSize : DEFAULT_PAGE_SIZE
  const search = params.get('q') ?? ''
  const showDeleted = params.get('deleted') === '1'

  function update(changes: Record<string, ParamValue>, resetPage = true) {
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev)
        for (const [key, value] of Object.entries(changes)) {
          if (value === null || value === '' || value === false) next.delete(key)
          else next.set(key, value === true ? '1' : String(value))
        }
        if (resetPage) next.delete('page')
        return next
      },
      // replace, not push: typing a search or flipping pages shouldn't bury the previous
      // screen under dozens of history entries.
      { replace: true },
    )
  }

  // The input needs to update on every keystroke, but the URL (and so the fetch) only once
  // typing pauses.
  const [searchInput, setSearchInput] = useState(search)
  const debouncedInput = useDebouncedValue(searchInput).trim()

  useEffect(() => {
    if (debouncedInput !== search) update({ q: debouncedInput })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedInput])

  // The URL changed from outside this input (Back/Forward) — reflect it in the box.
  useEffect(() => {
    if (search !== debouncedInput) setSearchInput(search)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search])

  return {
    page,
    pageSize,
    search,
    searchInput,
    showDeleted,
    setSearchInput,
    setPage: (value: number) => update({ page: value > 0 ? value + 1 : null }, false),
    setPageSize: (value: number) => update({ size: value === DEFAULT_PAGE_SIZE ? null : value }),
    setShowDeleted: (value: boolean) => update({ deleted: value }),
    getFilter: (key: string) => params.get(key) ?? '',
    setFilter: (key: string, value: string) => update({ [key]: value }),
  }
}
