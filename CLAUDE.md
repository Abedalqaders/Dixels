# Dixels

Room and desk booking product: ABP (.NET 10) backend in `backend/`, React 19 frontend in `frontend/`, PostgreSQL.

## Read before you change code
- `docs/architecture.md`: layers, modules and their boundaries. Read it before adding a module, entity, endpoint or feature folder.
- `docs/engineering-guidelines.md`: ABP building blocks, errors and HTTP status, correctness defaults, how we test. Follow it for all new code.
- `docs/conventions.md`: style, localization, generated files, line endings, git and PRs.
- `docs/development.md`: worktrees, running the app, tests, regenerating API types, screenshots.
- `docs/decisions/`: why the code is shaped as it is. Check it before undoing a design.

## Rules
1. Work in a worktree on its own branch; `main` only moves by merged PRs.
2. Every change ships with tests at the level that can catch its bug: domain unit tests, EF integration tests, Postgres tests for SQL, locks and races, vitest for UI.
3. A new error code or UI text goes into every language file in the same change, and every generated file it affects is regenerated (API types, i18n keys, feature boundaries, migrations).
4. One PR per user-visible task, full-stack, with a risk rating. The user merges.
5. Secrets stay in `backend/.env`; code, logs, commits and PR text carry none.

## Workflow
A task runs through `/task`: plan-task → groom-task → implement-task → code-review-task → verify-task → finalize-task. The user approves the plan before code is written, and merges the PR.

When the same mistake happens twice, add a rule here or in `docs/`.
