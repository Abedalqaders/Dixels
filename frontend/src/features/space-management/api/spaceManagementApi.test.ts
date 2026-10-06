import { afterEach, describe, expect, it, vi } from 'vitest'
import { getSpaceTypes } from './spaceManagementApi'
import type { SpaceTypeDto } from './spaceManagementApi'

afterEach(() => {
  vi.restoreAllMocks()
})

function types(from: number, count: number) {
  return Array.from({ length: count }, (_, i) => ({ id: `t${from + i}`, name: `Type ${from + i}` }) as SpaceTypeDto)
}

function page(items: SpaceTypeDto[], totalCount: number) {
  return new Response(JSON.stringify({ items, totalCount }))
}

function requestedParams(call: number) {
  return new URL(String(vi.mocked(globalThis.fetch).mock.calls[call][0])).searchParams
}

describe('getSpaceTypes', () => {
  it('reads every space type in one request when they fit a page', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(page(types(0, 12), 12))

    const result = await getSpaceTypes('token')

    expect(globalThis.fetch).toHaveBeenCalledTimes(1)
    expect(requestedParams(0).get('MaxResultCount') ?? requestedParams(0).get('maxResultCount')).toBe('1000')
    expect(result.items).toHaveLength(12)
  })

  it('keeps reading past the 1000 cap until it has them all', async () => {
    vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(page(types(0, 1000), 1500))
      .mockResolvedValueOnce(page(types(1000, 500), 1500))

    const result = await getSpaceTypes('token')

    expect(globalThis.fetch).toHaveBeenCalledTimes(2)
    expect(requestedParams(1).get('SkipCount') ?? requestedParams(1).get('skipCount')).toBe('1000')
    expect(result.items).toHaveLength(1500)
    expect(result.totalCount).toBe(1500)
  })

  it('stops if the list shrinks while it reads', async () => {
    vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(page(types(0, 1000), 1200))
      .mockResolvedValueOnce(page([], 900))

    const result = await getSpaceTypes('token')

    expect(globalThis.fetch).toHaveBeenCalledTimes(2)
    expect(result.items).toHaveLength(1000)
  })

  it('asks for just the page it is given', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(page(types(0, 10), 40))

    const result = await getSpaceTypes('token', { skipCount: 10, maxResultCount: 10 })

    expect(globalThis.fetch).toHaveBeenCalledTimes(1)
    expect(result.totalCount).toBe(40)
  })
})
