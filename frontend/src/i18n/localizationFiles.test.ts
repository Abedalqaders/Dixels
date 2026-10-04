import { describe, expect, it } from 'vitest'
import { backendLanguageFiles } from '@/test/backendLocalization'

// Every language the app offers is a JSON file in the backend's Dixels resource, translated
// from en.json. English is also the fallback, so a key missing from another language would
// quietly ship as English in the middle of that language's screen — caught here instead.
// A new language file is picked up with no change to this test.
//
// Counted texts are plural keys, one per CLDR plural form of the language (i18next picks
// the form from `count`): English has "Rooms_one" and "Rooms_other", Arabic six forms
// ("_zero", "_one", "_two", "_few", "_many", "_other"). They're compared by their base key,
// and each language must have every form its own plural rules use.

const PLURAL_SUFFIX = /_(zero|one|two|few|many|other)$/

const placeholders = (text: string) => [...new Set([...text.matchAll(/\{(\w+)\}/g)].map((m) => m[1]))].sort()

const baseKey = (key: string) => key.replace(PLURAL_SUFFIX, '')

const pluralForms = (code: string) => new Intl.PluralRules(code).resolvedOptions().pluralCategories

const [english, ...others] = backendLanguageFiles()
const englishBases = new Set(Object.keys(english.texts).map(baseKey))
const englishPlurals = new Set(Object.keys(english.texts).filter((k) => PLURAL_SUFFIX.test(k)).map(baseKey))

describe('backend language files', () => {
  it('English is there to translate from', () => {
    expect(english.code).toBe('en')
  })

  describe.each([english, ...others].map((file) => [file.code, file] as const))('%s', (code, file) => {
    it('has every form of each counted text its plural rules need', () => {
      const missing = [...englishPlurals].flatMap((base) =>
        pluralForms(code)
          .map((form) => `${base}_${form}`)
          .filter((key) => !(key in file.texts)),
      )
      expect(missing).toEqual([])
    })
  })

  describe.each(others.map((file) => [file.code, file] as const))('%s', (_code, file) => {
    const bases = new Set(Object.keys(file.texts).map(baseKey))

    it('has every English key', () => {
      const missing = [...englishBases].filter((base) => !bases.has(base))
      expect(missing).toEqual([])
    })

    it('has no key English lacks (a leftover or a typo)', () => {
      const extra = [...bases].filter((base) => !englishBases.has(base))
      expect(extra).toEqual([])
    })

    it('fills in every text', () => {
      const empty = Object.entries(file.texts)
        .filter(([, text]) => text.trim() === '')
        .map(([key]) => key)
      expect(empty).toEqual([])
    })

    it('uses the same {placeholders} as English', () => {
      const mismatched = Object.entries(file.texts)
        .filter(([key, text]) => {
          const base = baseKey(key)
          if (englishPlurals.has(base)) {
            // A form may leave {count} out ("غرفتان" is "2 rooms"), but adds nothing new.
            const allowed = placeholders(english.texts[`${base}_other`])
            return placeholders(text).some((p) => !allowed.includes(p))
          }
          return key in english.texts && placeholders(text).join() !== placeholders(english.texts[key]).join()
        })
        .map(([key]) => key)
      expect(mismatched).toEqual([])
    })
  })
})
