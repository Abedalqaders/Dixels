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
import { dateOf, formatDate, timeOf } from '@/lib/time/buildingTime'
import type { ReservationImpactDto } from '@/lib/api/reservationImpact'

export type ImpactMode = 'change' | 'closure' | 'delete' | 'reassign'
export type ImpactChoice = 'keep' | 'cancel' | null

export interface ImpactRequest {
  mode: ImpactMode
  impact: ReservationImpactDto
  /** What's being deleted, for the delete wording. */
  subject?: string
}

interface BookingImpactDialogProps extends ImpactRequest {
  onChoose: (choice: ImpactChoice) => void
}

/**
 * What an admin sees before a change leaves bookings behind: how many, which (who, when,
 * where, why), and the choice — keep them (they were booked under the old rules) or cancel
 * them. A delete or a move always cancels, so it only asks to confirm. Either way the people who
 * booked see on their calendar that an admin cancelled it, and why.
 */
export function BookingImpactDialog({ mode, impact, subject, onChoose }: BookingImpactDialogProps) {
  const { t } = useTranslation()
  const n = impact.count
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
          {impact.items.map((b) => (
            <li key={b.id} className="grid gap-0.5 rounded px-2 py-1.5 text-sm odd:bg-muted/50">
              <span className="flex flex-wrap justify-between gap-x-3">
                <span className="font-medium">
                  {formatDate(dateOf(b.localStart))} ·{' '}
                  <span className="font-mono">
                    {timeOf(b.localStart)}–{timeOf(b.localEnd)}
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
