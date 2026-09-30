import { describe, expect, it } from 'vitest'
import { backendLanguageFiles } from '@/test/backendLocalization'

// Every language the app offers is a JSON file in the backend's Dixels resource, translated
// from en.json. English is also the fallback, so a key missing from another language would
// quietly ship as English in the middle of that language's screen — caught here instead.
// A new language file is picked up with no change to this test.

const placeholders = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort().join()

const [english, ...others] = backendLanguageFiles()

describe('backend language files', () => {
  it('English is there to translate from', () => {
    expect(english.code).toBe('en')
  })

  describe.each(others.map((file) => [file.code, file] as const))('%s', (_code, file) => {
    it('has every English key', () => {
      const missing = Object.keys(english.texts).filter((key) => !(key in file.texts))
      expect(missing).toEqual([])
    })

    it('has no key English lacks (a leftover or a typo)', () => {
      const extra = Object.keys(file.texts).filter((key) => !(key in english.texts))
      expect(extra).toEqual([])
    })

    it('fills in every text', () => {
      const empty = Object.entries(file.texts)
        .filter(([, text]) => text.trim() === '')
        .map(([key]) => key)
      expect(empty).toEqual([])
    })

    it('uses the same {placeholders} as English', () => {
      const mismatched = Object.entries(english.texts)
        .filter(([key, text]) => key in file.texts && placeholders(file.texts[key]) !== placeholders(text))
        .map(([key]) => key)
      expect(mismatched).toEqual([])
    })
  })
})
