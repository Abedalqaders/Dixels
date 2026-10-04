import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'

const RELATIVE = join('backend', 'src', 'Dixels.Domain.Shared', 'Localization', 'Dixels')

/** backend/src/Dixels.Domain.Shared/Localization/Dixels — every language the app has.
 * Found by walking up from where the tests run (import.meta.url isn't a file path under
 * jsdom), so it works from frontend/ and from the repository root alike. */
function findLocalizationDir(): string {
  let dir = process.cwd()
  for (;;) {
    const candidate = join(dir, RELATIVE)
    if (existsSync(candidate)) return candidate
    const parent = dirname(dir)
    if (parent === dir) throw new Error(`Couldn't find ${RELATIVE} above ${process.cwd()}`)
    dir = parent
  }
}

export interface BackendLanguageFile {
  code: string
  texts: Record<string, string>
}

/** Every <code>.json in the backend's Dixels resource, English first. */
export function backendLanguageFiles(): BackendLanguageFile[] {
  const localizationDir = findLocalizationDir()
  return readdirSync(localizationDir)
    .filter((name) => name.endsWith('.json'))
    .map((name) => {
      const { culture, texts } = JSON.parse(readFileSync(join(localizationDir, name), 'utf8')) as {
        culture: string
        texts: Record<string, string>
      }
      return { code: culture, texts }
    })
    .sort((a, b) => (a.code === 'en' ? -1 : b.code === 'en' ? 1 : a.code.localeCompare(b.code)))
}
