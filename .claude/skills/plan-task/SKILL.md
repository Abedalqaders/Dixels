---
name: plan-task
description: Plan a Dixels change before any code — read the docs and the code it touches, write vertical slices with trade-offs and performance cost, get the user's approval. Use when starting a feature, fix or refactor.
---

# Plan task

1. **Ground it.** Read `docs/architecture.md`, `docs/engineering-guidelines.md`, the `docs/decisions/` records touching the area, and the code the change will touch (entities, app services, the frontend feature). Done when you can name every file that will change and every existing rule it must respect.

2. **Settle the user's decisions.** A choice with no conventional default and no answer in the docs (behaviour, UX, scope) is the user's: ask it, with options, a recommendation and its trade-off. Decide everything else yourself and say what you chose.

3. **Write the plan.** Split it into vertical slices (backend + frontend + tests, usable alone, one PR each); the thinnest end-to-end slice goes first. For each slice:
   - **Goal**: what the user can do afterwards.
   - **Backend / frontend**: the changes, by file or class.
   - **Tests**: which behaviours, at which level (unit, EF, Postgres, vitest), named as sentences.
   - **Trade-off**: what it costs versus the current approach or the alternative (a small table when comparing).
   - **Performance cost**: queries, requests, payload, indexes, locks.
   - **Expected risk**: Low / Medium / High / Critical, using the rubric in the code-review-task skill.
   - **Done when**: checkable outcomes.

   Cover the correctness defaults in `docs/engineering-guidelines.md` (transactions, concurrency, races, idempotency, soft delete, retroactive checks) wherever they apply, inside the slice, not as "later".

4. **Get approval.** Present the plan in short sections and stop. Done when the user has approved it in their own words.
