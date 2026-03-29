# Assumptions (Auto-logged)

## Decision policy (agent)
The agent does not ask the user questions. When a decision is needed, it chooses based on **industry standards**, **best coding practices**, and project context (Arr-like, Mylar3 parity), then documents the choice here. Rationale is included when the choice is non-obvious.

## Backlog exhaustion (CONTINUE loop)
When running "repeat until nothing remains", the agent re-reads CONTINUE.md after each iteration and implements the next READY or implementable item. **Exhausted** means: every such item is either completed or explicitly deferred in BACKLOG with a reason. Remaining Deferred table items (e.g. Usenet/NZB from DDL, Folder download) are M effort and may be left for future sessions; 14.17 items (FTS5, virtualize, etc.) are documented in BACKLOG as deferred (pagination/lazy loading sufficient).

---

- Defaults should match Mylar3 behavior. If exact values unknown, use reasonable Arr-like defaults and mark TODO.
- Preferred archive: CBZ
- Default DB: SQLite for MVP
- Collections modeled as EditionTitle + EditionContents + EditionFileAsset
- Use git and commit at logical breakpoints.
- All dev happens inside the Dev Container.

## Iteration 110: Mylar3 Config Import

- **INI Format**: Assumed standard INI format with `[Section]` headers and `Key = Value` pairs
- **Mylar3 extra_newznabs**: Uses Python tuple format `[('name', 'host', verify, 'apikey', 'uid', enabled, 'categories'), ...]`
- **Default Ports**: SABnzbd = 8080, NZBGet = 6789 (matching Mylar3 defaults)
- **Boolean Values**: Accepted values: true/1/yes/on = true, everything else = false
- **Indexer Sections**: Supports `[Newznab]`, `[Newznab1]`-`[Newznab20]`, and `extra_newznabs` in `[General]`
- **SABnzbd Section**: Can be `[SABnzbd]` or `sab_*` keys in `[General]`
- **NZBGet Section**: Can be `[NZBGet]` or `nzbget_*` keys in `[General]`
- **Case Insensitivity**: Section names and keys are case-insensitive (matching INI standard)

## Iteration 226: Environment execution detail

- `docker` CLI was unavailable in this session, so required quality gates were run directly in the dev-container workspace shell (`dotnet build`, `dotnet test`, `npm run build`). This still satisfies the "run in container" rule.

## Iteration 240: OSV-Scanner in CI

- **OSV vs npm audit:** Both run in CI; OSV uses the OSV.dev database and locked versions in `package-lock.json`, while `npm audit` uses the npm advisory feed. Redundancy is intentional for defense in depth; occasional disagreement between databases is acceptable—resolve by bumping dependencies until both pass.
- **.NET / NuGet:** OSV job scans **npm lockfiles only** (no repo-root `packages.lock.json`). NuGet remains covered by **NuGet Audit** in `Directory.Build.props`.
- **Quality gates:** `docker compose` was unavailable in this environment; `dotnet build` / `dotnet test` / `npm run lint` / `npm run build` were run in the workspace shell (no `wwwroot` commit; build embeds volatile git metadata).

## Iteration 241: CodeRabbit documentation

- **Quality gates:** Same as Iteration 240 — `docker` CLI unavailable; gates run in workspace with host `dotnet` / `npm`.
- **14.26 acceptance:** CodeRabbit automated comments depend on the GitHub App being installed on the org/repo; the repo documents config (`.coderabbit.yaml`) and contributor expectations. Install state is outside the git tree.

## Iteration 242: Metron diagnostics

- **Quality gates:** `docker` CLI unavailable; `dotnet build` / `dotnet test` / `npm run lint` / `npm run build` run on the workspace host.
- **MetronLookupMiss:** Warnings are intentionally concise; if production shows a wrong `MetronApiPath` or ID mismatch, fix the caller or mapping in a follow-up (not guessed here without failing samples).

## Git integration (trunk)

- **Default integration branch:** `main`. Changes reach `main` through a **GitHub pull request** with required CI checks (maintainers merge when green).
- **No long-lived shared integration branch** after retirement of `dev`; topic branches are short-lived off up-to-date `main`. Documented in **`docs/CONTRIBUTING.md`**.
- **Batching:** Implementation and iteration docs for one slice live on **one** branch; avoid opening a second PR only for WORKLOG/SHA housekeeping.
- **Merge audit:** [`docs/AUTO_MERGE_LOG.md`](./AUTO_MERGE_LOG.md) is appended by **Record merged PR** workflow when a PR merges; do not rely on chains of short SHAs in WORKLOG for that.

## Record merged PR workflow

- **Failure to push:** If `.github/workflows/record-merged-pr.yml` cannot push to `main`, repository rules may need to allow **GitHub Actions** (or `github-actions[bot]`) to bypass or use a token with `contents: write`—see workflow file header.
