import { afterEach, describe, expect, it, vi } from 'vitest'
import { randomUuid } from './uuid'

const UUID_V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/

describe('randomUuid', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('returns a version-4 UUID', () => {
    expect(randomUuid()).toMatch(UUID_V4)
  })

  it('still returns unique version-4 UUIDs where crypto.randomUUID is missing (plain http)', () => {
    vi.stubGlobal('crypto', { getRandomValues: crypto.getRandomValues.bind(crypto) })

    const ids = Array.from({ length: 50 }, () => randomUuid())

    ids.forEach((id) => expect(id).toMatch(UUID_V4))
    expect(new Set(ids).size).toBe(ids.length)
  })
})
