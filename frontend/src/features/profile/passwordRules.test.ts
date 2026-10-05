import { describe, expect, it } from 'vitest'
import { loadSharedFixture } from '@/test/sharedFixtures'
import { activeRules, unmetRules } from './passwordRules'
import type { PasswordRule, PasswordRules } from './passwordRules'

interface PasswordRulesCase {
  description: string
  password: string
  rules: PasswordRules
  unmet: PasswordRule[]
}

// The same cases the backend runs through ASP.NET Identity's own PasswordValidator
// (Dixels.Domain.Tests PasswordRulesFixtureTests), so the checklist can't drift from the server.
const cases = loadSharedFixture<PasswordRulesCase>('password-rules-cases.json')

describe('unmetRules (shared with the backend)', () => {
  it.each(cases.map((c) => [c.description, c] as const))('%s', (_, c) => {
    expect([...unmetRules(c.password, c.rules)].sort()).toEqual([...c.unmet].sort())
  })
})

describe('activeRules', () => {
  it('lists only the rules switched on, unique characters only when more than one is asked for', () => {
    const rules: PasswordRules = {
      requiredLength: 8,
      requiredUniqueChars: 1,
      requireDigit: true,
      requireLowercase: false,
      requireUppercase: true,
      requireNonAlphanumeric: false,
    }

    expect(activeRules(rules)).toEqual(['length', 'uppercase', 'digit'])
    expect(activeRules({ ...rules, requiredUniqueChars: 4 })).toContain('uniqueChars')
  })
})
