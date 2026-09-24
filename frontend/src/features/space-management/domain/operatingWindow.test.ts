import { describe, expect, it } from 'vitest'
import { loadSharedFixture } from '../../../test/sharedFixtures'
import { allowedHoursRange, OperatingWindow } from './operatingWindow'

interface OperatingWindowSpec {
  isOpen24Hours: boolean
  open: string
  close: string
}

interface OperatingWindowCase {
  description: string
  parent: OperatingWindowSpec
  child: OperatingWindowSpec
  expected: boolean
}

const cases = loadSharedFixture<OperatingWindowCase>('operating-window-cases.json')

function toWindow(spec: OperatingWindowSpec): OperatingWindow {
  return spec.isOpen24Hours ? OperatingWindow.FullDay : new OperatingWindow(spec.open, spec.close)
}

describe('OperatingWindow.isSubsetOf', () => {
  it.each(cases)('$description', ({ parent, child, expected }) => {
    expect(toWindow(child).isSubsetOf(toWindow(parent))).toBe(expected)
  })
})

describe('allowedHoursRange', () => {
  it("returns the parent's own range as a plain data shape", () => {
    const parent = new OperatingWindow('07:00', '20:00')

    expect(allowedHoursRange(parent)).toEqual({ isOpen24Hours: false, open: '07:00', close: '20:00' })
  })

  it('reports isOpen24Hours for a full-day parent', () => {
    expect(allowedHoursRange(OperatingWindow.FullDay).isOpen24Hours).toBe(true)
  })
})
