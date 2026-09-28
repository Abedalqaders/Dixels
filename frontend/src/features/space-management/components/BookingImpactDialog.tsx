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
import type { BookingImpactDto } from '@/features/space-management/api/spaceManagementApi'

export type ImpactMode = 'change' | 'closure' | 'delete'
export type ImpactChoice = 'keep' | 'cancel' | null

export interface ImpactRequest {
  mode: ImpactMode
  impact: BookingImpactDto
  /** What's being deleted, for the delete wording. */
  subject?: string
}

interface BookingImpactDialogProps extends ImpactRequest {
  onChoose: (choice: ImpactChoice) => void
}

const plural = (n: number) => `${n} upcoming ${n === 1 ? 'booking' : 'bookings'}`

/**
 * What an admin sees before a change leaves bookings behind: how many, which (who, when,
 * where, why), and the choice — keep them (they were booked under the old rules) or cancel
 * them. A delete always cancels, so it only asks to confirm. Either way the people who
 * booked see on their calendar that an admin cancelled it, and why.
 */
export function BookingImpactDialog({ mode, impact, subject, onChoose }: BookingImpactDialogProps) {
  const n = impact.count
  const people = impact.assignedEmployees ?? 0
  const title =
    mode === 'delete'
      ? n > 0
        ? `Deleting “${subject}” cancels ${plural(n)}`
        : `Delete “${subject}”?`
      : mode === 'closure'
        ? `This closure falls on ${plural(n)}`
        : `This change affects ${plural(n)}`
  const action = mode === 'closure' ? 'add the closure' : 'save'

  return (
    <AlertDialog open onOpenChange={(open) => !open && onChoose(null)}>
      <AlertDialogContent className="sm:max-w-lg">
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>
            {mode === 'delete'
              ? n > 0
                ? "They'll be cancelled, and whoever booked them will see why on their calendar. Restoring later won't bring them back."
                : 'It can be restored later.'
              : 'They were booked under the current rules. Keep them as they are, or cancel them — whoever booked them will see why on their calendar.'}
          </AlertDialogDescription>
        </AlertDialogHeader>

        {people > 0 && (
          <p role="note" className="rounded-md bg-[var(--state-expired-soft)] px-3 py-2 text-sm text-[var(--state-expired-ink)]">
            <strong>
              {people} {people === 1 ? 'employee is' : 'employees are'} assigned to {subject}.
            </strong>{' '}
            They won't be able to book until you assign them to another building — or restore this one.
          </p>
        )}

        {n > 0 && (
        <ul className="grid max-h-64 gap-1.5 overflow-y-auto rounded-md border p-1.5" aria-label="Affected bookings">
          {impact.bookings.map((b) => (
            <li key={b.bookingId} className="grid gap-0.5 rounded px-2 py-1.5 text-sm odd:bg-muted/50">
              <span className="flex flex-wrap justify-between gap-x-3">
                <span className="font-medium">
                  {formatDate(dateOf(b.localStart))} ·{' '}
                  <span className="font-mono">
                    {timeOf(b.localStart)}–{timeOf(b.localEnd)}
                  </span>
                </span>
                <span className="text-muted-foreground">
                  {b.spaceName} · {b.floorName}
                </span>
              </span>
              <span className="text-xs text-muted-foreground">
                {b.bookedBy} · {b.title}
              </span>
              <span className="text-xs text-slot-closed-ink">{b.reasons.join(' · ')}</span>
            </li>
          ))}
        </ul>
        )}

        <AlertDialogFooter className="gap-2">
          <AlertDialogCancel>Go back</AlertDialogCancel>
          {mode !== 'delete' && (
            <Button variant="outline" onClick={() => onChoose('keep')}>
              Keep them and {action}
            </Button>
          )}
          <Button variant="destructive" onClick={() => onChoose('cancel')}>
            {mode === 'delete' ? (n > 0 ? `Delete and cancel ${n}` : 'Delete') : `Cancel ${n} and ${action}`}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
