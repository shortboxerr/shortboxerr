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

### 4.5 DDL UI (Arr-Style) ✅ COMPLETED
- [x] **DDL provider list page**
  - AC: Table showing all DDL providers with status (healthy/unhealthy/disabled)
  - AC: Enable/disable toggle per provider
  - AC: Priority drag-and-drop reordering
- [x] **DDL provider add/edit modal**
  - AC: Form fields for site type, URL, credentials, rate limit, etc.
  - AC: "Test" button that validates connectivity and authentication
  - AC: Test result shows: connection status, auth status, sample search result count
- [x] **DDL provider test endpoint**
  - AC: POST /api/v1/providers/ddl/{id}/test
  - AC: Returns: { success, message, sampleResults, latencyMs }
- [x] **DDL activity feed**
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

## EPIC 5: UI (ARR-LIKE UI) ✅ COMPLETED
- [x] UI shell + nav map (Dashboard/Series/Collections/Wanted/Activity/History/Manual Import/Settings)
- [x] Series list page (table + bulk actions)
- [x] Collections list page
- [x] Activity + Manual Import pages (thin but functional)

## EPIC 6: Settings Persistence & UI Enhancements
- [x] **Theme persistence** ✅
  - AC: Save selected theme (dark/light/system) to database
  - AC: Load theme preference on app start
  - AC: API endpoint: GET/PUT /api/v1/settings/ui
- [x] **General settings persistence** ✅
  - AC: Save naming patterns, root folders to database
  - AC: Settings entity with key-value storage
  - AC: Settings API endpoints for all categories
- [ ] **API key management**
  - AC: Display API key in Settings > Security (masked by default)
  - AC: "Show" toggle to reveal full API key
  - AC: "Copy" button to copy API key to clipboard
  - AC: "Regenerate" button with confirmation dialog
  - AC: API endpoint: GET /api/v1/settings/apikey (returns masked), POST /api/v1/settings/apikey/regenerate
- [x] **Naming format token helper** ✅
  - AC: Display available tokens for Series Folder Format: `{Series Title}`, `{Series Year}`, `{Publisher}`, `{Status}`
  - AC: Display available tokens for Issue File Format: `{Series Title}`, `{Issue}`, `{Issue Title}`, `{Year}`, `{Publisher}`, `{Quality}`
  - AC: Display available tokens for Collection File Format: `{Series Title}`, `{Edition Type}`, `{Volume}`, `{Year}`, `{Publisher}`
  - AC: Clickable tokens that insert into the format input field
  - AC: Live preview showing example output with sample data
  - AC: API endpoint: GET /api/v1/settings/naming/tokens (returns available tokens per format type)
- [x] **Separate Download and Staging folders** ✅
  - AC: Add "Download Folder" setting - where files are initially downloaded
  - AC: Add "Staging Folder" setting - where files go for import review
  - AC: Download folder can be different from staging folder
  - AC: Option to auto-move from download to staging after completion
  - AC: API endpoints: GET/PUT /api/v1/settings/folders
  - AC: Validation that both paths exist and are writable

## EPIC 7: Mylar3 Migration (BEHAVIORAL PARITY SETUP)
- [ ] Read Mylar3 SQLite DB (read-only)
- [ ] Transform to intermediate JSON snapshot
- [ ] Import into Shortboxerr DB
- [ ] Post-migration scan job
- [ ] Migration report

## EPIC 8: DDL Site Adapters & Download Hosts (Mylar3 Parity)
Implement real DDL site adapters and download host resolvers matching Mylar3's supported providers.

### 8.1 DDL Site Indexers (Comic Discovery)

#### 8.1.1 GetComics.org Adapter (Primary)
- [ ] **HTML scraping for GetComics**
  - AC: Parse search results page for release links
  - AC: Extract all download host links from release pages
  - AC: Handle pagination for search results
  - AC: Parse release details (title, size, date posted, tags)
- [ ] **GetComics search integration**
  - AC: Search by series name, issue number
  - AC: Search by keyword/tag/category
  - AC: RSS feed polling for new releases (/feed/)
  - AC: Category browsing (DC, Marvel, Image, etc.)
- [ ] **GetComics link resolution**
  - AC: Follow redirects to actual download URLs
  - AC: Handle multiple mirror options with priority
  - AC: Detect dead/expired links and skip

#### 8.1.2 32P (32 Pages) Adapter
- [ ] **32P authentication**
  - AC: Login with username/password
  - AC: Session/cookie persistence
  - AC: Handle invite-only registration status
