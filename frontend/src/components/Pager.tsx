interface PagerProps {
  /** Zero-indexed current page. */
  page: number
  pageSize: number
  totalCount: number
  onPageChange: (page: number) => void
}

// Deliberately just Prev / "Page N of M" / Next — no numbered-page-list, no jump-to-page.
// None of the three list pages have enough realistic volume to need more than that, and
// there's no existing pagination UI in this codebase to match instead.
export function Pager({ page, pageSize, totalCount, onPageChange }: PagerProps) {
  // Guards against NaN, not just 0: totalCount/pageSize can come back as undefined or NaN
  // if a caller's data hasn't loaded yet, and `1/undefined` and `Math.max(1, NaN)` are both
  // NaN, not 1 — that quietly renders "Page 1 of NaN" instead of hiding or defaulting.
  if (!Number.isFinite(totalCount) || totalCount <= 0 || !Number.isFinite(pageSize) || pageSize <= 0) {
    return null
  }

  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))

  return (
    <div className="pager">
      <button
        type="button"
        className="btn sm sec"
        disabled={page <= 0}
        onClick={() => onPageChange(page - 1)}
      >
        Prev
      </button>
      <span className="m">
        Page {page + 1} of {pageCount}
      </span>
      <button
        type="button"
        className="btn sm sec"
        disabled={page >= pageCount - 1}
        onClick={() => onPageChange(page + 1)}
      >
        Next
      </button>
    </div>
  )
}
