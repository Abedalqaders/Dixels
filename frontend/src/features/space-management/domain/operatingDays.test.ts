import { describe, expect, it } from 'vitest'
import { loadSharedFixture } from '../../../test/sharedFixtures'
import { OperatingDays } from './operatingDays'
import type { DayName } from './operatingDays'

interface OperatingDaysCase {
  description: string
  parent: DayName[]
  child: DayName[]
  expected: boolean
}

const cases = loadSharedFixture<OperatingDaysCase>('operating-days-cases.json')

describe('OperatingDays.isSubsetOf', () => {
  it.each(cases)('$description', ({ parent, child, expected }) => {
    const parentDays = OperatingDays.fromDayNames(parent)
    const childDays = OperatingDays.fromDayNames(child)

    expect(childDays.isSubsetOf(parentDays)).toBe(expected)
  })
})
