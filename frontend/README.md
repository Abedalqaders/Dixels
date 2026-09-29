# Dixels frontend

React 19 + Vite + TypeScript. Admin (hierarchy, space types, constraints, users) and employee (find a space, my calendar) UI for the Dixels API.

```bash
cp .env.example .env      # API + OIDC URLs; the defaults point at the local backend
npm install
npm run dev               # http://localhost:5173
```

| Script | What it does |
|---|---|
| `npm run dev` | Vite dev server |
| `npm run build` | Typecheck (`tsc -b`) then a production bundle. Requires `VITE_API_BASE_URL`. |
| `npm run lint` | oxlint (`--deny-warnings` in CI) |
| `npm test` | vitest, once |
| `npm run api:types` | Pulls the Swagger document from a running backend into `src/lib/api/openapi.json` and generates `src/lib/api/schema.d.ts`. `src/lib/api/schema.contract.test.ts` checks the hand-written DTO types against it; CI regenerates both and fails on drift. |

## Layout

- `src/app` — routes (a data router, so pages can block navigation on unsaved edits).
- `src/features/<feature>/{api,components,hooks,routes}` — one folder per feature; `api/*.ts` holds the typed calls and DTO types.
- `src/lib/api` — the fetch wrapper (ABP error envelope, 401 → silent renew → retry), the shared query cache and its keys.
- `src/lib/time` — building-clock date maths and the one formatting module every label uses.
- `src/components` — app-wide pieces (toaster, confirm dialog, timezone picker, error boundary) and `ui/` (shadcn).
- `src/test` — test providers and helpers; `shared/test-fixtures` at the repo root is read by both the C# and the TS suites.

## Conventions

- Server state goes through `useApiQuery` with a key from `queryKeys`; the access token is never part of a key.
- Mutations call `emitBookingsChanged()` / `notifyHierarchyChanged()` so every page showing that data refreshes.
- User-facing dates use `lib/time/format` (24-hour clock, `Tue 29 Sep` style), never `toLocaleDateString` or date-fns directly.
- Destructive actions ask through `useConfirm()`; feedback goes through `useToast()`.
