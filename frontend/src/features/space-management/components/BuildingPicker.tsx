import { useEffect, useState } from 'react'
import { CheckIcon, ChevronsUpDownIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Command, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { cn } from '@/lib/utils'
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
  className?: string
}

interface Option {
  id: string
  name: string
}

// Type-to-search building combobox (shadcn Popover + Command). Searches the server, 20
// results at a time, instead of loading every building into a list — which stops being
// usable past a few dozen buildings. cmdk's own filtering is off (shouldFilter={false})
// because the server already did it.
export function BuildingPicker({ token, value, selectedName, noneLabel, ariaLabel, onChange, className }: BuildingPickerProps) {
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const debouncedQuery = useDebouncedValue(query, 200).trim()
  const [results, setResults] = useState<{ items: Option[]; totalCount: number } | null>(null)
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
      .then((r) => !cancelled && setResults({ items: r.items.map((b) => ({ id: b.id, name: b.name })), totalCount: r.totalCount }))
      .catch(() => !cancelled && setResults({ items: [], totalCount: 0 }))
    return () => {
      cancelled = true
    }
  }, [token, open, debouncedQuery])

  function handleOpenChange(next: boolean) {
    setOpen(next)
    if (next) {
      setQuery('')
      setResults(null)
    }
  }

  function choose(option: Option) {
    setOpen(false)
    if (option.id !== value) {
      if (option.id) setFetchedName({ id: option.id, name: option.name })
      onChange(option.id)
    }
  }

  const label = value ? (knownName ?? '…') : noneLabel
  const hiddenCount = results ? results.totalCount - results.items.length : 0
  // Index 0 is always the "none" option; results follow.
  const options: Option[] = [{ id: '', name: noneLabel }, ...(results?.items ?? [])]

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          aria-label={ariaLabel}
          className={cn('w-56 justify-between font-normal', !value && 'text-muted-foreground', className)}
        >
          <span className="truncate">{label}</span>
          <ChevronsUpDownIcon className="opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-64 p-0" align="start">
        <Command shouldFilter={false}>
          <CommandInput placeholder="Search buildings…" value={query} onValueChange={setQuery} />
          <CommandList>
            {results === null ? (
              <p className="py-6 text-center text-sm text-muted-foreground">Searching…</p>
            ) : (
              <>
                <CommandGroup>
                  {options.map((option) => (
                    <CommandItem
                      key={option.id || 'none'}
                      value={option.id || 'none'}
                      onSelect={() => choose(option)}
                      className={cn(option.id === '' && 'text-muted-foreground')}
                    >
                      <CheckIcon className={cn(option.id === value ? 'opacity-100' : 'opacity-0')} />
                      <span className="truncate">{option.name}</span>
                    </CommandItem>
                  ))}
                </CommandGroup>
                {/* Not CommandEmpty: the "none" option is always listed, so cmdk never sees an empty list. */}
                {results.items.length === 0 && debouncedQuery && (
                  <p className="border-t px-3 py-2 text-xs text-muted-foreground">No buildings match “{debouncedQuery}”.</p>
                )}
                {hiddenCount > 0 && (
                  <p className="border-t px-3 py-2 text-xs text-muted-foreground">
                    {hiddenCount} more — keep typing to narrow down.
                  </p>
                )}
              </>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  )
}
