import { cn } from '@/lib/utils'

/** "Sara Haddad" → "SH"; "admin" → "A". */
export function initialsOf(name: string): string {
  return name
    .split(/[\s._-]+/)
    .filter(Boolean)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase()
}

/**
 * A person's round picture, or their initials on the brand colour when they have none.
 * Decorative: the name is always written next to it.
 */
export function UserAvatar({ name, pictureUrl, className }: { name: string; pictureUrl: string | null; className?: string }) {
  return (
    <span className={cn('av', className)} aria-hidden>
      {pictureUrl ? <img src={pictureUrl} alt="" /> : initialsOf(name)}
    </span>
  )
}
