# Backlog

## EPIC 0: Repo Skeleton (FOUNDATION) ✅ COMPLETED
- [x] Create .NET solution structure:
  - src/Shortboxerr.Api
  - src/Shortboxerr.Core
  - src/Shortboxerr.Infrastructure
  - tests/Shortboxerr.Tests
- [x] Health endpoint + Swagger
- [x] SQLite migrations scaffold
- [x] Dockerfile + docker-compose
- [x] CI workflow (build + test)
- [x] Dev Container config (verify dotnet build/test run inside container)

## EPIC 1: Domain + Persistence (MINIMUM DATA MODEL) ✅ COMPLETED
- [x] Entities: Series, Issue, EditionTitle (Collections), FileAsset, HistoryEvent
- [x] Repositories + EF Core mappings (SQLite)
- [x] CRUD endpoints for Series + Collections
- [x] Filtering/paging conventions aligned with Arr APIs

## EPIC 2: Import Pipeline (MYLAR3-LIKE) ✅ COMPLETED
- [x] Staging folder model + endpoints
- [x] Filename parser (singles + collections)
- [x] Manual Import endpoints and basic UI contract
- [x] Atomic move/rename preview
- [x] History events for pipeline steps

## EPIC 3: DecisionEngine (MYLAR3-LIKE SELECTION) ✅ COMPLETED
- [x] Candidate model + rejection reasons
- [x] Ranking/scoring + deterministic tie-break
- [x] Explanation report surfaced to API
- [x] Golden test harness skeleton

## EPIC 4: Indexers + Download Clients (ARR-LIKE SHAPE)

### 4.1 Provider Abstractions ✅ COMPLETED
- [x] **IProvider interface**: Base abstraction for all provider types
  - AC: Defines Name, Type, IsEnabled, Test(), GetHealth()
- [x] **IIndexerProvider**: Extends IProvider for search/discovery
  - AC: Defines Search(query), GetLatest(), SupportsRss
- [x] **IDownloadProvider**: Extends IProvider for acquisition
  - AC: Defines Download(candidate), GetStatus(id), Cancel(id)
- [x] **ProviderManager**: Registry for all configured providers
  - AC: CRUD operations, priority ordering, enable/disable

### 4.2 DDL Provider (Mylar3-Compatible) - FIRST-CLASS PROVIDER TYPE
The DDL (Direct Download) provider must achieve behavioral parity with Mylar3's DDL functionality.

#### 4.2.1 DDL Discovery & Search ✅ COMPLETED
- [x] **DDL site adapter interface (IDdlSiteAdapter)**
  - AC: Each supported DDL site has its own adapter implementing parsing logic
  - AC: Adapters handle site-specific HTML/JSON parsing
  - AC: Must support: GettyComics, ReadComicOnline, and extensible for others
- [x] **DDL search endpoint polling**
  - AC: Configurable poll interval (default matches Mylar3)
  - AC: Supports series-specific and global searches
  - AC: Rate limiting per site (match Mylar3 defaults)
- [x] **DDL link discovery**
  - AC: Extract download links from pages (direct links, redirects, hosters)
  - AC: Handle multi-part releases
  - AC: Detect and skip dead/expired links

#### 4.2.2 DDL Candidate Normalization ✅ COMPLETED
- [x] **DDL release parser**
  - AC: Parse release names into structured candidates (series, issue, year, format, quality)
  - AC: Must match Mylar3 parsing rules exactly (fixture tests required)
- [x] **DDL candidate model**
  - AC: Fields: SourceSite, ReleaseTitle, ParsedInfo, DownloadLinks[], Size, DateFound
  - AC: Quality scoring aligned with DecisionEngine
- [x] **DDL filtering rules**
  - AC: Banned words filter (match Mylar3 defaults: sample, preview, etc.)
  - AC: Required words filter
  - AC: Size limits (min/max for singles and collections)
  - AC: Format preference (cbz > cbr, configurable)

#### 4.2.3 DDL Download Execution ✅ COMPLETED
- [x] **DDL downloader service**
  - AC: HTTP client with configurable timeouts (match Mylar3)
  - AC: User-Agent rotation/configuration
  - AC: Cookie/session handling for authenticated sites
  - AC: Resume support for interrupted downloads
- [x] **DDL retry semantics**
  - AC: Configurable retry count (default: 3, match Mylar3)
  - AC: Exponential backoff between retries
  - AC: Alternate mirror fallback if primary fails
- [x] **DDL failure handling**
  - AC: Mark candidate as failed after max retries
  - AC: Log detailed failure reason (timeout, 404, auth, corrupt)
  - AC: Quarantine repeated failures (configurable threshold)
  - AC: History event for all download attempts

#### 4.2.4 DDL → Import Handoff ✅ COMPLETED
- [x] **DDL post-download processing**
  - AC: Verify downloaded file (size, magic bytes, not HTML error page)
  - AC: Move to staging folder on success
  - AC: Trigger import pipeline (or queue for manual review based on config)
- [x] **DDL import integration**
  - AC: Auto-match to series/issue using parsed candidate info
  - AC: Respect "auto-import" vs "manual review" setting (match Mylar3)
  - AC: Create HistoryEvent linking download → import

### 4.3 DDL Configuration & Mylar3 Import ✅ COMPLETED
- [x] **DDL provider entity + settings**
  - AC: Entity: DdlProvider (Name, SiteType, BaseUrl, Credentials, Enabled, Priority)
  - AC: Per-provider settings: RateLimit, Timeout, RetryCount, UserAgent
