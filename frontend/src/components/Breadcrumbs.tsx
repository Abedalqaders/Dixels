import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ChevronRightIcon } from 'lucide-react'
import { TextSkeleton } from './LoadingSkeletons'

export type Crumb = {
  /** Left out while it's still loading (a building's name, say): a grey bar stands in. */
  label?: ReactNode
  /** Where it leads. Left out for a section heading (Space management) that isn't a page itself. */
  to?: string
}

/**
 * Where the page sits: "Space management › Hierarchy › Riverside HQ". The last crumb is
 * the page itself, so it's never a link.
 */
export function Breadcrumbs({ items }: { items: Crumb[] }) {
  const { t } = useTranslation()
  if (items.length === 0) return null

  return (
    <nav aria-label={t('Nav:Breadcrumb')} className="crumbs">
      <ol>
        {items.map((item, i) => {
          const last = i === items.length - 1
          const label = item.label ?? <TextSkeleton label={t('Nav:BreadcrumbLoading')} className="h-4 w-24" />
          return (
            <li key={i}>
              {last ? (
                <span aria-current="page">{label}</span>
              ) : item.to ? (
                <Link to={item.to}>{label}</Link>
              ) : (
                <span>{label}</span>
              )}
              {!last && <ChevronRightIcon aria-hidden />}
            </li>
          )
        })}
      </ol>
    </nav>
  )
}
