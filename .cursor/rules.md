# Cursor Rules for Claude (Shortboxerr)

## Mode
You are the autonomous implementation agent for this repo.

## Prime directive
Proceed without asking the user questions unless you are truly blocked from compiling or running tests.

If assumptions are needed:
1) pick the most reasonable Arr-like/Mylar3-like default
2) write it into docs/ASSUMPTIONS.md
3) continue

## Git workflow (MANDATORY)
- Use git for all changes.
- Commit after every logical breakpoint (small, reviewable commits).
- A “logical breakpoint” is: repo scaffolding, a new endpoint, a new migration, a new service module, a test suite addition, a UI page slice, or a bug fix.
- Each commit message MUST follow:
  - `feat: ...` for new functionality
  - `fix: ...` for bug fixes
  - `chore: ...` for tooling/docs/refactors
  - `test: ...` for test-only changes
- Each commit MUST include:
  - what changed
  - why
  - how to test (in commit body if non-trivial)
- Never squash. Keep history granular.

## Output requirements (every iteration)
- Update docs/WORKLOG.md (append)
- Update docs/BACKLOG.md (check off done, add next)
- Update docs/SELF_CHECK.md (overwrite with current status)
- Implement a vertical slice: code + tests + docs
- Create and commit changes at logical breakpoints.

## Containerized development (MANDATORY)
- All development must happen inside the Dev Container defined in .devcontainer/.
- Do NOT rely on host-installed SDKs (dotnet, node, etc. may not be on host PATH).
- **Run build, test, and npm commands inside the container:**
  ```bash
  docker compose -f docker-compose.dev.yml run --rm dev <command>
  ```
  Example: `docker compose -f docker-compose.dev.yml run --rm dev dotnet build --verbosity quiet`
  Example: `docker compose -f docker-compose.dev.yml run --rm dev dotnet test --no-build --verbosity quiet`
  Example: `docker compose -f docker-compose.dev.yml run --rm dev sh -c "cd ui && npm run build"`
- Ensure commands in docs and scripts are written to run inside the container.

## Boundaries
- Do not add license/rights gating sections.
- Do not integrate vendor-specific downloader/indexer details beyond generic adapters.
- Focus on Mylar3 behavioral parity for decisioning and media management.
- Variants are de-emphasized; collections (TPB/omnibus) are first-class.

## Style
- Prefer simple, boring, maintainable implementations.
- Favor deterministic logic and explainability.
- Write small commits/patches.

## Definition of done (per story)
A backlog item is done only when:
- code exists
- tests exist (or harness exists)
- docs updated
- project builds successfully
- changes are committed
