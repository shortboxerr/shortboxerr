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
| 🔄 | [EPIC 11](#epic-11-weekly-pull-list-mylar3-parity--in-progress) | Weekly Pull List | In Progress |
| 🔄 | [EPIC 12](#epic-12-performance--caching-strategy--in-progress) | Performance & Caching | In Progress |
| ✅ | EPIC 13 | Logging & Diagnostics | [Full Details Below](#epic-13-logging--diagnostics-mylar3sonarradarr-parity--completed) |
| 📋 | [EPIC 14](#epic-14-future-enhancements--planned) | Future Enhancements | Planned |
| ✅ | EPIC 15 | UI Bug Fixes | [Archive](./COMPLETED.md#epic-15-ui-bug-fixes--improvements--completed) |
| ✅ | EPIC 16 | E2E Testing Infrastructure | [Archive](./COMPLETED.md#epic-16-end-to-end-testing-infrastructure--completed) |
| ✅ | EPIC 17 | DDL Download Robustness | [Archive](./COMPLETED.md#epic-17-ddl-download-link-robustness--completed) |
| 🔄 | [EPIC 18](#epic-18-library-organization--rename-sonarradarr-parity--in-progress) | Library Organization | In Progress |

**Legend:** ✅ Completed | 🔄 In Progress | 📋 Planned

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

## EPIC 11: Weekly Pull List (Mylar3 Parity) 🔄 IN PROGRESS

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

#### 11.26 Pull List: Local Caching of Metron Cover Images ← ON HOLD
On hold pending completion of 11.27 (Pull List Data Flow Refactoring).

#### 11.27 Pull List Data Flow Refactoring: Unified Enrichment Strategy 🔄 IN PROGRESS
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

**Remaining:**
- [ ] Update local cover caching (integrates 11.26)
- [x] Fix `/api/v1/covers/discovery/{id}` endpoint naming (Iteration 167)

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

#### 14.8 Series Deletion UX Improvements ✅ COMPLETED (Iteration 168)
- [x] Confirmation modal for series deletion
- [x] Deletion progress indicator
- [x] List refresh after deletion (via navigate to /series)
- [x] Backend: Cascade delete linked annual series

### Remaining Deferred Items
| Item | EPIC | Effort | Status |
|------|------|--------|--------|
| Character/team appearances | 9 | M | Foundation complete |
| Usenet/NZB from DDL sites | 8 | M | Ready |
| Folder download (Dropbox/Drive) | 8 | M | Ready |
| Distributed cache pub/sub | 12 | L | Ready (optional) |

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

### 18.4 File Rename Within Series
- [ ] Issue file rename preview (IssueFileFormat tokens)
- [ ] Edition/Collection file rename preview (CollectionFileFormat tokens)
- [ ] Conflict detection

### 18.5 Bulk Organization Tools 🔄 IN PROGRESS
- [x] "Organize All" system task (Iteration 169)
- [ ] Scheduled organization option (auto-organize on format change)

### 18.6 Safety & Rollback
- [ ] Dry-run mode
- [ ] Atomic operations (per-series)
- [ ] Undo support (stretch goal)

### 18.7 UI Indicators ✅ COMPLETED
- [x] Series list path mismatch indicator (Iteration 170)
- [x] Settings format change warning (Iteration 171)

### Implementation Priority
**P1 - Core:** 18.1, 18.2, 18.3 (single series)
**P2 - Batch:** 18.3 (mass editor), 18.5
**P3 - Polish:** 18.4, 18.6, 18.7

---

## Story Ordering Notes

**Dependencies:**
- EPIC 9 depends on EPIC 1 (Series/Issue entities) and EPIC 5 (UI shell)
- EPIC 11 depends on EPIC 9 (ComicVine Integration) for release date metadata
- EPIC 12 has no hard dependencies; can be implemented incrementally
- EPIC 14 contains standalone enhancements with varied dependencies
- EPIC 18 depends on EPIC 2 (Import Pipeline) for file organization patterns
