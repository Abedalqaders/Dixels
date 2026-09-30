import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { PAGE_SIZE_OPTIONS } from '@/hooks/useListParams'

interface PagerProps {
  /** Zero-indexed current page. */
  page: number
  pageSize: number
  totalCount: number
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}

type PageSlot = number | 'gap'

/** First, last, and the current page ±1, with 'gap' wherever pages are skipped — e.g.
 * 1 … 6 7 8 … 40. A gap hiding exactly one page shows that page instead. */
export function pageSlots(current: number, pageCount: number): PageSlot[] {
  const wanted = [...new Set([0, current - 1, current, current + 1, pageCount - 1])]
    .filter((p) => p >= 0 && p < pageCount)
    .sort((a, b) => a - b)

  const slots: PageSlot[] = []
  for (const p of wanted) {
    const previous = slots[slots.length - 1]
    if (typeof previous === 'number' && p - previous === 2) slots.push(p - 1)
    else if (typeof previous === 'number' && p - previous > 2) slots.push('gap')
    slots.push(p)
  }
  return slots
}

export function Pager({ page, pageSize, totalCount, onPageChange, onPageSizeChange }: PagerProps) {
  const { t } = useTranslation()
  const valid = Number.isFinite(totalCount) && totalCount >= 0 && Number.isFinite(pageSize) && pageSize > 0
  const pageCount = valid ? Math.max(1, Math.ceil(totalCount / pageSize)) : 1

  // The current page can stop existing — the last row on the last page was deleted, or the
  // URL named a page past the end. Step back to the last real page instead of showing an
  // empty list.
  useEffect(() => {
    if (valid && page > pageCount - 1) onPageChange(pageCount - 1)
  }, [valid, page, pageCount, onPageChange])

  if (!valid || totalCount === 0) return null

  const first = page * pageSize + 1
  const last = Math.min(totalCount, (page + 1) * pageSize)

  return (
    <nav className="pager" aria-label={t('Pagination:Label')}>
      <span className="pager-range">{t('Pagination:Range', { first, last, total: totalCount })}</span>

      {pageCount > 1 && (
        <div className="pager-pages">
          <button type="button" className="pagebtn" disabled={page <= 0} onClick={() => onPageChange(page - 1)} aria-label={t('Pagination:Previous')}>
            ‹
          </button>
          {pageSlots(page, pageCount).map((slot, i) =>
            slot === 'gap' ? (
              <span key={`gap-${i}`} className="pagegap" aria-hidden="true">
                …
              </span>
            ) : (
              <button
                key={slot}
                type="button"
                className={`pagebtn${slot === page ? ' on' : ''}`}
                aria-current={slot === page ? 'page' : undefined}
                aria-label={t('Pagination:Page', { page: slot + 1 })}
                onClick={() => onPageChange(slot)}
              >
                {slot + 1}
              </button>
            ),
          )}
          <button
            type="button"
            className="pagebtn"
            disabled={page >= pageCount - 1}
            onClick={() => onPageChange(page + 1)}
            aria-label={t('Pagination:Next')}
          >
            ›
          </button>
        </div>
      )}

      {totalCount > PAGE_SIZE_OPTIONS[0] && (
        <label className="pager-size">
          {t('Pagination:Rows')}
          <select value={pageSize} onChange={(e) => onPageSizeChange(Number(e.target.value))} aria-label={t('Pagination:RowsPerPage')}>
            {PAGE_SIZE_OPTIONS.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>
      )}
    </nav>
  )
}
