---
name: code-review-task
description: Review a Dixels branch's diff against its plan, the architecture and the guidelines, fix what it finds, and rate the change's risk. Use before opening a PR, or when asked to review or rate a branch.
---

# Code review task

1. **Review with fresh eyes.** Dispatch a subagent with: the plan (or the PR description), the base (`origin/main`), and the checklist below. Fresh context is the point: it reads the diff the way the user will. Done when it reports on **every changed file**, with `file:line` for each finding.

2. **The checklist.**
   - **Spec**: every plan item is implemented; nothing beyond it is.
   - **Architecture** (`docs/architecture.md`): layer rules; no new reference from SpaceManagement/Users to Bookings (the known exception is listed there); cross-module effects go through events or `IReservationImpactProvider`; frontend feature boundaries.
   - **Guidelines** (`docs/engineering-guidelines.md`): ABP building blocks; error codes and their HTTP status; transactions, concurrency, races, idempotency, soft-delete-aware indexes; N+1 and missing indexes.
   - **Security**: every new endpoint authorizes (permission attribute, building access); input validated; a user can't reach another user's or building's data; no secret in code, logs or text.
   - **Tests**: every behaviour change has a test at a level that can catch it; races and SQL are proven on Postgres; tests assert the behaviour (not weakened to pass); the failure paths are tested too.
   - **Conventions**: localization in every language file; generated files regenerated; line endings intact; names and comments match the code around them.

3. **Fix.** Fix every finding that's a bug, a broken rule or a missing test, then re-run the checks from implement-task. A finding you choose not to fix goes in the PR under "Known gaps", with the reason. Done when no finding is unresolved and unlisted.

4. **Rate the risk.** Rate by the highest row that applies, then write the reason, what could break, and how to roll back.

| Rating | Applies when the change… |
|---|---|
| 🟢 **Low** | touches only UI text or style, tests, docs or comments; no behaviour a user relies on changes |
| 🟡 **Medium** | adds an endpoint or DTO field, changes logic inside one feature, adds a UI flow; no schema change |
| 🟠 **High** | adds a migration; adds or changes a lock, transaction or concurrency rule; changes booking rules, cancellations or anything that emails people; crosses module boundaries; changes background jobs |
| 🔴 **Critical** | touches authentication, permissions or building access; deletes or rewrites existing data (migration with data changes, bulk updates); changes secrets, certificates or the deploy setup |

Done when the rating, its reason, the blast radius and the rollback step are written down for finalize-task.
