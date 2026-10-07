import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { formatCount } from '@/lib/formatCount'
import { dateOf, formatDate, timeOf } from '@/lib/time/buildingTime'
import { formatClockRange } from '@/lib/time/format'
import type { ReservationImpactDto } from '@/lib/api/reservationImpact'

export type ImpactMode = 'change' | 'closure' | 'delete' | 'reassign'
export type ImpactChoice = 'keep' | 'cancel' | null

export interface ImpactRequest {
  mode: ImpactMode
  impact: ReservationImpactDto
  /** What's being deleted, for the delete wording. */
  subject?: string
  /**
   * The same preview again from `skip` on: the server sends the affected bookings a page at a
   * time, and "Show more" adds the next page. Without it only the first page is listed.
   */
  loadMore?: (skip: number) => Promise<ReservationImpactDto>
}

interface BookingImpactDialogProps extends ImpactRequest {
  onChoose: (choice: ImpactChoice) => void
}

/**
 * What an admin sees before a change leaves bookings behind: how many, which (who, when,
 * where, why), and the choice — keep them (they were booked under the old rules) or cancel
 * them. A delete or a move always cancels, so it only asks to confirm. Either way the people who
 * booked see on their calendar that an admin cancelled it, and why.
 *
 * A long list comes a page at a time: "Showing 50 of 1240" and "Show more". The count and the
 * buttons are always about all of them — keep or cancel acts on every one, shown or not.
 */
export function BookingImpactDialog({ mode, impact, subject, loadMore, onChoose }: BookingImpactDialogProps) {
  const { t } = useTranslation()
  const n = impact.count
  const [items, setItems] = useState(impact.items)
  const [loading, setLoading] = useState(false)
  const [loadFailed, setLoadFailed] = useState(false)
  const paged = n > impact.items.length

  async function showMore() {
    if (!loadMore) return
    setLoading(true)
    setLoadFailed(false)
    try {
      const next = await loadMore(items.length)
      // Rows can shift if bookings change between pages: never list one twice.
      setItems((shown) => {
        const seen = new Set(shown.map((b) => b.id))
        return [...shown, ...next.items.filter((b) => !seen.has(b.id))]
      })
    } catch {
      setLoadFailed(true)
    } finally {
      setLoading(false)
    }
  }

  const people = impact.assignedEmployees ?? 0
  const title =
    mode === 'delete'
      ? n > 0
        ? t('Hierarchy:ImpactDeleteTitle', { subject, count: n })
        : t('Hierarchy:DeleteTitle', { name: subject })
      : mode === 'closure'
        ? t('Hierarchy:ImpactClosureTitle', { count: n })
        : mode === 'reassign'
          ? t('Hierarchy:ImpactReassignTitle', { subject, count: n })
          : t('Hierarchy:ImpactChangeTitle', { count: n })
  // The two buttons that finish the job. A delete or a move never offers "keep".
  const keepLabel = mode === 'closure' ? t('Hierarchy:KeepAndAddClosure') : t('Hierarchy:KeepAndSave')
  const cancelLabel =
    mode === 'delete'
      ? n > 0
        ? t('Hierarchy:DeleteAndCancel', { count: n })
        : t('Common:Delete')
      : mode === 'reassign'
        ? t('Hierarchy:MoveAndCancel', { count: n })
        : mode === 'closure'
          ? t('Hierarchy:CancelAndAddClosure', { count: n })
          : t('Hierarchy:CancelAndSave', { count: n })

  return (
    <AlertDialog open onOpenChange={(open) => !open && onChoose(null)}>
      <AlertDialogContent className="sm:max-w-lg">
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>
            {mode === 'delete'
              ? n > 0
                ? t('Hierarchy:ImpactDeleteDetail')
                : t('Hierarchy:ImpactDeleteNoneDetail')
              : mode === 'reassign'
                ? t('Hierarchy:ImpactReassignDetail')
                : t('Hierarchy:ImpactChangeDetail')}
          </AlertDialogDescription>
        </AlertDialogHeader>

        {people > 0 && (
          <p role="note" className="rounded-md bg-[var(--state-expired-soft)] px-3 py-2 text-sm text-[var(--state-expired-ink)]">
            <strong>{t('Hierarchy:ImpactAssigned', { count: people, subject })}</strong> {t('Hierarchy:ImpactAssignedDetail')}
          </p>
        )}

        {n > 0 && (
        <ul className="grid max-h-64 gap-1.5 overflow-y-auto rounded-md border p-1.5" aria-label={t('Hierarchy:AffectedBookings')}>
          {items.map((b) => (
            <li key={b.id} className="grid gap-0.5 rounded px-2 py-1.5 text-sm odd:bg-muted/50">
              <span className="flex flex-wrap justify-between gap-x-3">
                <span className="font-medium">
                  {formatDate(dateOf(b.localStart))} ·{' '}
                  <span className="font-mono">
                    {formatClockRange(timeOf(b.localStart), timeOf(b.localEnd))}
                  </span>
                </span>
                <span className="text-muted-foreground">
                  {b.placeName} · {b.placeDetail}
                </span>
              </span>
              <span className="text-xs text-muted-foreground">
                {b.heldBy} · {b.title || t('Booking:Untitled')}
              </span>
              <span className="text-xs text-slot-closed-ink">{b.reasons.join(' · ')}</span>
            </li>
          ))}
        </ul>
        )}

        {paged && (
          <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
            <span aria-live="polite">{t('Hierarchy:ImpactShowing', { shown: formatCount(items.length), total: formatCount(n) })}</span>
            {loadMore && items.length < n && (
              <Button variant="ghost" size="sm" onClick={showMore} disabled={loading}>
                {loading ? t('Hierarchy:ImpactShowMoreLoading') : t('Hierarchy:ImpactShowMore')}
              </Button>
            )}
            {loadFailed && (
              <span role="alert" className="w-full text-destructive">
                {t('Hierarchy:ImpactShowMoreFailed')}
              </span>
            )}
          </div>
        )}

        <AlertDialogFooter className="gap-2">
          <AlertDialogCancel>{t('Hierarchy:GoBack')}</AlertDialogCancel>
          {mode !== 'delete' && mode !== 'reassign' && (
            <Button variant="outline" onClick={() => onChoose('keep')}>
              {keepLabel}
            </Button>
          )}
          <Button variant="destructive" onClick={() => onChoose('cancel')}>
            {cancelLabel}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
