# Development

Commands live in `frontend/package.json`, `backend/run-local.ps1` and `.github/workflows/ci.yml`; this page covers what those don't say.

## A worktree per task
Each task gets a worktree next to the main checkout, branched from `origin/main`:

```bash
git fetch origin
git worktree add -b feature/<name> ../Dixels-<name> origin/main
```

Then, inside the new worktree:
- **Frontend dependencies**: link the main checkout's `node_modules` instead of installing (`npm ci` hits EPERM while another dev server holds files):
  `New-Item -ItemType Junction -Path frontend\node_modules -Target ..\Dixels\frontend\node_modules`
- **Backend static libs**: copy `backend/src/Dixels.Web/wwwroot/libs` from the main checkout (git-ignored; without it every request is a 500 "Libs folder is missing").
- **Running it next to another checkout**: copy `backend/.env` and give it its own `DIXELS_API_PORT`, `DIXELS_MAIL_PORT`, `App__SelfUrl`, `App__CorsOrigins`, `Emails__AppUrl` and its own database (`Database=` in the connection string). A copy of the main database: `CREATE DATABASE "Dixels_<name>" TEMPLATE "Dixels"` (needs zero connections on `Dixels`). Frontend overrides go in `frontend/.env.development.local` (`VITE_API_BASE_URL`, `VITE_OIDC_AUTHORITY`, `DEV_PORT`). `frontend/.env.local` is tracked (LAN server values); leave it alone.
- **Sign-in on a new web port**: web ports 5173–5176 are registered by default. Another port needs `.\run-local.ps1 -Migrate` with `App__CorsOrigins` listing every port in use (it replaces the list), then a backend restart (OpenIddict caches the client).

Remove the worktree after its PR merges: `git worktree remove ../Dixels-<name>`.

## Running
- **Backend**: `backend\run-local.ps1` (API with `dotnet watch`; smtp4dev in Docker). `-Migrate` migrates and seeds this worktree's database first. Alternative: `docker compose up -d --build` in `backend/` (a plain restart reuses the old image).
- **Frontend**: `npm run dev` in `frontend/` (http://localhost:5173 unless `DEV_PORT` says otherwise).
- **Emails** land in smtp4dev: http://localhost:5000 (or this worktree's `DIXELS_MAIL_PORT`).
- **Sign in** with `Dixels__AdminEmail` / `Dixels__AdminPassword` from `backend/.env`. `Dixels__DemoData=true` seeds a demo building and three employees.

## Tests
```bash
# backend (from backend/)
dotnet build Dixels.slnx
dotnet test test/Dixels.Domain.Tests --no-build
dotnet test test/Dixels.EntityFrameworkCore.Tests --no-build
dotnet test test/Dixels.Web.Tests --no-build
DIXELS_POSTGRES_TESTS=1 dotnet test test/Dixels.Postgres.Tests --no-build   # needs Docker Desktop running

# one class while iterating
dotnet test test/Dixels.EntityFrameworkCore.Tests --filter "FullyQualifiedName~HierarchyCreateReplayTests"

# frontend (from frontend/)
npx tsc -b && npm run lint && npm run lint:boundaries && npx vitest run --maxWorkers=4
```
- Without `DIXELS_POSTGRES_TESTS=1` the Postgres tests are skipped, not run. CI always runs them.
- vitest can flake under full parallelism on Windows; `--maxWorkers=4` keeps it stable.

## Regenerating API types
`npm run api:types` reads Swagger from a running backend, and ABP needs a database to serve it. Run **this branch's** backend against a database migrated with **this branch's** migrations (its own database, never the shared one before the PR merges):

```bash
# create + migrate a throwaway database, start the API on a free port (Swagger is on in Development)
node scripts/fetch-openapi.mjs https://localhost:<port>
npx --yes -p openapi-typescript@7 openapi-typescript src/lib/api/openapi.json -o src/lib/api/schema.d.ts --root-types --alphabetize
```
Only the fields you changed should appear in the diff.

## Screenshots of the UI
- Drive the app with the Playwright MCP browser. It may only save inside the repo folder: save to `.playwright-mcp/<name>.png`. The browser profile is shared between sessions: call `browser_close` when done, and switch the app language back to English if you changed it.
- If the MCP browser is busy, drive your own Chromium over CDP instead.
- For a PR: commit the images to the orphan branch `pr-screenshots` under `<task>/`, push it, and embed them in the PR body as `![caption](https://github.com/Abedalqaders/Dixels/blob/pr-screenshots/<task>/<file>.png?raw=true)`.
- Show the change in both English and Arabic when it has text, and at phone width when it has layout.

## Pull requests
GitHub CLI: `"C:\Program Files\GitHub CLI\gh.exe"` (call it by full path from Git Bash), logged in to `github.com/Abedalqaders/Dixels`.