- [ ] **32P search and browse**
  - AC: Search API integration
  - AC: Browse by category/group
  - AC: Parse torrent and DDL options
- [ ] **32P notifications/RSS**
  - AC: Personal notification feed
  - AC: New releases feed

#### 8.1.3 Additional DDL Sites
- [ ] **Libgen/Library Genesis adapter** (comics section)
  - AC: Search by title/author
  - AC: Mirror selection
- [ ] **Generic DDL adapter template**
  - AC: Base class for rapid new site implementation
  - AC: Configurable CSS/XPath selectors
  - AC: Documentation for adding new sites

### 8.2 Download Host Resolvers (File Acquisition)

#### 8.2.1 Direct/Main Server Downloads
- [ ] **Direct HTTP downloads**
  - AC: Standard HTTP GET with resume support
  - AC: Handle Content-Disposition filename
  - AC: Verify file integrity (size, magic bytes)

#### 8.2.2 MediaFire Resolver
- [ ] **MediaFire link handling**
  - AC: Parse MediaFire share page
  - AC: Extract direct download URL
  - AC: Handle "Download" button extraction
  - AC: Detect expired/removed files

#### 8.2.3 Mega.nz Resolver
- [ ] **Mega link handling**
  - AC: Parse mega.nz/#! and mega.nz/file/ URLs
  - AC: Handle Mega's encryption (MEGAcmd or API)
  - AC: Support folder links with file selection
  - AC: Rate limit awareness (free tier limits)

#### 8.2.4 Pixeldrain Resolver
- [ ] **Pixeldrain link handling**
  - AC: Extract file ID from URL
  - AC: Use Pixeldrain API for direct download
  - AC: Handle bandwidth limits

#### 8.2.5 Dropbox Resolver
- [ ] **Dropbox link handling**
  - AC: Convert share links to direct download URLs
  - AC: Handle dl=0 to dl=1 conversion
  - AC: Support folder links

#### 8.2.6 Google Drive Resolver
- [ ] **Google Drive link handling**
  - AC: Parse drive.google.com share links
  - AC: Handle virus scan warning bypass
  - AC: Extract confirmation token for large files
  - AC: Support folder links with file listing

#### 8.2.7 Legacy/Additional Hosts
- [ ] **Zippyshare resolver** (defunct, legacy support)
  - AC: Detect and skip defunct links gracefully
- [ ] **Rapidgator/Uploaded resolver** (premium)
  - AC: Support premium account credentials
  - AC: Free tier with wait times (optional)
- [ ] **1fichier resolver**
  - AC: Parse download page
  - AC: Handle wait times for free users
- [ ] **Usenet/NZB integration**
  - AC: NZB file download from DDL sites
  - AC: Pass to configured Usenet downloader (SABnzbd, NZBGet)

### 8.3 Download Host Priority & Fallback
- [ ] **Host priority configuration**
  - AC: User-configurable host preference order
  - AC: Default priority: Direct > Mega > MediaFire > Pixeldrain > GDrive > Others
- [ ] **Automatic fallback**
  - AC: Try next host on failure
  - AC: Track host reliability per DDL site
  - AC: Blacklist consistently failing hosts temporarily

### 8.4 DDL Site Health Monitoring
- [ ] **Site availability checks**
  - AC: Periodic health checks for each configured site
  - AC: Detect site changes that break scraping (CSS/HTML changes)
  - AC: Alert/disable adapter on repeated failures
  - AC: Version detection for known site layouts
- [ ] **Rate limiting per site**
  - AC: Respect site-specific rate limits
  - AC: Configurable delays between requests
  - AC: Request queuing to prevent bans
  - AC: Cloudflare challenge handling

### 8.5 DDL Adapter Tests
- [ ] **GetComics fixture tests**
  - AC: Mock HTML responses for search results
  - AC: Mock HTML responses for release pages
  - AC: Verify correct link extraction for all host types
- [ ] **Download host resolver tests**
  - AC: Mock responses for each host type
  - AC: Test redirect following and URL extraction
  - AC: Test failure scenarios (expired, removed, rate limited)
- [ ] **Integration tests**
  - AC: Cached real responses for regression testing
  - AC: End-to-end: search → parse → resolve → download

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
- 4.5 depends on EPIC 5 UI shell ✅ (can now be implemented)
- EPIC 8 depends on EPIC 4.2 (DDL Provider interfaces and services)
