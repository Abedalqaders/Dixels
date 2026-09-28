import { useCallback, useState } from 'react'
import { BookingImpactDialog } from '@/features/space-management/components/BookingImpactDialog'
import type { ImpactChoice, ImpactRequest } from '@/features/space-management/components/BookingImpactDialog'

/**
 * Lets an admin action stop and ask "these bookings no longer fit — keep or cancel?"
 * inline: `await ask(request)` resolves with the choice ('keep', 'cancel', or null for
 * "go back"), and `prompt` is the dialog to render somewhere on the page.
 */
export function useBookingImpactPrompt() {
  const [pending, setPending] = useState<{ request: ImpactRequest; resolve: (choice: ImpactChoice) => void } | null>(null)

  const ask = useCallback(
    (request: ImpactRequest) => new Promise<ImpactChoice>((resolve) => setPending({ request, resolve })),
    [],
  )

  const prompt = pending ? (
    <BookingImpactDialog
      {...pending.request}
      onChoose={(choice) => {
        pending.resolve(choice)
        setPending(null)
      }}
    />
  ) : null

  return { ask, prompt }
}
