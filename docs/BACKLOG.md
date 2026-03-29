# Backlog

## Quick Navigation

| Status | EPIC | Description | Details |
|:------:|------|-------------|---------|
| ✅ | EPIC 0 | Repo Skeleton (Foundation) | [Archive](./COMPLETED.md#epic-0-repo-skeleton-foundation--completed) |
| ✅ | EPIC 1 | Domain + Persistence | [Archive](./COMPLETED.md#epic-1-domain--persistence-minimum-data-model--completed) |
| ✅ | EPIC 2 | Import Pipeline | [Archive](./COMPLETED.md#epic-2-import-pipeline-mylar3-like--completed) |
| ✅ | EPIC 3 | DecisionEngine | [Archive](./COMPLETED.md#epic-3-decisionengine-mylar3-like-selection--completed) |
| ✅ | EPIC 4 | Indexers + Download Clients | [Archive](./COMPLETED.md#epic-4-indexers--download-clients-arr-like-shape--completed) |
| ✅ | EPIC 5 | UI (Arr-like) | [Archive](./COMPLETED.md#epic-5-ui-arr-like-ui--completed) |
| ✅ | EPIC 6 | Settings & UI Enhancements | [Archive](./COMPLETED.md#epic-6-settings-persistence--ui-enhancements--completed) |
| ✅ | EPIC 7 | Mylar3 Migration | [Archive](./COMPLETED.md#epic-7-mylar3-migration-behavioral-parity-setup--completed) |
| ✅ | EPIC 8 | DDL Site Adapters | [Archive](./COMPLETED.md#epic-8-ddl-site-adapters--download-hosts-mylar3-parity--completed) |
| 🔄 | [EPIC 9](#epic-9-comicvine-integration-mylar3-parity--in-progress) | ComicVine Integration | In Progress |
| ✅ | EPIC 10 | NZB/Usenet Support | [Archive](./COMPLETED.md#epic-10-nzbusenet-support-mylar3sonarradarr-parity--completed) |
| ✅ | EPIC 11 | Weekly Pull List | [Full Details Below](#epic-11-weekly-pull-list-mylar3-parity--completed) |
| 🔄 | [EPIC 12](#epic-12-performance--caching-strategy--in-progress) | Performance & Caching | In Progress |
| ✅ | EPIC 13 | Logging & Diagnostics | [Full Details Below](#epic-13-logging--diagnostics-mylar3sonarradarr-parity--completed) |
| 📋 | [EPIC 14](#epic-14-future-enhancements--planned) | Future Enhancements | Planned |
| ✅ | EPIC 15 | UI Bug Fixes | [Archive](./COMPLETED.md#epic-15-ui-bug-fixes--improvements--completed) |
| ✅ | EPIC 16 | E2E Testing Infrastructure | [Archive](./COMPLETED.md#epic-16-end-to-end-testing-infrastructure--completed) |
| ✅ | EPIC 17 | DDL Download Robustness | [Archive](./COMPLETED.md#epic-17-ddl-download-link-robustness--completed) |
| 🔄 | [EPIC 18](#epic-18-library-organization--rename-sonarradarr-parity--in-progress) | Library Organization | In Progress |
| ✅ | EPIC 19 | Auto-Matching Robustness | [Archive](./COMPLETED.md#epic-19-auto-matching-robustness) |
| ✅ | [EPIC 20](#epic-20-performance-optimization--completed) | Performance Optimization | Completed |
| ✅ | [EPIC 21](#epic-21-test-stabilization--quality-gates--high-priority) | Test Stabilization | Completed |
| 🔄 | [EPIC 22](#epic-22-git-flow-pr-governance--semver-releases--in-progress) | PR Governance & Releases | In Progress |

**Legend:** ✅ Completed | 🔄 In Progress | 📋 Planned | 🔴 High Priority

> **Note:** Completed EPICs are archived in [COMPLETED.md](./COMPLETED.md) to keep this document focused on active work.

---

## EPIC 9: ComicVine Integration (Mylar3 Parity) 🔄 IN PROGRESS

ComicVine is the primary metadata source for comic series, issues, and collections.

### Completed Sections
- ✅ 9.1 ComicVine API Client (12 unit tests)
- ✅ 9.2 Series Metadata (14 unit tests)
- ✅ 9.3 Issue Metadata (16 unit tests)
- ✅ 9.4 Cover Art (17 unit tests)
- ✅ 9.5 Collection/TPB Metadata
- ✅ 9.6 Auto-Matching & Import Integration
- ✅ 9.7 Metadata Refresh
- ✅ 9.8 Mylar3 ComicVine Settings Import
- ✅ 9.9 ComicVine UI
- ✅ 9.10 ComicVine Conformance Tests
- ✅ 9.11 Series Detail Page - Issues Display
- ✅ 9.12 Series Status Accuracy
- ✅ 9.13 Cover Cache Size Limits & Eviction

### Remaining Work
All major items complete. Minor enhancements may be added as discovered.

#### 9.14 ComicVine Enablement Gating by Valid API Key ✅ COMPLETED (Iteration 226)
Only enable ComicVine-dependent features after a user provides an API key in Settings and the key is verified with a successful test call.

**Goals:**
- [x] Require ComicVine API key entry in Settings before enablement can be toggled on.
- [x] Add/confirm a "Test API Key" validation path that performs a real ComicVine connectivity/auth check.
- [x] Persist enabled state only when the test succeeds; if test fails, keep ComicVine disabled and show actionable error feedback.
- [x] Ensure backend enforces the same rule (no enablement without valid configured key), not just frontend UI gating.
- [x] Add/update unit/integration tests for valid/invalid key flows and enablement behavior.

**Acceptance:** ComicVine remains disabled until a key is configured and successfully tested; failed tests never leave ComicVine enabled.

**Effort:** S | **Priority:** P2

---

## EPIC 11: Weekly Pull List (Mylar3 Parity) ✅ COMPLETED

Track upcoming comic releases and automate wanted list management.

### Completed Sections
- ✅ 11.1 Release Date Tracking
- ✅ 11.2 Weekly Pull List Generation
- ✅ 11.3 Wanted List Automation
- ✅ 11.4 Pull List Notifications (Webhook, Email, Pushover, Pushbullet, Telegram)
- ✅ 11.5 Pull List UI
- ✅ 11.6 Pull List Configuration
- ✅ 11.7 Pull List Conformance Tests
- ✅ 11.8 This Week Discovery (Mylar3 Parity)
- ✅ 11.9 Pull List UX Improvements
- ✅ 11.10 Weekly Pull List Export
- ✅ 11.11 ComicVine Sync Parity
- ✅ 11.12 Show Upcoming Releases on Series View
- ✅ 11.13 Cover Image Fallback Implementation
- ✅ 11.14 Metron Integration for Backup Covers
- ✅ 11.15 Hide Internal Data Source Names from UI
- ✅ 11.16 WalkSoftly Pull List Integration
- ✅ 11.17 Discovery Cover Image Enrichment (Research)
- ✅ 11.18 Metron Settings UI Refinements
- ✅ 11.19 Security Audit: Credential Storage & Protection
- ✅ 11.20 Metron Enable Validation
- ✅ 11.21 Upcoming Issues: Display Parity
- ✅ 11.22 Upcoming Issues: Metron Cover Enrichment Service
- ✅ 11.23 Metron Cover Caching Parity
- ✅ 11.24 Enrichment Tracking for Cover Sources
- ✅ 11.25 ID-Less Upcoming Issue Matching for Metron Covers

### Remaining Work

#### 11.26 Pull List: Local Caching of Metron Cover Images ✅ COMPLETED (Iteration 178)
Integrated with 11.27 - Discovery covers are now cached locally.

#### 11.27 Pull List Data Flow Refactoring: Unified Enrichment Strategy ✅ COMPLETED (Iteration 178)
Refactor Pull List data retrieval and enrichment to establish a clear hierarchy of data sources with well-defined finalization states.

**Data Source Hierarchy:**
1. **ComicVine** - Authoritative source (finalizes data)
2. **Metron** - Interim fallback (when CV ID not available)
3. **WalkSoftly** - Release schedule source (initial data)

**Completed Items:**
- [x] Define enrichment state tracking (EnrichmentStatus enum)
- [x] Refactor GetDiscoveryReleasesAsync with branching logic
- [x] Implement ComicVine direct enrichment path
- [x] Refine Metron fallback path
- [x] Implement background upgrade service (Iteration 158)
- [x] Tests for enrichment state transitions
- [x] Update local cover caching (integrates 11.26) - Iteration 178
- [x] Fix `/api/v1/covers/discovery/{id}` endpoint naming (Iteration 167)

**Local Cover Caching Architecture (Iteration 178):**
- `DiscoveryCoverEnrichmentService` downloads Metron covers locally
- `DiscoveryUpgradeBackgroundService` downloads ComicVine covers locally during upgrade
- `CoverService.GetDiscoveryCoverAsync` serves cached covers from disk
- Covers stored at `covers/discovery/{CvIssueId}/{size}.jpg`

---

## EPIC 12: Performance & Caching Strategy 🔄 IN PROGRESS

### Completed Sections
- ✅ 12.1 Data Caching Strategy
- ✅ 12.2 Cache Implementation Patterns (ICacheService)
- ✅ 12.3 HTTP Response Caching
- ✅ 12.4 ComicVine API Optimization (request batching)
- ✅ 12.5 Intelligent Pull List Cache Lifecycle
- ✅ 12.6 Monitoring & Diagnostics

### Cache TTL Reference Table
| Data Type | Layer | TTL | Invalidation Trigger |
|-----------|-------|-----|---------------------|
| Pull list (active week) | Frontend | 30 min | Status change, manual refresh |
| Pull list (historical) | Frontend | 7 days | Manual refresh only |
| Series list | Backend | 2 min | Series CRUD |
| Series detail | Backend | 5 min | Series/Issue CRUD |
| Dashboard stats | Backend | 1 min | Any status change |
| ComicVine volume | Backend | 24 hours | Manual refresh |
| Cover images | Disk | Permanent | Manual clear |

---

## EPIC 13: Logging & Diagnostics (Mylar3/Sonarr/Radarr Parity) ✅ COMPLETED

### All Sections Complete
- ✅ 13.1 File-Based Logging (Serilog, sensitive data protection, rotation, compression)
- ✅ 13.2 Log Categories & Content (lifecycle, API, ComicVine, download, import, background)
- ✅ 13.3 Log Viewer UI (System > Logs, filtering, search, download)
- ✅ 13.4 Diagnostic Tools (system info, health checks, debug mode)
- ✅ 13.5 Log Settings UI (level, path, rotation, retention)

**Security:** SensitiveDataDestructuringPolicy auto-masks API keys, passwords, tokens in all logs.

---

## EPIC 14: Future Enhancements 📋 PLANNED

### Completed Enhancements
- ✅ 14.1 Deferred Items Audit (28 items tracked)
- ✅ 14.2 NZBGet Integration (75 unit tests)
- ✅ 14.3 Torrent Download Client Integration (qBittorrent, Transmission, Deluge)
- ✅ 14.4 Theme Accessibility & Color Scheme Audit
- ✅ 14.5 ReadComicOnline Parity with GetComics
- ✅ 14.6 Mylar3 Search Settings Parity
- ✅ 14.9 Workflow Connectivity Audit
- ✅ 14.10 DDL Auto-Import Background Service

### Planned Enhancements

#### 14.7 Issue Data & Cover Acquisition Refactoring 📋 PLANNED
Comprehensive examination of the issue data and cover acquisition pipeline.

**Sections:**
- 14.7.1 Code Architecture Review ✅ COMPLETED (Iteration 208)
- 14.7.2 Cover Source Integration Testing ✅ COMPLETED (Iteration 209)
- 14.7.3 Unit Test Coverage Expansion ✅ COMPLETED (Iteration 210)
- 14.7.4 Refactoring Candidates ✅ COMPLETED (Iteration 211)
- 14.7.5 Edge Case Handling ✅ COMPLETED (Iteration 212)

**Refactoring candidates (14.7.4, from docs/research/ISSUE_COVER_ARCHITECTURE.md):**
- Cover source integration testing: explicit tests that each source (ComicVine, Metron, volume fallback) is invoked in correct order; discovery cache keys align with API.
- Unit test coverage: CoverService path logic, revalidation, discovery key mapping; CoverFallbackService lookup order and cache key format.
- Edge cases: missing CV ID, rate limiting (Metron 429), behavior when both CV and Metron fail for an issue.

#### 14.12 Future Week Cover Enrichment Improvements ✅ COMPLETED (Iteration 188)
Fix issue where future weeks show volume/series images instead of actual issue covers from Metron.

**Implemented:**
- [x] **UI indicator for cover source**
  - Added `isVolumeFallbackCover` field to DiscoverableIssue model
  - Small warning-colored icon appears on cards with volume fallback covers
  - Tooltip explains "Series cover (issue cover unavailable)"
- [x] **Manual re-enrich action**
  - Added "Refresh Covers" button to Pull List toolbar
  - Forces cover enrichment with `force=true` (bypasses cooldown)
  - Shows loading spinner during operation
- [x] **Frontend type updates**
  - Added `coverSource`, `enrichmentStatus`, `isVolumeFallbackCover` to TS interface

**Optional / Ready when needed:**
- [x] **Debug Metron lookup failures** ✅ COMPLETED (Iteration 242) – `MetronClient` logs structured **Warning** lines prefixed with `MetronLookupMiss` (Reason, Operation, MetronApiPath, relevant IDs). No credentials or response bodies. Use logs + IDs to open follow-up issues if the API path or mapping is wrong.
- [x] **Lower confidence threshold for future issues** ✅ CLOSED (Iteration 241) – Already shipped as `AutoMatchSettings.LowConfidenceThreshold` (default 70), API `PUT /api/settings/automatch`, and Settings UI (*Match Verification*); see Iteration 176 / EPIC 19.4. This backlog line was redundant.
- [x] Auto re-enrich on week transition (background service enhancement) ✅ Iteration 202

**Effort:** M | **Priority:** P2

#### 14.13 Add Series Flow Improvements ✅ COMPLETED (Iteration 187)
Improve the "Add Series" experience to handle large result sets better and allow batch adding.

**Implemented:**
- [x] **Switch to compact list view**
  - Columns: Title, Year, Publisher, Issue Count
  - Toggle between list and grid views
- [x] **Sort results by year (newest first)**
  - Default changed from "Most Issues" to "Newest First"
- [x] **Multi-select for batch adding**
  - Checkboxes on each result row
  - "Add X Series" button with count
  - Progress indicator (Adding 1 of N...)
  - Select All / Deselect All button

**Optional enhancements (Ready to pick):**
- [x] **Replace Add Series modal with dedicated page** ✅ COMPLETED (Iteration 225) – Dedicated route `/series/add` with full-page Add Series flow; Series page "Add Series" navigates there. Shared `AddSeriesContent` component.
- [x] **Quick filters by publisher/year** ✅ COMPLETED (Iteration 220) – Add Series modal: optional Publisher text filter and Year range (From/To); passed to ComicVine search API.

**Effort:** M | **Priority:** P2

#### 14.8 Series Deletion UX Improvements ✅ COMPLETED (Iteration 168)
- [x] Confirmation modal for series deletion
- [x] Deletion progress indicator
- [x] List refresh after deletion (via navigate to /series)
- [x] Backend: Cascade delete linked annual series

#### 14.11 ComicVine ID Search Support ✅ COMPLETED (Iteration 184)
Accept ComicVine IDs as search parameters when adding comics, auto-detecting IDs vs search terms.

**ComicVine ID Formats:**
| Type | Format | Regex | Example |
|------|--------|-------|---------|
| Volume/Series | `4050-XXXXX` | `^4050-\d+$` | `4050-12345` |
| Issue | `4000-XXXXXX` | `^4000-\d+$` | `4000-123456` |
| Story Arc | `4045-XXXXX` | `^4045-\d+$` | `4045-98765` |

**Items:**
- [x] **Add ComicVine ID detection utility**
  - Created `ComicVineIdParser` with regex patterns for all types
  - Returns parsed type (Volume, Issue, StoryArc) and numeric ID
  - Handles full format (`4050-12345`), plain numeric (`12345`), and URLs
- [x] **Update Series Search endpoint**
  - Detects if search term matches ComicVine ID pattern
  - If volume ID detected, fetches directly via `GetSeriesByComicVineIdAsync`
  - Returns as single match with `IsDirectLookup` flag
- [x] **Update Issue Search/Lookup** ✅ COMPLETED (Iteration 215)
- [x] **Update Edition/Collection Search** ✅ COMPLETED (Iteration 219) – Edition/collection search accepts ComicVine volume ID (4050-xxxxx); direct lookup via GetEditionByComicVineIdAsync.
- [x] **UI hint for ID input** ✅ COMPLETED (Iteration 214)

**Effort:** S | **Priority:** P2

#### 14.14 Frontend Build-Time Version Embedding ✅ COMPLETED (Iteration 198)
Embed version/build info into the frontend at build time, matching how Sonarr/Radarr display version information.

**Implemented:**
- [x] Add build-time version injection via Vite `define`
  - `__APP_VERSION__`, `__COMMIT_HASH__`, `__COMMIT_DATE__`, `__BUILD_TIME__`, `__BRANCH__`
- [x] Display version in UI sidebar footer
  - Shows version number with tooltip showing commit hash and branch
- [x] TypeScript declarations in `vite-env.d.ts`

**Effort:** S | **Priority:** P3

#### 14.15 System Status Endpoint (*arr Parity) ✅ COMPLETED (Iteration 198)
Add `/api/v1/system/status` endpoint matching the pattern used by Sonarr/Radarr for frontend initialization.

**Implemented:**
- [x] `/api/v1/system/status` endpoint (pre-existing, enhanced)
  - Version, commit hash, branch
  - Start time, uptime
  - Memory usage (working set MB)
  - Database statistics (series, issues, collections, files count)
  - Indexer status (NZB + DDL sites)
- [x] `/api/v1/system/info` endpoint (pre-existing, enhanced)
  - Full OS info, runtime info
  - App data/config/log paths
  - Disk space info
  - Now includes commit hash and branch

**Optional / Blocked:**
- [ ] Frontend fetches on app load – **Won't do** for now (version already in UI).
- [ ] `/initialize.json` alias – **Won't do** (current endpoint sufficient).
- [ ] Authentication status in status endpoint – **📋 Ready when auth exists** (blocked on auth implementation).

**Effort:** S | **Priority:** P3

#### 14.18 Series List Text Search ✅ COMPLETED (Iteration 203)
Add text search filtering to the series list API endpoint.

**Implemented:**
- [x] Add `search` query parameter to `GET /api/v1/series`
- [x] Case-insensitive search on Title and SortTitle fields
- [x] Combines with existing filters (status, publisher, monitored)
- [x] Proper cache key versioning (v3)
- [x] 6 unit tests for search functionality

**Effort:** S | **Priority:** P2

#### 14.19 Edition List Text Search ✅ COMPLETED (Iteration 204)
Add text search filtering to the edition list API endpoint.

**Implemented:**
- [x] Add `search` query parameter to `GET /api/v1/editions`
- [x] Case-insensitive search on Title, SortTitle, and Series.Title fields
- [x] Combines with existing series filter
- [x] Added Swagger documentation
- [x] 11 unit tests for filter/search/sort functionality

**Effort:** S | **Priority:** P2

#### 14.20 Enhanced Wanted Endpoint Filters ✅ COMPLETED (Iteration 205)
Add advanced filtering to wanted issues and collections endpoints.

**Implemented:**
- [x] Publisher filter for wanted issues
- [x] Release date range filter (releasedAfter/releasedBefore) for wanted issues
- [x] Publisher filter for wanted collections
- [x] Release date range filter for wanted collections
- [x] Edition type filter for wanted collections
- [x] 9 unit tests for new filters

**Effort:** S | **Priority:** P2

#### 14.21 Series Release Date Sorting ✅ COMPLETED (Iteration 206)
Add release date sorting options to the series list endpoint.

**Implemented:**
- [x] `latestrelease` sort (most recent issue release date)
- [x] `nextrelease` sort (soonest upcoming issue)
- [x] StoreDate with fallback to ReleaseDate
- [x] 4 unit tests for sorting functionality

**Effort:** S | **Priority:** P2

#### 14.22 Enhanced Edition List Filters ✅ COMPLETED (Iteration 207)
Add monitoring and status filters to the edition list endpoint.

**Implemented:**
- [x] `monitored` filter (true/false)
- [x] `hasFile` filter (true/false)
- [x] `editionType` filter (TradesPaperback, Hardcover, Omnibus, etc.)
- [x] 9 unit tests for filter functionality

**Effort:** S | **Priority:** P2

#### 14.16 SignalR Real-Time Updates (*arr Parity) 🔄 IN PROGRESS (Iteration 200-201)
Add SignalR hub for push notifications, matching the real-time update pattern in Sonarr/Radarr/Lidarr.

**Implemented:**
- [x] Add SignalR hub at `/signalr/messages`
- [x] Create `IMessageBroadcaster` interface in Core layer
- [x] Implement `SignalRMessageBroadcaster` with typed messages:
  - `DownloadStartedMessage`, `DownloadCompletedMessage`
  - `ImportCompletedMessage`, `SearchResultsMessage`
  - `QueueUpdateMessage`, `SystemStatusMessage`
- [x] Configure CORS for SignalR (AllowCredentials)
- [x] Wire up DdlImportBackgroundService to broadcast ImportCompleted
- [x] Wire up AutoSearchBackgroundService to broadcast SearchResults
- [x] Add SignalR message unit tests (8 tests)

**Remaining (Ready to work):**
- [ ] **Frontend SignalR client** 📋 Ready (L) – Subscribe to hub from UI; replace polling on Activity/Queue where applicable. (Previously blocked by npm/network; re-try when environment allows.)
- [x] **Graceful fallback to polling** ✅ COMPLETED (Iteration 222) – Documented in docs/ARCHITECTURE.md and ActivityPage.tsx: when SignalR client is added, use existing polling as fallback when connection fails.

**Effort:** L | **Priority:** P2

#### 14.17 Performance Enhancements (from EPIC 20) 📋 PLANNED
Additional performance optimizations deferred from EPIC 20. All sub-items are **ready to pick** when prioritizing performance (low priority).

**Items:**
- [x] **Full-text search indexes (Series)** ✅ COMPLETED (Iteration 223) – SQLite FTS5 for series list search: migration AddSeriesFts5, SeriesFtsHelper, series endpoint uses FTS when SQLite and falls back to LIKE when FTS returns no IDs. Editions FTS can be added later.
- [ ] **Virtualize Series issue grid** 📋 Ready (L) – 2D grid virtualization; pagination is sufficient for now.
- [ ] **Virtualize Series table** 📋 Ready (M) – Lower priority; pagination sufficient.
- [ ] **Virtualize Pull List discovery** 📋 Ready (M) – Grouped by week; pagination sufficient.
- [x] **Intersection observer for images** ✅ COMPLETED (Iteration 224) – Documented in docs/DECISIONS.md: we rely on native `loading="lazy"`; custom observer deferred.
- [ ] **Server-side pagination for SeriesDetailPage** 📋 Ready (M) – Requires API contract changes.
- [x] **Batched API endpoints** (multi-week data in one call) ✅ COMPLETED (Iteration 213)

**Note:** These are nice-to-have optimizations. Current implementations with pagination and native browser features are working well.

**Effort:** L | **Priority:** P3

#### 14.23 ESLint: Address UI Lint Warnings ✅ COMPLETED (Iteration 237)
Resolve or formally accept ESLint warnings so the UI lint run is clean (zero warnings) or exceptions are documented.

- [x] **Document accepted warnings** (Iteration 217): `ui/eslint.config.js` describes rule levels and policy.
- [x] **Zero warnings:** `npm run lint` uses `eslint . --max-warnings 0` (Iteration 237). CI runs ESLint on the UI matrix job.

**Iteration 237 changes:** `useTheme` → `theme/ThemeContext.tsx` + `theme/ThemeProvider.tsx`; toast hook → `components/toast/useToast.ts` + `ToastProvider.tsx` (fast-refresh clean). **Layout:** `queueMicrotask` + NavLink `onClick` for sidebar close. **PullListPage / SeriesDetailPage:** display/view mode derived from settings + mutation `variables` (no sync effects). **WantedPage:** tab derived from URL. **client.ts:** typed API stubs replace `any` on flagged lines. **SeriesDetailPage:** pagination reset via `queueMicrotask`. **LogsPage:** one line-level disable for TanStack `useVirtualizer` (`incompatible-library`).

**Acceptance:** Either zero warnings, or an eslint.config.js / docs note that lists accepted warnings and rationale.

**Effort:** M | **Priority:** P2

#### 14.24 npm audit: Resolve UI Dependency Vulnerabilities ✅ COMPLETED (Iteration 216)
Address npm audit findings in `ui/` (1 moderate, 2 high vulnerabilities).

**Tasks:**
- [x] Run `npm audit` in `ui/` and capture current report (ajv, minimatch, rollup)
- [x] Apply `npm audit fix` – resolved all 3 (0 vulnerabilities remaining)
- [x] Re-run audit after changes – clean
- [x] UI build verified after fix

**Effort:** S | **Priority:** P2

#### 14.25 ESLint Accepted Warnings: Security & Safety Validation ✅ COMPLETED (Iteration 218)
Validate `ui/eslint.config.js` and the documented accepted warnings to ensure they do not introduce app or security risk.

**Goals:**
- [x] Review each downgraded rule for security implications (set-state-in-effect, only-export-components, no-explicit-any, static-components).
- [x] Confirm accepted patterns align with project security stance (docs/SECURITY.md).
- [x] Document outcome: added “ESLint Accepted Warnings (UI)” subsection to docs/SECURITY.md with per-rule assessment and reminder to re-check when changing accepted warnings.

**Effort:** S | **Priority:** P2

#### 14.26 AI-Powered PR Review (Free on GitHub) ✅ COMPLETED (Iteration 241)
Enable automated AI code review for pull requests on the shortboxerr repo using a **free** GitHub-integrated option.

**Chosen solution:** [CodeRabbit](https://coderabbit.ai/) GitHub App (free for public repos). Repository config: [`.coderabbit.yaml`](../.coderabbit.yaml) (profile, path filters, auto-review).

**Goals:**
- [x] Choose a free solution — CodeRabbit (no repo API keys required; app install on GitHub).
- [x] Configure so PRs get automated review comments — via CodeRabbit app + `.coderabbit.yaml` (summary/review behavior).
- [x] Document setup — `docs/CONTRIBUTING.md` *Automated PR review (CodeRabbit)*.

**Acceptance:** New PRs receive at least one automated AI review comment; solution is free for public repos and documented.

**Effort:** S | **Priority:** P3

#### 14.27 Prevent Committing AI-Related / Sensitive Dev Files ✅ COMPLETED (Iteration 234)
Ensure the repo does not commit files that should stay local (AI tooling state, secrets, dev-only config with credentials). If such files are already in history, remove them and fix history.

**Goals:**
- [x] **Define blocklist:** `docs/SECURITY.md` *AI and Dev Tooling* table (authoritative); aligned with `.gitignore`.
- [x] **Verify MCP / Cursor config in repo:** `.cursor/mcp.json` uses token-free `bash` + `.cursor/github-mcp.sh` only; documented under SECURITY *Committed MCP config*.
- [x] **Update .gitignore:** Added `.devcontainer/local-secrets/` (PAT file path used by `github-mcp.sh`); existing `.cursor/` and env patterns retained.
- [x] **Audit git history:** Spot-check `git log` on `.cursor/agent-transcripts/`, `.env*`, `*.secrets.json` — no hits (documented in SECURITY).
- [x] **Fix history if needed:** N/A (no blocklisted paths found in history).
- [x] **Document policy:** `docs/CONTRIBUTING.md` expanded with blocklist pointer, MCP, local-secrets, CI security checks.

**Acceptance:** (1) .gitignore and docs clearly define what must not be committed; (2) history is audited; (3) any past commits containing such files are rewritten so they no longer appear in history; (4) policy is documented.

**Effort:** M | **Priority:** P2

#### 14.28 Resolve NuGet transitive vulnerabilities (security audit Mar 2026) ✅ COMPLETED (Iteration 233)
`dotnet list package --vulnerable --include-transitive` reported high-severity transitive packages. Clear them by bumping direct dependencies, central package management, or explicit version overrides so the resolved graph uses patched versions.

**Packages called out in audit:**
- [x] **Microsoft.Extensions.Caching.Memory** (8.0.0) — [GHSA-qj66-m88j-hmgj](https://github.com/advisories/GHSA-qj66-m88j-hmgj) — pinned via `Directory.Build.props` (8.0.1)
- [x] **System.Text.Json** (8.0.0) — [GHSA-hh2w-p6rv-4g7w](https://github.com/advisories/GHSA-hh2w-p6rv-4g7w), [GHSA-8g4q-xg66-9fp4](https://github.com/advisories/GHSA-8g4q-xg66-9fp4) — pinned 8.0.6 in `Directory.Build.props`
- [x] **System.Text.Encodings.Web** (4.5.0, critical) — resolved by pinning 8.0.0 in `Directory.Build.props` (surfaced after other bumps)
- [x] **Shortboxerr.Tests only:** **System.Net.Http** / **System.Text.RegularExpressions** — explicit 4.3.4 / 4.3.1 in test csproj

**Also:** Bumped shared `Microsoft.AspNetCore.*` / `Microsoft.EntityFrameworkCore.*` references to **8.0.11**; `Microsoft.Extensions.Options` → **8.0.2**, `Microsoft.Extensions.Hosting.Abstractions` → **8.0.1** (8.0.11 not published for those packages on NuGet).

**Acceptance:** `dotnet list package --vulnerable --include-transitive` reports no known vulnerabilities for solution projects (or documented exceptions with mitigation).

**Effort:** M | **Priority:** P2

#### 14.29 Build output: no secrets in `wwwroot` / UI bundles ✅ COMPLETED (Iteration 234)
Treat `wwwroot` and Vite `dist` as build artifacts only. Ensure CI and docs guard against injecting API keys or other credentials at build time; optional follow-up: secret scanning on produced JS (e.g. gitleaks patterns) if automation is desired.

**Goals:**
- [x] Document in `docs/SECURITY.md` or build docs that frontend bundles must never embed runtime secrets; review env-based `define` usage (Iteration 233: "Build output" under Frontend Security).
- [x] CI **Gitleaks** scans full git history (including tracked `wwwroot` assets) on push/PR — catches accidental committed secrets; complements docs/policy (Iteration 234).

**Effort:** S | **Priority:** P3

#### 14.30 E2E folder: dependency audit & release boundary ✅ COMPLETED (Iteration 233)
`tests/e2e/` has its own `node_modules` tree. Keep lockfile discipline and periodically run `npm audit` there; confirm Playwright/e2e dependencies are never packaged into release images or published artifacts.

**Goals:**
- [x] Run `npm audit` in `tests/e2e/` on a recurring basis (or wire into CI) — audit run Mar 2026: 0 vulnerabilities; policy captured in `docs/SECURITY.md`.
- [x] Confirm Docker/release pipelines exclude e2e `node_modules` from shipped bits (documented in `docs/SECURITY.md`).

**Effort:** S | **Priority:** P3

#### 14.31 Deeper security posture (tooling & threat model) ✅ COMPLETED (Iteration 235)
The Mar 2026 security audit was lightweight (grep, `docs/SECURITY.md`, package vulnerability list). Schedule deeper work when warranted.

**Goals:**
- [x] Enable or tune **Dependabot** (NuGet + npm for `ui/` and `tests/e2e`, GitHub Actions) — `.github/dependabot.yml` added Iteration 234; tune branch/labels in repo settings as needed.
- [x] Add a recurring **`dotnet list package --vulnerable`** (and/or `npm audit`) step in CI — **NuGet** Iteration 233; **npm audit** (`ui` + `e2e`, `--audit-level=high`) Iteration 234.
- [x] **Gitleaks** in CI (Iteration 234). **OSV-Scanner** on npm lockfiles in CI (Iteration 240). **Semgrep:** optional follow-up if a future iteration wants extra static analysis.
- [x] Short **threat model** / data-flow note — `docs/SECURITY.md` *Lightweight threat model* (Iteration 235): trust boundaries, optional API key, TLS/reverse proxy, CI summary.

**Effort:** L | **Priority:** P3

#### 14.32 Vulnerability disclosure & container security notes ✅ COMPLETED (Iteration 238)
Make it easy for reporters to disclose issues safely and document how the shipped container relates to secrets and supply chain.

**Delivered (Iteration 238):**
- [x] **`.github/SECURITY.md`** — supported branches, how to report (private reporting / advisories, not public issues), pointer to `docs/SECURITY.md` and CI summary.
- [x] **`docs/SECURITY.md`** — CI checks table (NuGet, npm audit, ESLint, Gitleaks, Docker gate); **Docker** subsection (multi-stage, non-root user, no baked `.env`/secrets in image layers).
- [x] **`docs/CONTRIBUTING.md`** — link to `.github/SECURITY.md` for vulnerability reports.

**Effort:** S | **Priority:** P2

### Items Available for Work (formerly Deferred)

These items are in a **workable** status: either Ready to pick when capacity allows, or Planned with a clear unblock condition.

| Item | EPIC | Effort | Status | Next step |
|------|------|--------|--------|-----------|
| **Character/team appearances** | 9 | M | 📋 Ready | Foundation complete; implement UI + API for character/team data (ComicVine or Metron). |
| **Usenet/NZB from DDL sites** | 8 | M | 📋 Ready | Implementable; extend DDL pipeline to consume NZB/Usenet from DDL sources. Pick when M capacity available. |
| **Folder download (Dropbox/Drive)** | 8 | M | 📋 Ready | Implementable; add folder-based download (Dropbox, GDrive, etc.). Pick when M capacity available. |
| ~~Distributed cache pub/sub~~ | 12 | L | ✅ COMPLETED (Iteration 180) | — |

**Status legend:** 📋 Ready = unblocked, can be scheduled; **Won't do** = out of scope for now; **Ready when X** = blocked until X is done. Treat all 📋 Ready items as normal backlog; prioritize by EPIC and effort.

---

## EPIC 18: Library Organization & Rename (Sonarr/Radarr Parity) 🔄 IN PROGRESS

Reorganize existing library files to match current naming format settings.

### 18.1 Series Folder Rename Service ✅ COMPLETED
- [x] **ILibraryOrganizationService interface** (Iteration 164)
- [x] **SeriesRenamePreview model** (Iteration 164)
- [x] **LibraryOrganizationService implementation** (Iteration 164)

### 18.2 Series Rename API Endpoints ✅ COMPLETED
- [x] **Preview endpoint**: POST /api/v1/series/organize/preview (Iteration 164)
- [x] **Execute endpoint**: POST /api/v1/series/organize/execute (Iteration 164)
- [x] **Single series endpoint**: POST /api/v1/series/{id}/organize (Iteration 164)
- [x] **Single series preview**: GET /api/v1/series/{id}/organize/preview (Iteration 164)

### 18.3 Mass Editor Integration ✅ COMPLETED
- [x] Series page bulk "Organize" action (Iteration 166)
- [x] Series Detail Page "Organize" button (Iteration 165)

### 18.4 File Rename Within Series ✅ COMPLETED
- [x] Issue file rename preview (IssueFileFormat tokens) (Iteration 164)
- [x] Edition/Collection file rename preview (CollectionFileFormat tokens) (Iteration 164)
- [x] Conflict detection (Iteration 164)
- [x] Enhanced preview UI with filtering and type grouping (Iteration 172)

### 18.5 Bulk Organization Tools ✅ COMPLETED
- [x] "Organize All" system task (Iteration 169)
- [x] Auto-organize on format change (Iteration 179)

### 18.6 Safety & Rollback (Partial)
- [x] Dry-run mode (Iteration 179)
- [x] **Atomic operations (per-series)** ✅ COMPLETED (Iteration 221) – On any file-move failure, roll back successful moves and do not update DB; single-series organize is all-or-nothing.
- [ ] **Undo support** 📋 Ready (L, stretch) – Restore previous paths after organize; design first (e.g. journal or snapshot), then implement.

### 18.7 UI Indicators ✅ COMPLETED
- [x] Series list path mismatch indicator (Iteration 170)
- [x] Settings format change warning (Iteration 171)

### Implementation Priority
**P1 - Core:** 18.1, 18.2, 18.3 (single series)
**P2 - Batch:** 18.3 (mass editor), 18.5
**P3 - Polish:** 18.4, 18.6, 18.7

---

## EPIC 19: Auto-Matching Robustness (P1 - Critical) 🔴 HIGH PRIORITY

The auto-matching logic that matches downloaded files to series/issues must be rock-solid and practically fool-proof. Current issues include files from different series with similar names being incorrectly matched (e.g., "Deadman (2017)" files matched to "Deadman (2006)" series).

### 19.1 Year-Aware Matching ✅ COMPLETED (Iteration 173)
- [x] Extract year from release filename (already implemented in DdlReleaseParser)
- [x] Compare extracted year against series StartYear
- [x] Reject matches where year differs by more than tolerance (configurable, default 2)
- [x] Handle cases where year is missing from filename (flag as low confidence for ambiguous series)
- [x] Add AutoMatchSettings for configurable tolerance/behavior
- [x] Add API endpoints for settings
- [x] Add UI in Import settings tab

### 19.2 Series Name Disambiguation ✅ COMPLETED (Iteration 174)
- [x] Detect when multiple series share the same base name (already in 19.1)
- [x] Require stricter matching criteria when ambiguous series exist
- [x] Consider publisher in matching when available in release name
- [x] Add detailed confidence scoring breakdown (ConfidenceBreakdown class)
- [x] Add publisher match bonus/mismatch penalty settings
- [x] Add PreferPublisherMatchForAmbiguous setting to filter by publisher
- [x] Add RejectMismatchedPublishers setting for strict mode
- [x] Add Publisher Matching section in UI

### 19.3 Release Parser Improvements ✅ COMPLETED (Iteration 175)
- [x] Improve year extraction from various filename formats (brackets, standalone)
- [x] Handle volume indicators (Vol. 1, Vol. 2, v1, v2, Vol. One, (v1))
- [x] Detect reboot/revival indicators (New 52, Rebirth, Dawn of X, etc.)
- [x] Extract publisher hints from release group naming conventions (DC-Empire, Marvel-Minutemen)
- [x] Add disambiguation year detection for modern series runs
- [x] Add series version detection (Second Series, 2nd Series, etc.)
- [x] Add DdlParsedInfo properties: RebootIndicator, SeriesVersion, DisambiguationYear, PublisherHint

### 19.4 Match Verification & Confirmation ✅ COMPLETED (Iteration 176)
- [x] Add "low confidence" flag to questionable matches
  - Added `IsLowConfidence` property to `DdlMatchResult`
  - Added `LowConfidenceThreshold` setting (default: 70%)
- [x] Queue low-confidence matches for manual review instead of auto-importing
  - Enhanced `RequiresManualReview` logic with multiple triggers
  - Added `ReviewReason` property explaining why review is needed
- [x] Show match confidence score in Manual Import UI
  - Added `ShowMatchReasoning` setting (default: true)
  - UI shows detailed score breakdown when enabled
- [x] Option to require manual confirmation for first issue of any series
  - Added `RequireConfirmationForFirstIssue` setting (default: true)
  - Added `IsFirstIssueForSeries` property to `DdlMatchResult`
  - Checks if series has any existing file assets

### 19.5 Matching Audit & Logging ✓ COMPLETED (Iteration 177)
- [x] Log detailed matching decisions with reasoning
  - Created `MatchHistory` entity to store match decisions
  - Records parsed info, confidence, outcome, and explanations
  - Stores JSON-serialized score breakdown and reductions
- [x] Track match accuracy over time
  - `GetAccuracyStatsAsync` calculates accuracy metrics
  - Tracks verified correct/incorrect and unverified counts
  - Auto-import accuracy separate from overall accuracy
- [x] Flag series with frequent mismatches for review
  - `GetProblematicSeriesAsync` identifies high-mismatch series
  - Returns mismatch rate and last mismatch date
- [x] Add "Match History" view to see what was matched and why
  - Added MatchStatisticsSection to Import Settings
  - Shows total matches, accuracy rate, confidence averages
  - Color-coded stats for visual clarity

### Implementation Priority
**All items are P1 - this is critical functionality that affects data integrity.**

---

## EPIC 20: Performance Optimization ✅ COMPLETED

Systematic performance improvements across backend database queries, API endpoints, background services, and frontend rendering. All high-impact items complete; remaining enhancements moved to EPIC 14.

### 20.1 Database Query Optimization ✅ COMPLETED (Iteration 182)
Optimize EF Core queries to eliminate N+1 issues and reduce memory usage.

**Items:**
- [x] **Fix N+1 query in Series sorting by issue count**
  - Changed `s.Issues.Count` to `s.Issues.Count()` method call for proper SQL translation
- [x] **Add AsSplitQuery to multi-collection includes**
  - Added to: `SeriesEndpoints.cs` (series list, deletion preview), `LibraryOrganizationService.cs` (3 methods)
  - Prevents cartesian explosion from Series × Issues × Editions joins
- [x] **Optimize History endpoint pagination**
  - Refactored to get proper total counts from database
  - Order at database level before materialization
  - Reduced over-fetching from `pageSize * 2` to `page * pageSize`

**Effort:** M | **Priority:** P1

### 20.2 Database Index Optimization ✅ COMPLETED (Iteration 185)
Add missing indexes for common query patterns.

**Items:**
- [x] **Add composite indexes for common queries**
  - Added `IX_Issues_Status` for wanted issue queries
  - Added `IX_Issues_Status_StoreDate` for pull list date queries
  - Added `IX_Issues_Monitored_Status` for combined filters
  - Added `IX_Series_Monitored` for monitored series queries
  - Verified existing `(MatchedSeriesId, Timestamp)` index is effective

**Effort:** S | **Priority:** P2

### 20.3 Background Service Optimization ✅ DONE (Iteration 194)
Improved efficiency of background processing.

**Items:**
- [x] **Parallelize DDL import processing**
  - Uses `Parallel.ForEachAsync` with configurable max concurrency (default: 3)
  - Added `ddl_auto_import_max_concurrent` setting
- [x] **Make auto-search batch size configurable**
  - Added `AutoSearchBatchSize` to `SearchSettings` (default: 50)
  - Replaces hardcoded value
- [x] **Optimize MatchHistoryService stats calculation**
  - Replaced `ToListAsync()` + in-memory aggregation
  - Uses `CountAsync`, `AverageAsync`, `MinAsync`, `MaxAsync`, `GroupBy`

**Effort:** M | **Priority:** P2

### 20.4 Frontend Virtualization ✅ COMPLETED (Iteration 183)
Add virtual scrolling for large lists to reduce DOM nodes and improve performance.

**Items:**
- [x] **Add virtualization library**
  - Installed `@tanstack/react-virtual`
- [x] **Virtualize Log viewer**
  - File: `LogsPage.tsx`
  - Renders only visible rows (~20-30) instead of all 500+ lines
  - Maintains auto-scroll to bottom functionality

**Note:** Additional virtualization (Series grid, tables, Pull List) deferred to EPIC 14 - existing pagination is sufficient.

**Effort:** M | **Priority:** P1

### 20.5 Frontend Image Optimization ✅ COMPLETED (Iteration 181)
Optimize image loading for faster perceived performance.

**Items:**
- [x] **Add lazy loading to cover images**
  - Added `loading="lazy"` and `decoding="async"` to all `<img>` tags
  - Affected: `SeriesDetailPage`, `PullListPage`, `SeriesPage`, `Dashboard`, `CalendarPage`, `EditionDetailPage`
- [x] **Add placeholder/skeleton states for images**
  - Created reusable `CoverImage` component with CSS pulse animation skeleton

**Note:** Native `loading="lazy"` is sufficient; intersection observer deferred to EPIC 14.

**Effort:** S | **Priority:** P1

### 20.6 Frontend Component Memoization ✅ COMPLETED (Iteration 186)
Prevent unnecessary re-renders with React.memo and proper hook usage.

**Items:**
- [x] **Memoize list item components**
  - `SeriesSearchResult` (SeriesPage.tsx) - memoized with useCallback handlers
  - `IssueCoverCard` (SeriesDetailPage.tsx) - memoized with useCallback handlers
  - `IssueListRow` (SeriesDetailPage.tsx) - memoized
  - `QueueItemCard` (ActivityPage.tsx) - memoized
  - `StatusCard` (Dashboard.tsx) - memoized
- [x] **Review useCallback/useMemo usage**
  - Added useCallback for image error handlers
  - Extracted constant objects outside components

**Effort:** S | **Priority:** P2

### 20.7 API Call Optimization ✅ COMPLETED (Iteration 195)
Reduced unnecessary network requests and optimized data fetching patterns.

**Items:**
- [x] **Parallelize PullListPage API calls**
  - Changed from sequential `for` loop to `Promise.all`
  - Fetches 4 weeks in parallel (4x faster)
- [x] **Optimize refetch behavior**
  - Added `refetchIntervalInBackground: false` to all polling queries
  - Pauses background polling when tab not visible (saves bandwidth)

**Note:** Server-side pagination and batched endpoints moved to EPIC 14 (enhancement, not required).

**Effort:** M | **Priority:** P2

### 20.8 Bundle Optimization ✅ DONE (Iteration 196)
Reduced frontend bundle size and improved initial load time.

**Items:**
- [x] **Add bundle analysis tooling**
  - Installed `rollup-plugin-visualizer`
  - Generates `bundle-stats.html` on build
- [x] **Code split heavy pages**
  - 9 pages now lazy-loaded: SettingsPage, PullListPage, CollectionsPage,
    EditionDetailPage, ManualImportPage, HistoryPage, WantedPage, CalendarPage, LogsPage
- [x] **Manual chunks for vendor code**
  - `react-vendor`: react, react-dom, react-router-dom (47 KB)
  - `query`: @tanstack/react-query (36 KB)
  - `icons`: lucide-react (18 KB)
- [x] **Lazy load heavy components**
  - Suspense wrapper with loading state

**Results:**
- Initial bundle: 665 KB → 410 KB (38% reduction)
- SettingsPage (180 KB) loaded on-demand only when needed
- Better browser caching (vendor chunks change less frequently)

**Effort:** M | **Priority:** P3

### Implementation Priority

**P1 - High Impact (Do First):**
- 20.1 Database Query Optimization
- 20.4 Frontend Virtualization
- 20.5 Frontend Image Optimization

**P2 - Medium Impact:**
- 20.2 Database Index Optimization
- 20.3 Background Service Optimization
- 20.6 Frontend Component Memoization
- 20.7 API Call Optimization

**P3 - Lower Priority:**
- 20.8 Bundle Optimization

### Metrics to Track
| Metric | Current | Target |
|--------|---------|--------|
| Series list load time | TBD | < 200ms |
| Series detail page load | TBD | < 500ms |
| Pull list page load | TBD | < 1s |
| Initial bundle size | TBD | < 500KB |
| Lighthouse performance score | TBD | > 80 |

---

## EPIC 21: Test Stabilization & Quality Gates 🔴 HIGH PRIORITY

### 21.1 Fix Existing Test Failures ✅ DONE (Iteration 189)
All 2529 tests now passing (was 45 failing, 2485 passing).

**Fixed Test Categories:**
- [x] **DDL Site Management** (2 tests) - Updated RCO default disabled expectations
- [x] **Activity Service** (4 tests) - Added test isolation via IAsyncLifetime
- [x] **Metron Client** (10 tests) - Fixed mock setup for IServiceProvider and HttpClient BaseAddress
- [x] **GetComics Adapter** (8 tests) - Updated HTML fixtures to match parser's article regex
- [x] **DDL Release Parser** (4 tests) - Aligned expectations with actual parser behavior
- [x] **PullList Service** (8 tests) - Fixed EF Core InMemory GroupBy issues
- [x] **Cover Service** (1 test) - Fixed HttpClient reuse in mock
- [x] **Endpoint/Swagger** (4 tests) - Removed duplicate DTOs causing schema conflicts
- [x] **Download Host Resolver** (1 test) - Updated URL (Mega now supported)
- [x] **Golden Tests** (1 test) - Aligned 'Absolute' edition expectations

**Effort:** L | **Priority:** P1 (blocking quality gates)

### 21.2 Establish Test Baseline ✅ DONE (Iteration 193)

- [x] Document current test count in `docs/TEST_BASELINE.md`
- [x] Add pre-commit hook for test regression prevention
- [x] Verified no flaky tests (2 consecutive runs passed)

**Baseline**: 2541 tests, 0 failures

**Effort:** S | **Priority:** P1

### 21.3 Audit Git History for Masked Bugs ✅ DONE (Iteration 190)

Comprehensive review of all test changes in git history. Created `docs/DECISIONS.md` with findings.

**Findings:**
- [x] AUDIT-001: GetComicsAdapter lost 5 methods in V2 rename → **Bug, needs fix (21.4)**
- [x] AUDIT-002: DdlReleaseParser regex truncates hyphenated groups → **Bug, needs fix (21.5)**
- [x] AUDIT-003: "Absolute" edition detection → Missing feature (documented)
- [x] AUDIT-004: "Marvel NOW" reboot indicator → Missing feature (documented)

**Legitimate Fixes Verified:**
- ActivityService isolation, MetronClient mocks, GetComicsAdapter HTML fixtures
- Swagger duplicate DTOs, RCO default status, CoverService mocks, Mega support

**Effort:** L | **Priority:** P1

### 21.4 Fix GetComicsAdapter Feature Regression ✅ DONE (Iteration 191)

**Classification**: AUDIT-001 - Critical regression bug

Restored 6 methods lost during V2 rename (commit `a6192fe`):
- [x] `GetRssFeedAsync(int limit, CancellationToken)` - Get latest from RSS
- [x] `GetCategoryAsync(string category, int limit, CancellationToken)` - Browse by category
- [x] `GetCategoryRssFeedAsync(string category, int limit, CancellationToken)` - Category RSS
- [x] `GetPublisherRssFeedAsync(string publisher, int limit, CancellationToken)` - Publisher RSS
- [x] `GetPublisherAsync(string publisher, int limit, CancellationToken)` - Browse by publisher
- [x] `GetAvailableCategories()` - List all categories with display names

Also restored 12 deleted tests from commit `4d4afa9`.
Test count: 2529 → 2541

**Effort:** M | **Priority:** P1 (feature regression)

### 21.5 Fix DdlReleaseParser Release Group Regex ✅ DONE (Iteration 192)

**Classification**: AUDIT-002 - Medium code bug

Fixed release group extraction and reordered parsing pipeline:

1. **Regex fix**: Changed `[^-]+?` to `[A-Za-z][\w-]+` to capture hyphens
2. **Reordered pipeline**: Extract release group BEFORE inline publisher extraction

**Before:**
```
Input:  "Batman 001 (2023) - DC-Empire.cbz"
Result: ReleaseGroup = "Empire", Publisher = "DC"
```

**After:**
```
Input:  "Batman 001 (2023) - DC-Empire.cbz"
Result: ReleaseGroup = "DC-Empire", Publisher = "DC Comics", PublisherHint = "DC Comics"
```

- [x] `ReleaseGroupPublishers` dictionary now correctly used
- [x] Test expectations restored ("DC Comics", "Image Comics")

**Effort:** S | **Priority:** P2 (quality improvement)

---

## EPIC 22: Git Flow, PR Governance & SemVer Releases 🔄 IN PROGRESS

Migrate from direct `dev` pushes to a PR-first workflow targeting `main`, with branch protections and semantic-versioned releases.

### 22.1 Branch Strategy Migration (`dev` -> `main`) ✅ COMPLETED (Iteration 243)
- [x] Decide and document target flow:
  - short-lived feature branches from `main`
  - PRs merge into `main`
  - no direct commits to protected branches
- [x] Create/sync `main` from current stable tip and set as default branch on GitHub.
- [x] Update local and CI references that currently assume `dev` as default branch.
- [x] Retire **`dev`:** CI runs on **`main` only** (push + `pull_request`); **`docs/CONTRIBUTING.md`** documents trunk workflow; **`.github/SECURITY.md`** supported-versions wording updated. Delete remote **`dev`** in GitHub after open `dev` → `main` PRs are merged.

**Acceptance:** `main` is default branch; active development branches PR into `main`; `dev` no longer required for day-to-day work.

**Effort:** M | **Priority:** P1

### 22.2 PR Governance & Branch Protection 📋 PLANNED
- [ ] Enable branch protection on `main`:
  - require pull request before merge
  - require up-to-date branch before merge
  - require status checks to pass
  - restrict force-push/deletion
- [ ] Require at least one review (CODEOWNERS optional but recommended).
- [ ] Define allowed merge strategy (squash vs merge commit) and enforce it.
- [x] Add PR template with sections for summary, test plan, risk, rollback notes — **`.github/pull_request_template.md`** (Iteration 240: *Security impact* subsection for auth/deps/CI changes).

**Acceptance:** No direct pushes to `main`; all merges happen through reviewed PRs with passing checks.

**Effort:** S | **Priority:** P1

### 22.3 CI as Required Status Checks 📋 PLANNED
- [ ] Ensure CI workflow names are stable and suitable for required checks.
- [ ] Required checks include at least:
  - backend build
  - backend tests
  - frontend build
- [ ] Add optional non-blocking checks (lint, docs validation, dependency scan) as advisory.
- [x] Document expected check runtime and troubleshooting step pointers — **`docs/CONTRIBUTING.md`** (CI bullets) and **`docs/SECURITY.md`** (CI checks table; Iteration 240). **Job IDs** in `.github/workflows/ci.yml`: `build-and-test`, `npm-audit` (matrix: UI / E2E), `secret-scan`, `osv-scan`, `docker-build`.

**Acceptance:** PR merge is blocked until required CI checks pass.

**Effort:** S | **Priority:** P1

### 22.4 Conventional Commits + SemVer Rules 📋 PLANNED
- [ ] Formalize mapping from commit type to version bump:
  - `fix` -> patch
  - `feat` -> minor
  - `BREAKING CHANGE`/`!` -> major
- [ ] Ensure commit/PR title conventions are compatible with automated release tooling.
- [ ] Document examples and edge cases (revert, docs-only, chore-only).

**Acceptance:** Version bump logic is deterministic and documented for maintainers.

**Effort:** S | **Priority:** P1

### 22.5 Automated Release Pipeline (Tags + Notes) 📋 PLANNED
- [ ] Add release workflow (on merge to `main` or manual dispatch) that:
  - computes next SemVer
  - creates git tag (`vX.Y.Z`)
  - generates changelog/release notes from commits/PRs
  - publishes GitHub Release
- [ ] Prevent duplicate/parallel release runs.
- [ ] Include dry-run mode for validation.

**Acceptance:** Merging release-worthy PRs to `main` reliably produces tagged SemVer releases with notes.

**Effort:** M | **Priority:** P1

### 22.6 Version Surfacing in App/API 📋 PLANNED
- [ ] Ensure release version is injected into backend/frontend build metadata.
- [ ] Expose release version in system status endpoint and UI footer consistently.
- [ ] Add tests or smoke checks for version presence.

**Acceptance:** Running app clearly shows the released SemVer and commit metadata.

**Effort:** S | **Priority:** P2

### 22.7 Release Operations & Rollback Playbook 📋 PLANNED
- [ ] Document release checklist (preflight, deploy, verification, rollback).
- [ ] Define hotfix flow (`hotfix/*` from `main`) and patch release process.
- [ ] Add incident guidance for bad release recovery and tag correction policy.

**Acceptance:** Maintainers can execute and recover releases without tribal knowledge.

**Effort:** S | **Priority:** P2

### 22.8 Security/Compliance for Release Automation 📋 PLANNED
- [x] **Document** least-privilege scoping and rotation for GitHub tokens/secrets in Actions — **`docs/SECURITY.md`** *GitHub Actions secrets and future release workflows* (Iteration 240). **Apply:** maintainers must create PATs/environments with minimum scope and rotate on schedule or incident when release automation lands.
- [x] Ensure no secrets leak in logs/artifacts — **partial (Iteration 238):** `docs/SECURITY.md` documents Docker image hygiene (no baked secrets), CI secret scanning (Gitleaks), and contributor reporting via `.github/SECURITY.md`. **Release** workflow log/artifact review still TBD when EPIC 22 adds publish jobs.
- [x] Gate dependency/license/security scans as required or advisory before release — **partial (Iteration 235 + 240):** **`main`** **CI** runs NuGet Audit, npm audit (high+), **OSV-Scanner** on npm lockfiles, and Gitleaks; Dependabot opens update PRs. Dedicated **release** workflow gates still TBD when EPIC 22 automates releases.

**Acceptance:** Release automation follows least-privilege and existing security policy.

**Effort:** S | **Priority:** P2

---

## Story Ordering Notes

**✅ EPIC 21.1 COMPLETE (Iteration 189)**
All 2529 tests passing. Quality gates in CONTINUE.md are now effective.

**✅ EPIC 21.3 COMPLETE (Iteration 190)**
Git history audit complete. Found 2 masked bugs, 2 documented missing features.
Created `docs/DECISIONS.md` with full findings.

**✅ EPIC 21.4 COMPLETE (Iteration 191)**
Restored 6 methods and 12 tests. GetComicsAdapter has full RSS/category/publisher support again.

**✅ EPIC 21.5 COMPLETE (Iteration 192)**
Parser now correctly extracts "DC-Empire" and looks up "DC Comics" from dictionary.

**✅ EPIC 21.2 COMPLETE (Iteration 193)**
Test baseline established at 2541 tests. Pre-commit hook prevents regression.

### 21.6 Fix Premium Host Resolver Tests ✅ DONE (Iteration 199)

3 integration tests were failing due to external service unpredictability.

**Root Cause:** Tests hit real external services (Mega.nz, Rapidgator, Uploaded.net) which returned unpredictable responses.

**Classification:** Test Bug - tests were integration tests masquerading as unit tests.

**Fix:**
- [x] Replaced integration tests with properly mocked unit tests
- [x] Created testable resolver subclasses that inject mock HTTP handlers
- [x] Tests now verify specific scenarios with deterministic responses:
  - Mega: API returns -9 (file not found), API returns -3 (error)
  - Rapidgator: 403 (auth required), 404 (file not found)
  - Uploaded: 403 (auth required), 404 (file not found)
- [x] All 2544 tests now pass (added 3 more specific test cases)

**Effort:** S | **Priority:** P0 (BLOCKING)

**Dependencies:**
- **EPIC 21** - No dependencies, blocks all other work (quality gates require passing tests)
- EPIC 9 depends on EPIC 1 (Series/Issue entities) and EPIC 5 (UI shell)
- EPIC 11 depends on EPIC 9 (ComicVine Integration) for release date metadata
- EPIC 12 has no hard dependencies; can be implemented incrementally
- EPIC 14 contains standalone enhancements with varied dependencies
- EPIC 18 depends on EPIC 2 (Import Pipeline) for file organization patterns
- EPIC 19 depends on EPIC 2 (Import Pipeline) and EPIC 8 (DDL Site Adapters) for matching context
- EPIC 20 ✅ COMPLETED (Iterations 181-196) - All high-impact items done; enhancements moved to EPIC 14
