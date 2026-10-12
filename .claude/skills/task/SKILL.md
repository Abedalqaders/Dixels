---
name: task
description: Take a Dixels task from idea to an open PR — plan, groom, implement, review, verify, finalize — pausing for the user's plan approval.
disable-model-invocation: true
---

# Task

Runs one task through the six stages, each its own skill. Invoke each with the Skill tool, in order; finish a stage's completion criterion before starting the next.

1. **plan-task** → a written plan. **Stop and wait** until the user approves it in their own words. A change they ask for means re-planning, then waiting again.
2. **groom-task** → the approved plan as small steps, each with a check.
3. **implement-task** → code and tests in a worktree, every check green.
4. **code-review-task** → the diff reviewed against plan and docs, findings fixed, a risk rating.
5. **verify-task** → the change seen working in the running app, screenshots for UI.
6. **finalize-task** → the PR open with risk, tests and screenshots, CI watched.

Then report the PR link and the risk rating, and stop. The user reviews and merges.

A plan of several slices runs stages 2–6 once per slice, one PR each; start the next slice only after the user has reviewed the previous PR.

For a change with no behaviour (docs, comments, a typo), stage 2 can be skipped and stage 5 reports "no runtime change". Every other stage still runs.
