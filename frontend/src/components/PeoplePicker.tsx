import { useId, useState } from 'react'
import type { FormEvent, KeyboardEvent, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Command as CommandPrimitive } from 'cmdk'
import { Mail, SearchIcon, X } from 'lucide-react'
import type { QueryKey } from '@tanstack/react-query'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Command, CommandGroup, CommandItem, CommandList } from '@/components/ui/command'
import { Popover, PopoverAnchor, PopoverContent } from '@/components/ui/popover'
import { FieldError } from '@/components/FieldError'
import { initialsOf } from '@/components/UserAvatar'
import { useApiQuery } from '@/hooks/useApiQuery'
import { useDebouncedValue } from '@/hooks/useDebouncedValue'
import { cn } from '@/lib/utils'

/** Someone picked: a colleague (with `userId`), or a guest known only by email. */
export interface PickedPerson {
  userId?: string | null
  name: string
  email: string
  isExternal: boolean
}

/** Someone the search found. */
export interface PersonMatch {
  id: string
  name: string
  email: string
}

interface PeoplePickerProps {
  /** Prefix for the ids inside, so two pickers on a page don't clash. */
  id: string
  value: PickedPerson[]
  onChange: (people: PickedPerson[]) => void
  /** Names the search's data (see lib/api/queryKeys), so a repeat search comes from the cache. */
  searchKey: (filter: string) => QueryKey
  search: (filter: string) => Promise<PersonMatch[]>
  /** Offer the "External guest" tab: anyone, by email and an optional name. */
  allowGuests?: boolean
  max?: number
  /** Something extra at the end of a person's row, before Remove — e.g. their answer to the invite. */
  rowExtra?: (person: PickedPerson) => ReactNode
}

const MIN_CHARS = 2
const DEBOUNCE_MS = 250
const MAX_EMAIL_LENGTH = 256
const MAX_NAME_LENGTH = 128
// Loose on purpose — the server has the final say; this only catches a half-typed address.
const EMAIL = /^[^\s@<>]+@[^\s@<>]+\.[^\s@<>]+$/

const sameEmail = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase()

/** Whether `person` is already in `people` — the same colleague, or the same email. */
export function isPicked(people: PickedPerson[], person: { userId?: string | null; email: string }): boolean {
  return people.some((p) => (person.userId && p.userId === person.userId) || (person.email && sameEmail(p.email, person.email)))
}

/**
 * Pick the people coming to something. Two tabs: "Colleague" searches people (from 2
 * characters, after a 250 ms pause), "External guest" takes an email and an optional name.
 * Everyone picked is listed under them — name, email and, for a guest, a "Guest" tag.
 *
 * Keyboard: in the search, arrows move through the matches and Enter adds; Backspace in an
 * empty search removes the last person; Enter in the guest fields adds the guest.
 */
