# Conventions

## Code style
- **Match the file you're in**: its naming, its idiom, its comment density.
- **Comments explain why**, in plain sentences, on the class or the non-obvious line. The surrounding code is the model: `/// <summary>` on public types and members, short `//` lines above the tricky part.
- **Names say what the thing is in the product's words**: `CancelOwnAsync`, `IsOpenForAnswers`, `HierarchyCreateReplays`.
- **Test names are sentences**: `A_booking_cannot_be_cancelled_twice` (C#), `it('sends a retry after an error with the same key')` (TS).
- **Frontend**: server state through `useApiQuery(key, fetcher)` with a key from `queryKeys` (the token never in a key); mutations call `emitBookingsChanged()` / `notifyHierarchyChanged()`; dates through `lib/time/format`; destructive actions ask through `useConfirm()`; feedback through `useToast()`; dialogs are shadcn; RTL-safe logical classes (`ms-`, `border-s`, `insetInlineStart`).

## Localization (en + ar, built to grow to ~10 languages)
- UI texts live in the **backend**: `backend/src/Dixels.Domain.Shared/Localization/Dixels/<lang>.json`. The frontend loads them at runtime.
- Every key goes in **every** language file. Counted texts are i18next plural keys (`Key_one/_other` in en; `_zero/_one/_two/_few/_many/_other` in ar).
- After changing `en.json`, run `npm run i18n:types` in `frontend/` and commit `src/i18n/keys.d.ts`. CI fails on a stale file.
- Names of buildings, floors, spaces and space types are stored per language (`*Translation` rows, every language including English); other entities' free text (booking titles, closure reasons) isn't translated.
- Arabic wording: constraints = القيود, rules = القواعد.

## Generated files: regenerate, never hand-edit
| You changed | Run | Commit |
|---|---|---|
| A DTO or endpoint | `npm run api:types` against **this branch's** backend (see [development.md](development.md#regenerating-api-types)) | `frontend/src/lib/api/openapi.json`, `schema.d.ts` |
| `en.json` keys | `npm run i18n:types` | `frontend/src/i18n/keys.d.ts` |
| A feature's public files | `npm run lint:boundaries` | `frontend/.oxlintrc.json` |
| An entity or EF model | `dotnet ef migrations add <Name>` in `backend/src/Dixels.EntityFrameworkCore` | the migration, its Designer, the snapshot |

CI regenerates the first three and fails on any difference, and fails when the model has changes no migration covers. Check that locally from `backend/`: `dotnet ef migrations has-pending-model-changes --project src/Dixels.EntityFrameworkCore`.

## Line endings (Windows, `core.autocrlf=true`)
Many backend files are CRLF in the repository, and `en.json` / `ar.json` are CRLF blobs with a few `\r\r\n` lines.
- Edit them without changing their line endings (`sed -b`, or a script that keeps `\r\n`).
- Stage the language files with `git -c core.autocrlf=false add …`.
- Before committing, `git diff --cached --stat` and `git diff --cached --ignore-cr-at-eol --stat` must show the **same** numbers; otherwise a whole file was rewritten.
- `npm run lint:boundaries` rewrites `.oxlintrc.json` with LF; if nothing else changed, `git checkout` it.

## Git and PRs
- **Never commit on `main`.** Every task gets its own worktree and branch (see [development.md](development.md#a-worktree-per-task)). Branch names: `feature/…`, `fix/…`, `refactor/…`, `docs/…`.
- **One PR per user-visible task**, full-stack (backend + frontend + tests), usable on its own.
- A PR body has: what and why, how, trade-offs, a **risk rating**, tests run, and screenshots for UI changes. The `finalize-task` skill writes it.
- Screenshots go on the orphan branch `pr-screenshots` (a folder per task) and are embedded in the PR body; never on the PR branch.
- No secrets in code, logs, commits or PR text. Local secrets live in `backend/.env` (template: `.env.example`).
