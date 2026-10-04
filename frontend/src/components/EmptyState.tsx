import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { SearchXIcon, XIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

type EmptyStateProps = {
  icon: ReactNode
  title: string
  description?: string
  /** Usually one button: "Add building", "Clear filters". */
  action?: ReactNode
  className?: string
}

/**
 * What a list shows in place of rows: an icon, a line saying why it's empty, and what to
 * do next — over a faint grid that fades out towards the edges.
 */
export function EmptyState({ icon, title, description, action, className }: EmptyStateProps) {
  return (
    // role="status": a screen reader hears "No results found" when a search empties the list,
    // just as a sighted user sees the rows disappear.
    <div
      role="status"
      className={cn('relative isolate flex min-h-80 flex-col items-center justify-center overflow-hidden px-6 py-14 text-center', className)}
    >
      <GridBackdrop />
      <span className="mb-4 grid size-10 place-items-center rounded-lg border bg-card text-foreground shadow-sm [&_svg]:size-5">
        {icon}
      </span>
      <h3 className="text-sm font-semibold">{title}</h3>
      {description && <p className="mt-1 max-w-xs text-sm text-muted-foreground">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  )
}

/** The empty state for a search or filter that matched nothing, with a button that clears them. */
export function NoResults({ onClear }: { onClear: () => void }) {
  const { t } = useTranslation()
  return (
    <EmptyState
      icon={<SearchXIcon />}
      title={t('Common:NoResults')}
      description={t('Common:NoResultsHint')}
      action={
        <Button variant="outline" size="sm" onClick={onClear}>
          <XIcon />
          {t('Common:ClearFilters')}
        </Button>
      }
    />
  )
}

const CELL = 40
const COLUMNS = 13
const ROWS = 9
// A few cells filled in at fixed spots, so the grid looks like a texture rather than graph paper.
const SHADED: [column: number, row: number][] = [
  [1, 2], [3, 0], [4, 5], [2, 7], [5, 1], [7, 0], [8, 6], [9, 2], [10, 7], [11, 4], [6, 8], [0, 5],
]

function GridBackdrop() {
  const width = CELL * COLUMNS
  const height = CELL * ROWS
  return (
    <svg
      aria-hidden="true"
      viewBox={`0 0 ${width} ${height}`}
      width={width}
      height={height}
      className="pointer-events-none absolute top-1/2 left-1/2 -z-10 max-w-none -translate-x-1/2 -translate-y-1/2 text-border"
      style={{
        maskImage: 'radial-gradient(closest-side, black 20%, transparent)',
        WebkitMaskImage: 'radial-gradient(closest-side, black 20%, transparent)',
      }}
    >
      {SHADED.map(([column, row]) => (
        <rect key={`${column}-${row}`} x={column * CELL} y={row * CELL} width={CELL} height={CELL} fill="currentColor" opacity={0.6} />
      ))}
      <g stroke="currentColor" strokeWidth={1}>
        {Array.from({ length: COLUMNS + 1 }, (_, i) => (
          <line key={`c${i}`} x1={i * CELL + 0.5} y1={0} x2={i * CELL + 0.5} y2={height} />
        ))}
        {Array.from({ length: ROWS + 1 }, (_, i) => (
          <line key={`r${i}`} x1={0} y1={i * CELL + 0.5} x2={width} y2={i * CELL + 0.5} />
        ))}
      </g>
    </svg>
  )
}
