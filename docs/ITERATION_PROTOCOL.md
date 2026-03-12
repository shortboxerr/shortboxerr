# Iteration Protocol (Autonomous)

## Objective
Implement Shortboxerr as an Arr-like application with Mylar3 behavioral parity and first-class collected editions.

## No-questions policy
Do not ask the user for clarification. If needed, assume defaults and document them in docs/ASSUMPTIONS.md.

## Container-only policy (MANDATORY)
All build/test/run steps must be designed to run inside the Dev Container.
If a tool is needed (dotnet, node, sqlite tooling), add it to the devcontainer image.

## Git workflow (MANDATORY)
- Use git for all changes.
- Commit after every logical breakpoint (small, reviewable commits).
- **Do not leave uncommitted files.** Before finishing an iteration, run `git status`. Commit any staged or untracked changes that belong in the repo. If you unstage some files to keep a commit focused, commit the remainder in the next commit in the same session.
- Breakpoints examples:
  - initial repo scaffold
  - add/extend DB migration + entity mapping
  - add endpoint + tests
  - add background job + tests
  - add UI page slice
  - fix a failing test/bug
- Commit messages:
  - `feat: ...`, `fix: ...`, `chore: ...`, `test: ...`
- Each iteration should typically produce multiple commits.
- Never rewrite history during iteration (no squashing).

## Iteration deliverables (MANDATORY)
Each iteration MUST:
1) implement a vertical slice (end-to-end) including tests
2) update docs/WORKLOG.md (append)
3) update docs/BACKLOG.md (mark done/next)
4) update docs/SELF_CHECK.md (overwrite)
5) leave the repo in a buildable state, or explicitly document failures and next steps
6) commit changes at logical breakpoints

## Vertical slice definition
A vertical slice includes:
- at least one API endpoint
- associated domain/service layer logic
- persistence change (if needed)
- a unit/integration test
- docs update (docs/API.md + relevant doc)

## Workflow
1) Read docs/PLAN.md (source of truth)
2) Read docs/BACKLOG.md and pick top READY items
3) Implement smallest useful slice
4) Add tests (golden tests for decisioning where applicable)
5) Update docs
6) Run SELF_CHECK rubric and record results
7) Commit at breakpoints throughout

## Default priorities
1) Repo skeleton + health + migrations
2) Domain + CRUD for Series/Issues/Collections
3) Import pipeline (staging/manual import)
4) DecisionEngine parity scaffolding
5) Indexer manager + first-party DDL stubs
6) UI shell + pages
7) Mylar3 migration tool

## Stop Criteria (to prevent infinite wandering)
Stop the current iteration and write a clear next-steps plan if any of the following occur:
1) **Build not green after 2 consecutive fix attempts** in the same iteration.
2) **More than 90 minutes of work** (estimate) has been spent in the same iteration without landing a meaningful vertical slice.
3) **Scope creep detected**: a task expands beyond the epic/story acceptance criteria—defer extras to backlog.
4) **Refactor temptation**: do not refactor unless required to ship the slice or fix a failing test.
5) **Repeated flaky failure**: if the same test fails intermittently twice, quarantine it (mark as flaky) and add a backlog item to stabilize.

When stopping:
- Update docs/SELF_CHECK.md with the exact failure
- Append to docs/WORKLOG.md what was attempted
- Update docs/BACKLOG.md with newly discovered tasks
- Commit any safe partial work (WIP commits are allowed but must be labeled `chore(wip): ...`)

## Quality bars
- deterministic behavior
- explainable decisions
- atomic file operations
- no breaking API changes without version bump
