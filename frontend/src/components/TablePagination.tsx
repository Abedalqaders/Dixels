import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { ChevronLeftIcon, ChevronRightIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Pagination, PaginationContent, PaginationEllipsis, PaginationItem } from '@/components/ui/pagination'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { PAGE_SIZE_OPTIONS } from '@/hooks/useListParams'
import { pageSlots } from './Pager'

interface TablePaginationProps {
  /** Zero-indexed current page. */
  page: number
  pageSize: number
  totalCount: number
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}

/** The footer under a shadcn Table: "1–25 of 80", page buttons, rows-per-page. Same
 * behaviour as the older Pager (same page slots, same page sizes), built from shadcn parts. */
export function TablePagination({ page, pageSize, totalCount, onPageChange, onPageSizeChange }: TablePaginationProps) {
  const { t } = useTranslation()
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))

  // The current page can stop existing — the last row on the last page was deleted, or the
  // URL named a page past the end. Step back to the last real page instead of an empty table.
  useEffect(() => {
    if (page > pageCount - 1) onPageChange(pageCount - 1)
  }, [page, pageCount, onPageChange])

  if (totalCount === 0) return null

  const first = page * pageSize + 1
  const last = Math.min(totalCount, (page + 1) * pageSize)

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm text-muted-foreground">
      <span>{t('Pagination:Range', { first, last, total: totalCount })}</span>

      {pageCount > 1 && (
        <Pagination className="mx-0 w-auto">
          <PaginationContent>
            <PaginationItem>
              <Button variant="ghost" size="icon-sm" disabled={page <= 0} onClick={() => onPageChange(page - 1)} aria-label={t('Pagination:Previous')}>
                <ChevronLeftIcon className="rtl:-scale-x-100" />
              </Button>
            </PaginationItem>
            {pageSlots(page, pageCount).map((slot, i) => (
              <PaginationItem key={slot === 'gap' ? `gap-${i}` : slot}>
                {slot === 'gap' ? (
                  <PaginationEllipsis />
                ) : (
                  <Button
                    variant={slot === page ? 'outline' : 'ghost'}
                    size="icon-sm"
                    aria-current={slot === page ? 'page' : undefined}
                    aria-label={t('Pagination:Page', { page: slot + 1 })}
                    onClick={() => onPageChange(slot)}
                  >
                    {slot + 1}
                  </Button>
                )}
              </PaginationItem>
            ))}
            <PaginationItem>
              <Button
                variant="ghost"
                size="icon-sm"
                disabled={page >= pageCount - 1}
                onClick={() => onPageChange(page + 1)}
                aria-label={t('Pagination:Next')}
              >
                <ChevronRightIcon className="rtl:-scale-x-100" />
              </Button>
            </PaginationItem>
          </PaginationContent>
        </Pagination>
      )}

      <div className="flex items-center gap-2">
        <span>{t('Pagination:Rows')}</span>
        <Select value={String(pageSize)} onValueChange={(v) => onPageSizeChange(Number(v))}>
          <SelectTrigger size="sm" aria-label={t('Pagination:RowsPerPage')} className="w-20">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {PAGE_SIZE_OPTIONS.map((size) => (
              <SelectItem key={size} value={String(size)}>
                {size}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    </div>
  )
}
