import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import type { HhMm } from '../../../lib/time/buildingTime'

interface TimeSelectProps {
  id: string
  value: HhMm
  options: HhMm[]
  onChange: (time: HhMm) => void
}

/** A time on the slot grid. "24:00" reads as midnight so an end-of-day booking is obvious. */
export function TimeSelect({ id, value, options, onChange }: TimeSelectProps) {
  return (
    <Select value={value} onValueChange={onChange}>
      <SelectTrigger id={id} className="w-full font-mono">
        <SelectValue />
      </SelectTrigger>
      <SelectContent className="max-h-72">
        {options.map((t) => (
          <SelectItem key={t} value={t} className="font-mono">
            {t === '24:00' ? '24:00 (midnight)' : t}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
