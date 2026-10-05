import { request } from '@/lib/api/httpClient'

/**
 * What a new password must have — the admin's ABP Identity settings
 * (Abp.Identity.Password.*), which ABP shares with every signed-in user.
 */
export interface PasswordRules {
  requiredLength: number
  requiredUniqueChars: number
  requireDigit: boolean
  requireLowercase: boolean
  requireUppercase: boolean
  requireNonAlphanumeric: boolean
}

export type PasswordRule = 'length' | 'uniqueChars' | 'digit' | 'lowercase' | 'uppercase' | 'symbol'

/** The rules this admin has switched on, in the order My profile lists them. */
export function activeRules(rules: PasswordRules): PasswordRule[] {
  const active: PasswordRule[] = []
  if (rules.requiredLength > 0) active.push('length')
  if (rules.requireUppercase) active.push('uppercase')
  if (rules.requireLowercase) active.push('lowercase')
  if (rules.requireDigit) active.push('digit')
  if (rules.requireNonAlphanumeric) active.push('symbol')
  if (rules.requiredUniqueChars > 1) active.push('uniqueChars')
  return active
}

/*
 * The same checks as ASP.NET Identity's PasswordValidator, which the server runs: letters
 * and digits are ASCII only (a–z, A–Z, 0–9), so an Arabic letter counts as a symbol, and
 * the length is in UTF-16 units, as .NET counts it. The server stays the judge — this only
 * says in advance what it will say. shared/test-fixtures/password-rules-cases.json runs the
 * same cases through both.
 */
const isDigit = (c: string) => c >= '0' && c <= '9'
const isLower = (c: string) => c >= 'a' && c <= 'z'
const isUpper = (c: string) => c >= 'A' && c <= 'Z'

/** Which of the active rules `password` doesn't meet yet. */
export function unmetRules(password: string, rules: PasswordRules): PasswordRule[] {
  const chars = password.split('')
  const unmet: PasswordRule[] = []
  if (password.length < rules.requiredLength) unmet.push('length')
  if (rules.requireNonAlphanumeric && chars.every((c) => isUpper(c) || isLower(c) || isDigit(c))) unmet.push('symbol')
  if (rules.requireDigit && !chars.some(isDigit)) unmet.push('digit')
  if (rules.requireLowercase && !chars.some(isLower)) unmet.push('lowercase')
  if (rules.requireUppercase && !chars.some(isUpper)) unmet.push('uppercase')
  if (rules.requiredUniqueChars >= 1 && new Set(chars).size < rules.requiredUniqueChars) unmet.push('uniqueChars')
  return unmet
}

/** The rules, from ABP's application configuration (the same request any page can make). */
export async function getPasswordRules(token: string): Promise<PasswordRules> {
  const config = await request<{ setting: { values: Record<string, string | undefined> } }>(
    '/api/abp/application-configuration?includeLocalizationResources=false',
    token,
  )
  const value = (name: string) => config.setting.values[`Abp.Identity.Password.${name}`]
  const flag = (name: string) => value(name)?.toLowerCase() === 'true'
  return {
    requiredLength: Number(value('RequiredLength') ?? 0),
    requiredUniqueChars: Number(value('RequiredUniqueChars') ?? 1),
    requireDigit: flag('RequireDigit'),
    requireLowercase: flag('RequireLowercase'),
    requireUppercase: flag('RequireUppercase'),
    requireNonAlphanumeric: flag('RequireNonAlphanumeric'),
  }
}
