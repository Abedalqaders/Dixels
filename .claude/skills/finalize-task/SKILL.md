---
name: finalize-task
description: Open the PR for a reviewed and verified Dixels branch — screenshots embedded, risk rating, tests run, the user's checklist — and watch CI. Use as the last stage of a task.
---

# Finalize task

1. **Screenshots.** Commit verify-task's images to the orphan branch `pr-screenshots` under `<branch-name>/` and push it (`docs/development.md#screenshots-of-the-ui`). Done when each image URL opens.

2. **Push and open the PR** against `main` with `gh pr create --body-file`, body in this shape:

   ```markdown
   ## What
   <the problem, in the user's words; what they can do now>

   ## How
   <the approach, by layer; the files that matter>

   ## Risk: <🟢 Low | 🟡 Medium | 🟠 High | 🔴 Critical>
   - **Why:** <the rubric row that applies>
   - **Could break:** <blast radius>
   - **Roll back:** <revert the PR / down-migration / config switch>

   ## Trade-offs
   <cost versus the alternative; performance cost>

   ## Tests
   - <new tests, by behaviour>
   - Local: <suite: passed counts>; Postgres: <ran / not run and why>

   ## Screenshots
   <embedded images, or "No UI change">

   ## Known gaps
   <review findings left open, with reasons; or "None">

   ## Before you merge
   - [ ] Every line of the diff makes sense to you
   - [ ] It fits the architecture and the decisions
   - [ ] It solves the real problem for the business
   - [ ] Tests pass for real (not adjusted to pass)
   - [ ] If it breaks in production, you know how to fix or roll it back
   ```

   End the body with the attribution line the session asks for.

3. **Watch CI.** Follow the PR's checks until each finishes. A failure is investigated and fixed on the branch (back to implement-task), then pushed. Done when every check is green, or a failure you can't fix is reported to the user with its log.

4. **Hand over.** Give the user the PR link, the risk rating and anything they should look at first. Merging is theirs.
