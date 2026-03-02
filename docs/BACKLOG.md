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
| 📋 | [EPIC 20](#epic-20-performance-optimization--planned) | Performance Optimization | Planned |

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
- 14.7.1 Code Architecture Review
- 14.7.2 Cover Source Integration Testing
- 14.7.3 Unit Test Coverage Expansion
- 14.7.4 Refactoring Candidates
- 14.7.5 Edge Case Handling

#### 14.12 Future Week Cover Enrichment Improvements 📋 READY
Fix issue where future weeks show volume/series images instead of actual issue covers from Metron.

**Current Behavior:**
The cover enrichment pipeline tries sources in this order:
1. ComicVine issue cover (often unavailable for future issues)
2. Metron via CV issue ID (requires CV to have indexed the issue)
3. Metron via CV volume ID + issue number
4. Metron via series name + issue number (fuzzy search)
5. ComicVine volume cover (fallback - shows generic series cover)

**Problem:**
For future weeks, ComicVine often hasn't indexed issues yet, causing the system to fall back to volume covers prematurely. Metron typically has covers for upcoming issues but lookups may be failing.

**Investigation Items:**
- [ ] **Debug Metron lookup for future issues**
  - Add logging to track why Metron lookups fail for issues without CV IDs
  - Check if volume ID → issue number mapping is working
  - Verify fuzzy series name search confidence thresholds
- [ ] **Check rate limiting impact**
  - Metron has rate limits - verify enrichment isn't being throttled
  - Consider spreading enrichment requests more evenly

**Fixes:**
- [ ] **Improve CV-less issue matching in Metron**
  - For issues without CV ID, use volume name + issue number directly
  - Consider WalkSoftly series name as additional match input
  - Lower confidence threshold for future issues (they're more likely correct)
- [ ] **Prioritize future weeks for enrichment**
  - Already partially done (future weeks processed first)
  - Add more aggressive retry for future weeks still showing volume covers
- [ ] **Add re-enrichment trigger**
  - When a new week becomes "current", force re-check issues with volume fallback
  - These issues are more likely to have Metron/CV covers now
- [ ] **UI indicator for cover source**
  - Show badge/tooltip indicating "Series Cover" vs "Issue Cover"
  - Helps users understand why cover looks generic
- [ ] **Manual re-enrich action**
  - Add "Refresh Covers" button on Pull List page
  - Forces re-enrichment for visible weeks

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
- [ ] **Update Issue Search/Lookup** (deferred - future enhancement)
- [ ] **Update Edition/Collection Search** (deferred - future enhancement)
- [ ] **UI hint for ID input** (deferred - future enhancement)

**Effort:** S | **Priority:** P2

### Remaining Deferred Items
| Item | EPIC | Effort | Status |
|------|------|--------|--------|
| Character/team appearances | 9 | M | Foundation complete |
| Usenet/NZB from DDL sites | 8 | M | Ready |
| Folder download (Dropbox/Drive) | 8 | M | Ready |
| ~~Distributed cache pub/sub~~ | 12 | L | ✅ COMPLETED (Iteration 180) |

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
- [ ] Atomic operations (per-series) - Deferred
- [ ] Undo support (stretch goal) - Deferred

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

## EPIC 20: Performance Optimization 📋 PLANNED

Systematic performance improvements across backend database queries, API endpoints, background services, and frontend rendering.

### 20.1 Database Query Optimization ✅ COMPLETED (Iteration 182)
Optimize EF Core queries to eliminate N+1 issues and reduce memory usage.

**Items:**
- [x] **Fix N+1 query in Series sorting by issue count**
  - Changed `s.Issues.Count` to `s.Issues.Count()` method call for proper SQL translation
- [x] **Add AsSplitQuery to multi-collection includes**
  - Added to: `SeriesEndpoints.cs` (series list, deletion preview), `LibraryOrganizationService.cs` (3 methods)
  - Prevents cartesian explosion from Series × Issues × Editions joins
- [ ] **Paginate large result sets in organization service** - Deferred
  - Would require API contract changes; AsSplitQuery mitigates the issue
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
- [ ] **Add full-text indexes for search queries** (deferred - requires SQLite FTS5 setup)

**Effort:** S | **Priority:** P2

### 20.3 Background Service Optimization 📋 READY
Improve efficiency of background processing.

**Items:**
- [ ] **Parallelize DDL import processing**
  - File: `DdlImportBackgroundService.cs` lines 139-254
  - Issue: Sequential processing in foreach loop
  - Fix: Use `Parallel.ForEachAsync` with concurrency limits
- [ ] **Make auto-search batch size configurable**
  - File: `AutoSearchBackgroundService.cs` line 134
  - Issue: Hardcoded `maxIssuesPerRun = 50`
  - Fix: Add to settings with dynamic batching based on system load
- [ ] **Optimize MatchHistoryService stats calculation**
  - File: `MatchHistoryService.cs` lines 214-215
  - Issue: Loads all records into memory for stats
  - Fix: Use database aggregation queries

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
- [ ] **Virtualize Series issue grid (cover view)** - Deferred
  - Already has pagination (max 192 items per page)
  - Complex 2D grid virtualization for future iteration
- [ ] **Virtualize Series table** - Deferred
  - Would benefit from virtualization but lower priority
- [ ] **Virtualize Pull List discovery items** - Deferred
  - Grouped by week, requires more complex implementation

**Effort:** M | **Priority:** P1

### 20.5 Frontend Image Optimization ✅ COMPLETED (Iteration 181)
Optimize image loading for faster perceived performance.

**Items:**
- [x] **Add lazy loading to cover images**
  - Added `loading="lazy"` and `decoding="async"` to all `<img>` tags
  - Affected: `SeriesDetailPage`, `PullListPage`, `SeriesPage`, `Dashboard`, `CalendarPage`, `EditionDetailPage`
- [x] **Add placeholder/skeleton states for images**
  - Created reusable `CoverImage` component with CSS pulse animation skeleton
- [ ] **Implement intersection observer for manual lazy loading** - Deferred
  - Native lazy loading is sufficient for current use cases

**Effort:** S | **Priority:** P1

### 20.6 Frontend Component Memoization 📋 READY
Prevent unnecessary re-renders with React.memo and proper hook usage.

**Items:**
- [ ] **Memoize list item components**
  - `SeriesSearchResult` (SeriesPage.tsx:702)
  - `IssueCoverCard` (SeriesDetailPage.tsx)
  - `IssueListRow` (SeriesDetailPage.tsx)
  - `QueueItemCard` (ActivityPage.tsx:83)
  - `StatusCard` (Dashboard.tsx:128)
- [ ] **Review useCallback/useMemo usage**
  - Ensure event handlers passed to memoized components use useCallback
  - Verify dependencies are correct

**Effort:** S | **Priority:** P2

### 20.7 API Call Optimization 📋 READY
Reduce unnecessary network requests and optimize data fetching patterns.

**Items:**
- [ ] **Server-side pagination for SeriesDetailPage issues**
  - File: `SeriesDetailPage.tsx` line 249
  - Issue: Fetches 500 issues, paginates client-side
  - Fix: Implement server-side pagination endpoint
- [ ] **Parallelize PullListPage API calls**
  - File: `PullListPage.tsx` lines 169-196
  - Issue: Sequential API calls (4 weeks × 1 call)
  - Fix: Use `Promise.all` for parallel fetching
- [ ] **Optimize refetch behavior**
  - Reduce `refetchInterval` when tab not visible
  - Disable `refetchOnWindowFocus` for less critical data
- [ ] **Consider batched API endpoints**
  - Add endpoints that return multiple weeks of data in one call

**Effort:** M | **Priority:** P2

### 20.8 Bundle Optimization 📋 PLANNED
Reduce frontend bundle size and improve initial load time.

**Items:**
- [ ] **Add bundle analysis tooling**
  - Install `rollup-plugin-visualizer`
  - Identify large dependencies
- [ ] **Code split SettingsPage**
  - File is 7,500+ lines
  - Split into lazy-loaded sub-pages
- [ ] **Verify tree-shaking for lucide-react**
  - Ensure only used icons are included
- [ ] **Lazy load heavy components**
  - Modal dialogs, settings tabs, etc.

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

## Story Ordering Notes

**Dependencies:**
- EPIC 9 depends on EPIC 1 (Series/Issue entities) and EPIC 5 (UI shell)
- EPIC 11 depends on EPIC 9 (ComicVine Integration) for release date metadata
- EPIC 12 has no hard dependencies; can be implemented incrementally
- EPIC 14 contains standalone enhancements with varied dependencies
- EPIC 18 depends on EPIC 2 (Import Pipeline) for file organization patterns
- EPIC 19 depends on EPIC 2 (Import Pipeline) and EPIC 8 (DDL Site Adapters) for matching context
- EPIC 20 has no hard dependencies; items can be implemented incrementally in any order
