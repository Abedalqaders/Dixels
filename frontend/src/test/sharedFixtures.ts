import { existsSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

/**
 * Loads a JSON fixture from /shared/test-fixtures at the repo root — the same cases the
 * backend's Dixels.Domain.Tests suite is tested against (see SharedFixtureLoader.cs), so the
 * two implementations of the day/hour subset rules can't silently drift apart.
 */
export function loadSharedFixture<T>(fileName: string): T[] {
  const json = readFileSync(findFixtureFile(fileName), 'utf-8')
  return JSON.parse(json) as T[]
}

function findFixtureFile(fileName: string): string {
  const startDir = dirname(fileURLToPath(import.meta.url))
  let dir = startDir
  for (;;) {
    const candidate = join(dir, 'shared', 'test-fixtures', fileName)
    if (existsSync(candidate)) return candidate
    const parent = dirname(dir)
    if (parent === dir) {
      throw new Error(`Could not find shared test fixture '${fileName}' by walking up from '${startDir}'.`)
    }
    dir = parent
  }
}
