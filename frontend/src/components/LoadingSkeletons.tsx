import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import i18n from '@/i18n'

/**
 * Placeholders shaped like the content that's on its way, shown while a page's first load
 * is in flight. Each one is a single role="status" region with a visually hidden label, so
 * screen readers still hear "Loading buildings…" instead of a pile of empty divs.
 */

// Varied bar widths so the placeholder rows don't look like a barcode.
const WIDTHS = ['w-48', 'w-36', 'w-56', 'w-40', 'w-52', 'w-32']

function Status({ label, className, children }: { label: string; className?: string; children: React.ReactNode }) {
  return (
    <div role="status" aria-label={label} className={className}>
      <span className="sr-only">{label}</span>
      {children}
    </div>
  )
}

/** Rows of the admin tree lists (buildings, floors, spaces): icon, name, small meta. */
export function TreeSkeleton({ label, rows = 6 }: { label: string; rows?: number }) {
  return (
    <Status label={label}>
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="flex items-center gap-2.5 px-3 py-2.5">
          <Skeleton className="size-4 flex-none" />
          <Skeleton className={`h-4 ${WIDTHS[i % WIDTHS.length]}`} />
          <Skeleton className="h-3 w-12" />
        </div>
      ))}
    </Status>
  )
}

/** Rows of a shadcn <Table> card: a leading avatar/icon cell and a few text columns. */
export function TableSkeleton({ label, columns = 3, rows = 6 }: { label: string; columns?: number; rows?: number }) {
  return (
    <Status label={label} className="divide-y divide-border">
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="flex items-center gap-6 px-4 py-3">
          <div className="flex min-w-0 flex-1 items-center gap-3">
            <Skeleton className="size-8 flex-none rounded-full" />
            <div className="grid gap-1.5">
              <Skeleton className={`h-4 ${WIDTHS[i % WIDTHS.length]}`} />
              <Skeleton className="h-3 w-24" />
            </div>
          </div>
          {Array.from({ length: columns - 1 }, (_, c) => (
            <Skeleton key={c} className="hidden h-4 w-24 flex-1 sm:block" />
          ))}
        </div>
      ))}
    </Status>
  )
}

/** A stack of labelled fields — the constraints editor. */
export function FormSkeleton({ label, fields = 4 }: { label: string; fields?: number }) {
  return (
    <Status label={label} className="mt-6 grid gap-6">
      <div className="flex flex-wrap gap-3">
        {Array.from({ length: 3 }, (_, i) => (
          <Skeleton key={i} className="h-16 w-44" />
        ))}
      </div>
      {Array.from({ length: fields }, (_, i) => (
        <div key={i} className="grid gap-2">
          <Skeleton className="h-4 w-28" />
          <Skeleton className="h-9 w-full max-w-md" />
        </div>
      ))}
    </Status>
  )
}

/** Find a space's result cards: space icon + name, the day bar, the free label, Book. */
export function ResultsSkeleton({ label, rows = 4 }: { label: string; rows?: number }) {
  return (
    <Status label={label}>
      <Skeleton className="h-6 w-72" />
      <Skeleton className="mt-2 h-4 w-96 max-w-full" />
      <div className="mt-3 grid gap-2">
        {Array.from({ length: rows }, (_, i) => (
          <Card
            key={i}
            className="grid grid-cols-[1fr_auto] items-center gap-x-4 gap-y-2 px-4 pt-5 pb-3 lg:grid-cols-[minmax(200px,1.1fr)_2fr_minmax(150px,0.8fr)_auto]"
          >
            <div className="flex items-center gap-3">
              <Skeleton className="size-9 flex-none" />
              <div className="grid gap-1.5">
                <Skeleton className={`h-4 ${WIDTHS[i % WIDTHS.length]}`} />
                <Skeleton className="h-3 w-40" />
              </div>
            </div>
            <Skeleton className="col-span-2 h-6 lg:col-span-1" />
            <Skeleton className="hidden h-4 w-28 lg:block" />
            <Skeleton className="h-8 w-16" />
          </Card>
        ))}
      </div>
    </Status>
  )
}

/** Find a space before the building arrives: the search bar card, then the results. */
export function FindSpaceSkeleton() {
  return (
    <div>
      <Skeleton className="h-5 w-80 max-w-full" />
      <Card className="mt-6 grid grid-cols-2 gap-3 p-4 md:grid-cols-3 xl:grid-cols-6">
        {Array.from({ length: 6 }, (_, i) => (
          <div key={i} className="grid gap-2">
            <Skeleton className="h-4 w-16" />
            <Skeleton className="h-9 w-full" />
          </div>
        ))}
      </Card>
      <div className="mt-6">
        <ResultsSkeleton label={i18n.t('Common:LoadingBuilding')} />
      </div>
    </div>
  )
}

/** A short inline bar for single-line spots like the top bar's building name. */
export function TextSkeleton({ label, className = 'h-4 w-32' }: { label: string; className?: string }) {
  return (
    <span role="status" aria-label={label} className="inline-flex align-middle">
      <span className="sr-only">{label}</span>
      {/* A <span>, not <Skeleton>'s <div>, since this sits inside inline text. */}
      <span className={`animate-pulse rounded-md bg-muted ${className}`} />
    </span>
  )
}
