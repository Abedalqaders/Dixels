---
name: implement-task
description: Implement groomed Dixels steps in a worktree — test first, code, regenerate generated files, run every backend and frontend check. Use after groom-task, or to build a small agreed change.
---

# Implement task

1. **Worktree.** Create the task's worktree and branch from `origin/main` and set it up (`docs/development.md#a-worktree-per-task`). All edits happen there. Done when the worktree builds.

2. **Each step, in order.** Write its test and watch it go red for the right reason; write the code until it's green; tick the step. Code follows `docs/engineering-guidelines.md` and `docs/conventions.md` and reads like the file around it. A step that needs a change to the plan stops here: tell the user what and why before going on.

3. **Generated files.** For everything the change touched, regenerate and commit the generated file (`docs/conventions.md#generated-files-regenerate-never-hand-edit`): migration, API types (against this branch's own backend and database), i18n keys, feature boundaries. Every new error code or text is in every language file.

4. **All checks, green.**
   - Backend: build `Dixels.slnx` with `-warnaserror:CS8600,CS8602,CS8603,CS8604`; run Domain, EntityFrameworkCore and Web tests; run Postgres tests (`DIXELS_POSTGRES_TESTS=1`, Docker running) whenever SQL, a migration, a lock, an index or a race is involved. If Docker can't run, say so; don't skip silently.
   - Frontend: `npx tsc -b`, `npx oxlint --deny-warnings`, `npm run lint:boundaries`, `npx vitest run --maxWorkers=4`; `npm audit --omit=dev --audit-level=high` when a dependency changed; `npx vite build` when config or imports changed.
   - `dotnet ef migrations has-pending-model-changes --project src/Dixels.EntityFrameworkCore` (from `backend/`) is clean.

   Done when every command passed on the final code. Record each suite's pass count for the PR.

5. **Commit.** Stage with the line-ending check in `docs/conventions.md#line-endings-windows-coreautocrlftrue` (the two `--stat` numbers match); the line-ending-only change `lint:boundaries` leaves on `.oxlintrc.json` is reverted. One commit per logical step or one for the slice, message explaining why.
