---
name: groom-task
description: Break an approved Dixels plan slice into small ordered steps, each with the test that proves it and a check that says it's done. Use after plan approval, before writing code.
---

# Groom task

1. **Cut the slice into steps.** Each step is one coherent change a reviewer could read on its own: an entity and its migration; a domain rule; an app-service method; a DTO + regenerated types; a component. Order them so each step builds and its tests pass before the next starts (backend inside-out, then frontend).

2. **Give every step**:
   - **Files** it changes or adds.
   - **Test first**: the test(s) that fail before the step and pass after, at the level in the plan.
   - **Done when**: a command and its expected result (`dotnet test … --filter …` green, `npx vitest run <file>` green, `npm run api:types` shows only the new fields).

3. **Check coverage.** Done when every item of the plan's slice maps to at least one step, and every step has a runnable done-when check. List the steps as a checklist; it drives implement-task.
