import { useCallback, useState } from 'react'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'

export interface ConfirmRequest {
  title: string
  description?: string
  /** The button that does it — say what happens ("Delete floor"), not "OK". */
  confirmLabel: string
  cancelLabel?: string
  /** Red button for something that can't be undone or that cancels other people's bookings. */
  destructive?: boolean
}

/**
 * The app's "are you sure?" — an accessible dialog (focus trapped, Escape cancels, the
 * question is read out) instead of the browser's confirm(), which can't be styled, blocks
 * the whole tab and is silently suppressed by some browsers after the first few.
 *
 *   const { confirm, dialog } = useConfirm()
 *   ...
 *   if (!(await confirm({ title: 'Delete Level 3?', confirmLabel: 'Delete floor', destructive: true }))) return
 *   ...
 *   return <>{dialog} ...</>
 */
export function useConfirm() {
  const [pending, setPending] = useState<{ request: ConfirmRequest; resolve: (answer: boolean) => void } | null>(null)

  const confirm = useCallback(
    (request: ConfirmRequest) => new Promise<boolean>((resolve) => setPending({ request, resolve })),
    [],
  )

  const dialog = pending ? (
    <ConfirmDialog
      {...pending.request}
      onAnswer={(answer) => {
        pending.resolve(answer)
        setPending(null)
      }}
    />
  ) : null

  return { confirm, dialog }
}

export function ConfirmDialog({
  title,
  description,
  confirmLabel,
  cancelLabel = 'Cancel',
  destructive,
  onAnswer,
}: ConfirmRequest & { onAnswer: (answer: boolean) => void }) {
  return (
    <AlertDialog open onOpenChange={(open) => !open && onAnswer(false)}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          {description && <AlertDialogDescription>{description}</AlertDialogDescription>}
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel onClick={() => onAnswer(false)}>{cancelLabel}</AlertDialogCancel>
          <AlertDialogAction
            variant={destructive ? 'destructive' : 'default'}
            onClick={() => onAnswer(true)}
          >
            {confirmLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