- [x] **Mylar3 DDL settings import**
  - AC: Parse Mylar3 config.ini DDL sections
  - AC: Map DDL provider configs to Shortboxerr DdlProvider entities
  - AC: Import DDL-specific credentials securely
  - AC: Validation report showing mapped vs. unsupported settings
- [x] **DDL provider defaults**
  - AC: Ship with Mylar3-equivalent default settings in config/defaults.mylar3.json
  - AC: Document any deviations from Mylar3 behavior

### 4.4 DDL Conformance Tests (Mylar3 Parity) ✅ COMPLETED
- [x] **DDL parsing fixture tests**
  - AC: Golden test files with Mylar3 release names → expected parsed output
  - AC: Must pass 100% to claim Mylar3 parity
- [x] **DDL filtering fixture tests**
  - AC: Test cases for banned words, required words, size limits
  - AC: Fixtures derived from Mylar3 test cases or observed behavior
- [x] **DDL retry/failure fixture tests**
  - AC: Simulate timeout, 404, corrupt file scenarios
  - AC: Verify retry count, backoff timing, failure state transitions
- [x] **DDL integration tests**
  - AC: Mock DDL site responses
  - AC: End-to-end: discovery → candidate → download → staging

### 4.5 DDL UI (Arr-Style)
- [ ] **DDL provider list page**
  - AC: Table showing all DDL providers with status (healthy/unhealthy/disabled)
  - AC: Enable/disable toggle per provider
  - AC: Priority drag-and-drop reordering
- [ ] **DDL provider add/edit modal**
  - AC: Form fields for site type, URL, credentials, rate limit, etc.
  - AC: "Test" button that validates connectivity and authentication
  - AC: Test result shows: connection status, auth status, sample search result count
- [ ] **DDL provider test endpoint**
  - AC: POST /api/v1/providers/ddl/{id}/test
  - AC: Returns: { success, message, sampleResults, latencyMs }
- [ ] **DDL activity feed**
  - AC: Show recent DDL searches, downloads, failures
  - AC: Filterable by provider, status, date range

### 4.6 Generic Indexer/Download Client Support ✅ COMPLETED
- [x] **RSS/Atom indexer adapter**
  - AC: Poll RSS feeds for new releases
  - AC: Parse feed items into candidates
- [x] **Generic HTTP download client**
  - AC: Simple URL → file download
  - AC: For use with RSS-discovered direct links
- [x] **Torrent client abstraction** (placeholder for future)
  - AC: Interface only, no implementation in EPIC 4

### 4.7 DDL Parser Enhancements (Mylar3 Parity) ✅ COMPLETED
Address edge cases documented in `ddl_parsing_golden.json` aspirationalTests section.

- [x] **Publisher extraction improvement**
  - AC: Extract publisher from parentheses when followed by year: `Wolverine 0001 (Marvel) (2024).cbz`
  - AC: Handle multiple parenthetical metadata groups in any order
- [x] **Quality tag extraction**
  - AC: Extract quality tags (Webrip, Digital, Scan) reliably: `Action Comics 1050 (2023) (Webrip).cbz`
  - AC: Handle quality tags in parentheses and as standalone tokens
- [x] **Separator normalization**
  - AC: Normalize underscores to spaces before parsing: `Wonder_Woman_001_(DC)_(2023).cbz`
  - AC: Normalize periods to spaces (except file extension): `Aquaman.001.2023.Digital.cbz`
  - AC: Pre-processing step preserves file extension
- [x] **Hyphen-separated subtitles**
  - AC: Handle `Series - Subtitle` patterns: `Star Wars - Darth Vader 001 (Marvel) (2020).cbz`
  - AC: Preserve subtitle in series title or extract as separate field
- [x] **Aspirational tests promoted to main tests**
  - AC: Move all aspirationalTests to main testCases array
  - AC: All tests must pass (100% parity)

## EPIC 5: UI (ARR-LIKE UI)
- [ ] UI shell + nav map (Dashboard/Series/Collections/Wanted/Activity/History/Manual Import/Settings)
- [ ] Series list page (table + bulk actions)
- [ ] Collections list page
- [ ] Activity + Manual Import pages (thin but functional)

## EPIC 6: Mylar3 Migration (BEHAVIORAL PARITY SETUP)
- [ ] Read Mylar3 SQLite DB (read-only)
- [ ] Transform to intermediate JSON snapshot
- [ ] Import into Shortboxerr DB
- [ ] Post-migration scan job
- [ ] Migration report

---

## Story Ordering Notes

**EPIC 4 Implementation Order:**
1. 4.1 Provider Abstractions (foundation)
2. 4.2.2 DDL Candidate Normalization (needed by DecisionEngine)
3. 4.2.1 DDL Discovery & Search (site adapters)
4. 4.2.3 DDL Download Execution
5. 4.2.4 DDL → Import Handoff
6. 4.3 DDL Configuration & Mylar3 Import
7. 4.4 DDL Conformance Tests
8. 4.5 DDL UI
9. 4.6 Generic Indexer/Download Client Support
10. 4.7 DDL Parser Enhancements (can be done anytime after 4.4)

**Dependencies:**
- 4.2.* depends on EPIC 3 (DecisionEngine) for candidate ranking
- 4.2.4 depends on EPIC 2 (Import Pipeline) for handoff
- 4.5 depends on EPIC 5 UI shell (can be implemented in parallel)
