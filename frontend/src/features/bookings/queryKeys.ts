/** Bookings' query keys — also My calendar's, which shows the same bookings. See lib/api/queryKeys. */
export const bookingsKeys = {
  bookings: {
    all: ['bookings'] as const,
    myBuilding: () => ['bookings', 'my-building'] as const,
    search: (input: unknown) => ['bookings', 'search', input] as const,
    spaceDays: (spaceId: string, from: string, to: string) => ['bookings', 'space-days', spaceId, from, to] as const,
    detail: (id: string | null) => ['bookings', 'detail', id] as const,
  },
}
