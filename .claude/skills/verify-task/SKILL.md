---
name: verify-task
description: See a Dixels change working in the running app — run this branch's backend and frontend, walk the scenarios in a browser, take screenshots of UI changes. Use after review, before the PR.
---

# Verify task

1. **Run this branch.** Start this worktree's backend against its own database migrated with this branch's migrations, and its frontend (`docs/development.md#running`). Never point it at the shared database before the PR merges. Done when the app loads and you can sign in.

2. **Walk every scenario.** For each user-visible behaviour in the plan: the happy path, the main failure path (a rule refused, a conflict, an empty state), and anything the change could break next to it. Drive it in the browser (Playwright MCP) as the user would: real clicks and typing. Check the browser console and the backend log for errors. Done when each scenario was seen working, or a failure was found; a failure goes back to implement-task.

3. **Screenshots** (when the UI changed). Capture each state that shows the change, before and after where it helps; in English and in Arabic when there's text; at phone width when there's layout (`docs/development.md#screenshots-of-the-ui`). Name files by what they show (`add-floor-retry-same-floor.png`). Done when every changed screen has a screenshot.

4. **Close up.** `browser_close`, switch the app language back to English if you changed it, stop the servers you started. A change with no runtime effect reports "no runtime change" instead of steps 1–3.
