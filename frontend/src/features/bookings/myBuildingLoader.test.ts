import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { LoaderFunctionArgs } from 'react-router-dom'
import { createQueryClient } from '@/lib/api/queryClient'
import { queryKeys } from '@/lib/api/queryKeys'
import { userManager } from '@/features/auth/userManager'
import { getMyBookableBuilding } from '@/features/bookings/api/bookingsApi'
import { myBuildingLoader } from './myBuildingLoader'

vi.mock('@/features/auth/userManager', () => ({ userManager: { getUser: vi.fn() } }))
vi.mock('@/features/bookings/api/bookingsApi', () => ({ getMyBookableBuilding: vi.fn() }))

const building = { id: 'b1', name: 'Riverside HQ' }
const args = {} as LoaderFunctionArgs

describe('myBuildingLoader', () => {
  beforeEach(() => {
    vi.mocked(getMyBookableBuilding).mockReset().mockResolvedValue(building as never)
  })

  it("puts the building in the cache under the pages' own key, with the signed-in user's token", async () => {
    vi.mocked(userManager.getUser).mockResolvedValue({ access_token: 'tok', expired: false } as never)
    const queryClient = createQueryClient()

    await myBuildingLoader(queryClient)(args)
    await vi.waitFor(() => expect(queryClient.getQueryData(queryKeys.bookings.myBuilding())).toEqual(building))

    expect(getMyBookableBuilding).toHaveBeenCalledOnce()
    expect(getMyBookableBuilding).toHaveBeenCalledWith('tok')
  })

  it("doesn't wait for the building: the page never waits on the head start", async () => {
    vi.mocked(userManager.getUser).mockResolvedValue({ access_token: 'tok', expired: false } as never)
    vi.mocked(getMyBookableBuilding).mockReturnValue(new Promise(() => {}))

    await expect(myBuildingLoader(createQueryClient())(args)).resolves.toBeNull()
  })

  it("doesn't ask again when the building is already fresh in the cache", async () => {
    vi.mocked(userManager.getUser).mockResolvedValue({ access_token: 'tok', expired: false } as never)
    const queryClient = createQueryClient()
    queryClient.setQueryData(queryKeys.bookings.myBuilding(), building)

    await myBuildingLoader(queryClient)(args)

    expect(getMyBookableBuilding).not.toHaveBeenCalled()
  })

  it.each([
    ['signed out', null],
    ['an expired token', { access_token: 'old', expired: true }],
  ])('leaves it to the page with %s', async (_, user) => {
    vi.mocked(userManager.getUser).mockResolvedValue(user as never)

    await myBuildingLoader(createQueryClient())(args)

    expect(getMyBookableBuilding).not.toHaveBeenCalled()
  })
})
