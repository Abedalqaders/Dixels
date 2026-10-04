import { describe, expect, it } from 'vitest'
import { loadSharedFixture } from '@/test/sharedFixtures'
import { setLanguage } from '@/i18n'
import { allowedDays, describeDays, OperatingDays } from './operatingDays'
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

describe('allowedDays', () => {
  it("returns exactly the parent's own days", () => {
    const parent = OperatingDays.fromDayNames(['Monday', 'Wednesday', 'Friday'])

    expect(allowedDays(parent)).toEqual(['Monday', 'Wednesday', 'Friday'])
  })

  it('returns every day when the parent is Everyday', () => {
    expect(allowedDays(OperatingDays.Everyday)).toHaveLength(7)
  })

  it('returns no days when the parent is None', () => {
    expect(allowedDays(OperatingDays.None)).toEqual([])
  })
})

describe('describeDays', () => {
  const weekdays = OperatingDays.fromDayNames(['Monday', 'Tuesday', 'Wednesday'])

  it('names the days in English', () => {
    expect(describeDays(OperatingDays.Everyday)).toBe('Every day')
    expect(describeDays(OperatingDays.None)).toBe('None')
    expect(describeDays(weekdays)).toBe('Mon, Tue, Wed')
  })

  it('names the days in Arabic, with the Arabic comma', async () => {
    await setLanguage('ar')
    expect(describeDays(OperatingDays.Everyday)).toBe('كل يوم')
    expect(describeDays(weekdays)).toBe('الاثنين، الثلاثاء، الأربعاء')
  })
})
