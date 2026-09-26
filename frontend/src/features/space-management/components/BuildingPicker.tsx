import { useEffect, useId, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { useDebouncedValue } from '../../../hooks/useDebouncedValue'
import { getBuilding, getBuildings } from '../api/spaceManagementApi'

const RESULT_LIMIT = 20

interface BuildingPickerProps {
  token: string
  /** Selected building id, or '' for the "none" option. */
  value: string
  /** The selected building's name when the caller already has it; otherwise it's fetched. */
  selectedName?: string | null
  /** Label of the '' option — "All buildings" for a filter, "Not assigned" for an assignment. */
  noneLabel: string
  ariaLabel: string
  onChange: (buildingId: string) => void
}

interface Option {
  id: string
  name: string
}

// Type-to-search building combobox. Searches the server (20 results at a time) instead of
// loading every building into a native <select>, which stops being usable past a few dozen
// options and was hard-capped at 1000.
export function BuildingPicker({ token, value, selectedName, noneLabel, ariaLabel, onChange }: BuildingPickerProps) {
  const listId = useId()
  const inputRef = useRef<HTMLInputElement>(null)

  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const debouncedQuery = useDebouncedValue(query, 200).trim()
  const [results, setResults] = useState<{ items: Option[]; totalCount: number } | null>(null)
  const [activeIndex, setActiveIndex] = useState(0)
  const [fetchedName, setFetchedName] = useState<{ id: string; name: string } | null>(null)

  const knownName = selectedName ?? (fetchedName?.id === value ? fetchedName.name : null)

  // A filter restored from the URL arrives as an id only — look its name up once.
  useEffect(() => {
    if (!value || selectedName || fetchedName?.id === value) return
    let cancelled = false
    getBuilding(token, value)
      .then((b) => !cancelled && setFetchedName({ id: value, name: b.name }))
      .catch(() => !cancelled && setFetchedName({ id: value, name: 'Unknown building' }))
    return () => {
      cancelled = true
    }
  }, [token, value, selectedName, fetchedName])

  useEffect(() => {
    if (!open) return
    let cancelled = false
    getBuildings(token, { filter: debouncedQuery || undefined, maxResultCount: RESULT_LIMIT })
      .then((r) => {
        if (cancelled) return
        setResults({ items: r.items.map((b) => ({ id: b.id, name: b.name })), totalCount: r.totalCount })
        setActiveIndex(0)
      })
      .catch(() => !cancelled && setResults({ items: [], totalCount: 0 }))
    return () => {
      cancelled = true
    }
  }, [token, open, debouncedQuery])

  // Index 0 is always the "none" option; results follow.
  const options: Option[] = [{ id: '', name: noneLabel }, ...(results?.items ?? [])]
  const displayText = open ? query : value ? (knownName ?? '…') : noneLabel

  function openPanel() {
    setQuery('')
    setResults(null)
    setOpen(true)
  }

  function close() {
    setOpen(false)
    setQuery('')
  }

  function choose(option: Option) {
    close()
    inputRef.current?.blur()
    if (option.id !== value) {
      if (option.id) setFetchedName({ id: option.id, name: option.name })
      onChange(option.id)
    }
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault()
      if (!open) return openPanel()
      const step = e.key === 'ArrowDown' ? 1 : -1
      setActiveIndex((i) => (i + step + options.length) % options.length)
    } else if (e.key === 'Enter' && open) {
      e.preventDefault()
      const option = options[activeIndex]
      if (option) choose(option)
    } else if (e.key === 'Escape' && open) {
      e.preventDefault()
      close()
    }
  }

  const hiddenCount = results ? results.totalCount - results.items.length : 0

  return (
    <div className="combo">
      <input
        ref={inputRef}
        className="ctrl"
        role="combobox"
        aria-label={ariaLabel}
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open ? `${listId}-${activeIndex}` : undefined}
        autoComplete="off"
        placeholder={open ? 'Type to search buildings…' : undefined}
        value={displayText}
        onFocus={openPanel}
        onBlur={close}
        onChange={(e) => {
          setQuery(e.target.value)
          if (!open) setOpen(true)
        }}
        onKeyDown={handleKeyDown}
      />
      {open && (
        // preventDefault on mousedown keeps focus in the input, so picking an option (or
        // dragging the panel's scrollbar) doesn't blur-and-close before the click lands.
        <div className="combo-panel" onMouseDown={(e) => e.preventDefault()}>
          <ul id={listId} role="listbox" aria-label={ariaLabel}>
            {options.map((option, i) => (
              <li
                key={option.id || 'none'}
                id={`${listId}-${i}`}
                role="option"
                aria-selected={option.id === value}
                className={`combo-option${i === activeIndex ? ' active' : ''}${option.id === '' ? ' none' : ''}`}
                onMouseEnter={() => setActiveIndex(i)}
                onClick={() => choose(option)}
              >
                {option.name}
              </li>
            ))}
          </ul>
          {results === null && <p className="combo-foot">Searching…</p>}
          {results !== null && results.items.length === 0 && <p className="combo-foot">No buildings match “{debouncedQuery}”.</p>}
          {hiddenCount > 0 && (
            <p className="combo-foot">
              {hiddenCount} more — keep typing to narrow down.
            </p>
          )}
        </div>
      )}
    </div>
  )
}
