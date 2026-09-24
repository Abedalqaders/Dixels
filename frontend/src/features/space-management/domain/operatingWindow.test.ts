import { describe, expect, it } from 'vitest'
import { loadSharedFixture } from '../../../test/sharedFixtures'
import { OperatingWindow } from './operatingWindow'

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
