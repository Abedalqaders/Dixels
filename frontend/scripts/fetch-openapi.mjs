// Pulls the API description from a running backend and keeps only this app's own endpoints
// (everything ABP adds — identity, permissions, settings — is left out). The result,
// src/lib/api/openapi.json, is committed; `npm run api:types` turns it into TypeScript
// (src/lib/api/schema.d.ts) that the hand-written DTO types are checked against in
// src/lib/api/schema.contract.test.ts. CI regenerates both and fails on any difference,
// so a backend DTO change can't drift away from the frontend types unnoticed.
//
// Usage: node scripts/fetch-openapi.mjs [api base url]
import { writeFileSync } from 'node:fs'

const base = process.argv[2] ?? process.env.VITE_API_BASE_URL ?? 'https://localhost:44334'
// The local backend runs on the ASP.NET dev certificate, which Node doesn't trust.
process.env.NODE_TLS_REJECT_UNAUTHORIZED ??= '0'

const response = await fetch(`${base}/swagger/v1/swagger.json`)
if (!response.ok) {
  throw new Error(`Could not fetch the Swagger document from ${base}: ${response.status} ${response.statusText}`)
}
const doc = await response.json()

// Our controllers all live under /api/app/ (see Dixels.HttpApi/Controllers).
const paths = Object.fromEntries(
  Object.entries(doc.paths)
    .filter(([path]) => path.startsWith('/api/app/'))
    .sort(([a], [b]) => a.localeCompare(b)),
)

// Every schema those endpoints reach, transitively.
const referenced = new Set()
function walk(node) {
  if (!node || typeof node !== 'object') return
  if (Array.isArray(node)) {
    node.forEach(walk)
    return
  }
  if (typeof node.$ref === 'string') {
    const name = node.$ref.replace('#/components/schemas/', '')
    if (!referenced.has(name)) {
      referenced.add(name)
      walk(doc.components.schemas[name])
    }
  }
  Object.values(node).forEach(walk)
}
walk(paths)

const schemas = Object.fromEntries([...referenced].sort().map((name) => [name, doc.components.schemas[name]]))

const trimmed = {
  openapi: doc.openapi,
  info: { title: 'Dixels API — application endpoints', version: doc.info.version },
  paths,
  components: { schemas },
}

writeFileSync(new URL('../src/lib/api/openapi.json', import.meta.url), JSON.stringify(trimmed, null, 2) + '\n')
console.log(`openapi.json: ${Object.keys(paths).length} paths, ${Object.keys(schemas).length} schemas from ${base}`)