export function PeoplePicker({ id, value, onChange, searchKey, search, allowGuests = false, max, rowExtra }: PeoplePickerProps) {
  const { t } = useTranslation()
  const [tab, setTab] = useState<'colleague' | 'guest'>('colleague')
  const full = max !== undefined && value.length >= max
  const showGuestTab = allowGuests && tab === 'guest'
  const tabsId = useId()

  const add = (person: PickedPerson) => onChange([...value, person])
  const remove = (index: number) => onChange(value.filter((_, i) => i !== index))

  return (
    <div className="grid gap-2">
      {allowGuests && (
        <div role="tablist" aria-label={t('People:HowToAdd')} className="flex w-fit max-w-full gap-0.5 rounded-lg bg-muted p-0.5">
          {(['colleague', 'guest'] as const).map((key) => (
            <button
              key={key}
              type="button"
              role="tab"
              id={`${tabsId}-${key}`}
              aria-selected={tab === key}
              aria-controls={`${tabsId}-panel`}
              onClick={() => setTab(key)}
              className={cn(
                'inline-flex items-center gap-1.5 rounded-md px-3 py-1 text-sm font-medium text-muted-foreground transition-colors',
                'focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none',
                tab === key && 'bg-background text-foreground shadow-xs',
              )}
            >
              {key === 'colleague' ? <SearchIcon className="size-3.5" aria-hidden="true" /> : <Mail className="size-3.5" aria-hidden="true" />}
              {key === 'colleague' ? t('People:TabColleague') : t('People:TabGuest')}
            </button>
          ))}
        </div>
      )}

      <div id={`${tabsId}-panel`} role={allowGuests ? 'tabpanel' : undefined} aria-labelledby={allowGuests ? `${tabsId}-${tab}` : undefined}>
        {full ? (
          <p className="text-sm text-muted-foreground">{t('People:Full', { max: max ?? 0 })}</p>
        ) : showGuestTab ? (
          <GuestFields id={id} people={value} onAdd={add} />
        ) : (
          <ColleagueSearch people={value} searchKey={searchKey} search={search} onAdd={add} onRemoveLast={() => value.length && remove(value.length - 1)} />
        )}
      </div>

      {value.length > 0 && (
        <ul className="grid rounded-md border" aria-label={t('People:Picked')}>
          {value.map((p, i) => (
            <li key={p.userId ?? `guest:${p.email.toLowerCase()}`} className="flex min-w-0 items-center gap-3 px-3 py-2 not-first:border-t">
              <Initials name={p.name || p.email} guest={p.isExternal} />
              <span className="grid min-w-0 flex-1">
                <span className="truncate text-sm font-medium">{p.name || p.email}</span>
                {p.email && (p.name || !p.isExternal) && (
                  <span className="truncate text-xs text-muted-foreground" dir="ltr">
                    {p.email}
                  </span>
                )}
              </span>
              {p.isExternal && <GuestTag />}
              {rowExtra?.(p)}
              <Button
                type="button"
                variant="ghost"
                size="icon-xs"
                className="text-muted-foreground"
                aria-label={t('People:Remove', { name: p.name || p.email })}
                onClick={() => remove(i)}
              >
                <X />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

interface ColleagueSearchProps {
  people: PickedPerson[]
  searchKey: (filter: string) => QueryKey
  search: (filter: string) => Promise<PersonMatch[]>
  onAdd: (person: PickedPerson) => void
  onRemoveLast: () => void
}

/** A search box with the matches in a popover under it — cmdk does the arrows and Enter. */
function ColleagueSearch({ people, searchKey, search, onAdd, onRemoveLast }: ColleagueSearchProps) {
  const { t } = useTranslation()
  const [text, setText] = useState('')
  const [open, setOpen] = useState(false)
  const filter = useDebouncedValue(text.trim(), DEBOUNCE_MS)
  const searching = filter.length >= MIN_CHARS
  const results = useApiQuery(searchKey(filter), () => search(filter), { enabled: searching, keepPreviousData: true })

  const matches = searching && results.status === 'success' ? results.data.filter((m) => !isPicked(people, { userId: m.id, email: m.email })) : []
  // Still waiting for the answer to what's typed now (or the pause before asking).
  const pending = text.trim().length >= MIN_CHARS && (filter !== text.trim() || results.status === 'loading' || results.isRefreshing)

  function pick(m: PersonMatch) {
    onAdd({ userId: m.id, name: m.name, email: m.email, isExternal: false })
    setText('')
    setOpen(false)
  }

  function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    // Arrows and Enter are cmdk's (on the Command around us; it prevents Enter's default, so
    // the form around us isn't submitted) — and it skips any key already prevented here.
    if (e.key === 'Backspace' && text === '') onRemoveLast()
    if (e.key === 'Escape' && open) {
      // Close the list only — not the dialog the picker sits in.
      e.preventDefault()
      e.stopPropagation()
      setOpen(false)
    }
  }

  let message: string | null = null
  if (text.trim().length < MIN_CHARS) message = t('People:TypeMore')
  else if (pending && matches.length === 0) message = t('People:Searching')
  else if (results.status === 'error') message = t('People:SearchFailed')
  else if (matches.length === 0) message = t('People:NoMatch', { text: text.trim() })

  return (
    // cmdk names the search box from `label` (a hidden <label> it points at), and gives it an id of its own.
    <Command shouldFilter={false} label={t('People:Search')} className="overflow-visible bg-transparent">
      <Popover open={open && text.trim() !== ''}>
        <PopoverAnchor asChild>
          <div
            className={cn(
              'flex h-9 items-center gap-2 rounded-md border border-input px-3 shadow-xs dark:bg-input/30',
              'focus-within:border-ring focus-within:ring-[3px] focus-within:ring-ring/50',
            )}
          >
            <SearchIcon className="size-4 shrink-0 opacity-50" aria-hidden="true" />
            <CommandPrimitive.Input
              value={text}
              onValueChange={(next) => {
                setText(next)
                setOpen(true)
              }}
              onKeyDown={handleKeyDown}
              onBlur={() => setOpen(false)}
              onFocus={() => text && setOpen(true)}
              placeholder={t('People:Search')}
              autoComplete="off"
              className="h-full w-full min-w-0 bg-transparent text-base outline-hidden placeholder:text-muted-foreground md:text-sm"
            />
          </div>
        </PopoverAnchor>
        <PopoverContent
          align="start"
          className="w-(--radix-popover-trigger-width) min-w-64 p-1"
          // The search box keeps the focus while the list is open.
          onOpenAutoFocus={(e) => e.preventDefault()}
          onCloseAutoFocus={(e) => e.preventDefault()}
          // A click on a match mustn't blur the box (and close the list) before it counts.
          onMouseDown={(e) => e.preventDefault()}
        >
          <CommandList>
            {matches.length > 0 && (
              <CommandGroup className="p-0">
                {matches.map((m) => (
                  <CommandItem key={m.id} value={m.id} onSelect={() => pick(m)}>
                    <Initials name={m.name} />
                    <span className="grid min-w-0">
                      <span className="truncate">{m.name}</span>
                      <span className="truncate text-xs text-muted-foreground" dir="ltr">
                        {m.email}
                      </span>
                    </span>
                  </CommandItem>
                ))}
              </CommandGroup>
            )}
            {message && <p className="px-2 py-1.5 text-sm text-muted-foreground">{message}</p>}
          </CommandList>
        </PopoverContent>
      </Popover>
    </Command>
  )
}

/** Email + optional name + Add. Not a <form>: it sits inside the booking form. */
function GuestFields({ id, people, onAdd }: { id: string; people: PickedPerson[]; onAdd: (person: PickedPerson) => void }) {
  const { t } = useTranslation()
  const [email, setEmail] = useState('')
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const errorId = `${id}-guest-error`

  function submit(e?: FormEvent | KeyboardEvent) {
    e?.preventDefault()
    const address = email.trim()
    if (!EMAIL.test(address)) return setError(t('People:EmailInvalid'))
    if (isPicked(people, { email: address })) return setError(t('People:AlreadyAdded'))
    onAdd({ userId: null, name: name.trim(), email: address, isExternal: true })
    setEmail('')
    setName('')
    setError(null)
    document.getElementById(`${id}-guest-email`)?.focus()
  }

  const enterAdds = (e: KeyboardEvent<HTMLInputElement>) => e.key === 'Enter' && submit(e)

  return (
    <div className="grid gap-2 rounded-md border border-dashed bg-[color-mix(in_srgb,var(--state-expired-soft)_30%,transparent)] p-3">
      <div className="grid gap-2 sm:grid-cols-[1.3fr_1fr]">
        <div className="grid gap-1.5">
          <Label htmlFor={`${id}-guest-email`}>{t('People:GuestEmail')}</Label>
          <Input
            id={`${id}-guest-email`}
            type="email"
            dir="ltr"
            inputMode="email"
            autoComplete="off"
            maxLength={MAX_EMAIL_LENGTH}
            placeholder="name@company.com"
            value={email}
            onChange={(e) => {
              setEmail(e.target.value)
              setError(null)
            }}
            onKeyDown={enterAdds}
            aria-invalid={error ? true : undefined}
            aria-describedby={error ? errorId : undefined}
          />
        </div>
        <div className="grid gap-1.5">
          <Label htmlFor={`${id}-guest-name`}>{t('People:GuestName')}</Label>
          <Input
            id={`${id}-guest-name`}
            autoComplete="off"
            maxLength={MAX_NAME_LENGTH}
            placeholder={t('People:Optional')}
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={enterAdds}
          />
        </div>
      </div>
      {error && <FieldError id={errorId} message={error} />}
      <Button type="button" size="sm" className="justify-self-end" onClick={() => submit()}>
        {t('People:AddGuest')}
      </Button>
    </div>
  )
}

/** A person's initials in a small circle — amber for a guest, the brand purple for a colleague. */
export function Initials({ name, guest = false }: { name: string; guest?: boolean }) {
  return (
    <span
      className={cn('av size-7! text-[11px]!', guest && 'bg-none! bg-[var(--state-expired-soft)]! text-[var(--state-expired-ink)]!')}
      aria-hidden="true"
    >
      {initialsOf(name.replace(/@.*/, ''))}
    </span>
  )
}

/** The small amber "Guest" tag beside someone invited by email. */
export function GuestTag() {
  const { t } = useTranslation()
  return (
    <span className="flex-none rounded-sm bg-[var(--state-expired-soft)] px-1.5 py-px text-[10px] font-semibold tracking-wide text-[var(--state-expired-ink)] uppercase">
      {t('People:Guest')}
    </span>
  )
}
