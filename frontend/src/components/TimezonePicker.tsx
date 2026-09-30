import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CheckIcon, ChevronsUpDownIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { cn } from '@/lib/utils'

interface TimezonePickerProps {
  id?: string
  /** An IANA zone id, e.g. "Asia/Amman". */
  value: string
  onChange: (timezone: string) => void
  ariaLabel?: string
  className?: string
}

/** Where the browser can't list zones (very old engines), enough to keep the form usable. */
const FALLBACK_ZONES = ['UTC', 'Asia/Amman', 'Asia/Riyadh', 'Asia/Dubai', 'Africa/Cairo', 'Europe/London', 'America/New_York']

/** Every IANA zone the runtime knows — the same list the backend validates against. */
export function supportedTimezones(): string[] {
  try {
    const zones = Intl.supportedValuesOf('timeZone')
    return zones.includes('UTC') ? zones : ['UTC', ...zones]
  } catch {
    return FALLBACK_ZONES
  }
}

/** "Asia/Amman" → "Asia / Amman (GMT+3)" — searchable by either the id or the city. */
function describe(zone: string): string {
  try {
    const offset = new Intl.DateTimeFormat('en', { timeZone: zone, timeZoneName: 'shortOffset' })
      .formatToParts(new Date())
      .find((p) => p.type === 'timeZoneName')?.value
    return offset ? `${zone.replaceAll('_', ' ')} (${offset})` : zone.replaceAll('_', ' ')
  } catch {
    return zone
  }
}

/**
 * Type-to-search picker over every IANA time zone (400+), instead of a fixed short list
 * that can't express a building in Riyadh or Cairo. Shows each zone's current UTC offset
 * so "Asia/Amman" and "Asia/Riyadh" can be told apart at a glance.
 */
export function TimezonePicker({ id, value, onChange, ariaLabel, className }: TimezonePickerProps) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const zones = useMemo(() => supportedTimezones(), [])

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          aria-label={ariaLabel ?? t('Timezone:Label')}
          className={cn('w-full justify-between font-normal', className)}
        >
          <span className="truncate">{value ? describe(value) : t('Timezone:Choose')}</span>
          <ChevronsUpDownIcon className="opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0 sm:min-w-64" align="start">
        <Command>
          <CommandInput placeholder={t('Timezone:Search')} />
          <CommandList>
            <CommandEmpty>{t('Timezone:NoMatch')}</CommandEmpty>
            <CommandGroup>
              {zones.map((zone) => (
                <CommandItem
                  key={zone}
                  value={zone.replaceAll('_', ' ')}
                  onSelect={() => {
                    onChange(zone)
                    setOpen(false)
                  }}
                >
                  <CheckIcon className={cn(zone === value ? 'opacity-100' : 'opacity-0')} />
                  <span className="truncate">{describe(zone)}</span>
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  )
}
