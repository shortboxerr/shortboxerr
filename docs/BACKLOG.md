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
- [x] **Developer tooling** ✅
  - AC: Makefile with common targets (restore, build, test, run, install-hooks)
  - AC: Git commit-msg hook enforcing conventional commits (feat/fix/chore/test prefix)
  - AC: .gitignore for build artifacts and IDE files
- [x] **Port standardization** ✅
  - AC: Default application port: 8585 (changed from 7878)
  - AC: Updated in docker-compose, Dockerfile, CI workflow, launchSettings

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

### 4.2 DDL Provider (Mylar3-Compatible) - BUILT-IN SERVICE
The DDL (Direct Download) provider is a **built-in internal service** with Mylar3 parity.
DDL indexers are NOT user-configurable providers - they are always available like in Mylar3.
Site-specific configuration (credentials, rate limits) is done via DDL Settings, not by "adding" indexers.
**225+ unit tests** cover DDL functionality.

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
- [x] **Built-in HTTP download client** (Mylar3 parity)
  - AC: Internal service, NOT a user-configurable download client
  - AC: Always available - no need to "add" in Download Clients settings
  - AC: Used internally by DDL providers and RSS indexers for direct HTTP downloads
  - AC: Configurable via General settings only (timeout, user-agent, retries)
  - AC: Similar to how Mylar3 handles DDL downloads without external client configuration
  - AC: Supports custom headers, cookies, user-agent rotation, basic auth
  - AC: Retry logic on network failures (HttpRequestException)
  - AC: Auto-creates destination directories
  - AC: 15 unit tests covering all functionality
- [x] **Torrent client abstraction** (placeholder for future)
  - AC: Interface only, no implementation in EPIC 4
  - AC: External torrent clients (qBittorrent, etc.) WILL be user-added providers

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
- [x] **UI technology stack** ✅
  - AC: React 18 with TypeScript
  - AC: Vite for build tooling and dev server
  - AC: React Router v6 for client-side routing
  - AC: TanStack Query (React Query) for data fetching and state management
  - AC: Lucide React for icons
  - AC: CSS variables for consistent theming
  - AC: Dark theme as default aesthetic
- [x] **UI shell + navigation** ✅
  - AC: Sidebar navigation (Dashboard/Series/Collections/Wanted/Activity/History/Manual Import/Settings)
  - AC: System status indicator in sidebar
  - AC: Responsive layout
- [x] **Core pages** ✅
  - AC: Series list page (table with status indicators, bulk actions)
  - AC: Collections list page
  - AC: Activity page (thin but functional)
  - AC: Manual Import page (placeholder)
- [x] **Build integration** ✅
  - AC: Vite builds to API wwwroot folder
  - AC: API serves static files from wwwroot
  - AC: npm scripts: dev, build, preview
- [x] **API response mapping** ✅ (Bug Fix)
  - AC: API client correctly maps backend PagedResult format (records/totalRecords) to UI format (items/totalCount)
  - AC: Series and Collections pages display data correctly instead of blank pages
  - AC: Helper function toPagedResult() converts between API and UI formats

## EPIC 6: Settings Persistence & UI Enhancements
- [x] **Theme persistence** ✅
  - AC: Save selected theme (dark/light/system) to database
  - AC: Load theme preference on app start
  - AC: API endpoint: GET/PUT /api/v1/settings/ui
- [x] **General settings persistence** ✅
  - AC: Save naming patterns, root folders to database
  - AC: Settings entity with key-value storage
  - AC: Settings API endpoints for all categories
- [x] **API key management** ✅
  - AC: Display API key in Settings > General (always visible, not masked)
  - AC: "Copy" icon button to copy API key to clipboard
  - AC: "Reset" icon button to regenerate key with confirmation dialog
  - AC: API always enabled (Sonarr/Radarr behavioral parity - no enable toggle)
  - AC: Auto-generate unique API key on first application launch
  - AC: API endpoint: GET /api/v1/settings/apikey/full (returns full key), POST /api/v1/settings/apikey/regenerate
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
- [x] **UI/API development infrastructure** ✅
  - AC: CORS support enabled for local development (allows UI on different port)
  - AC: Vite proxy configuration for API requests during development
  - AC: API client uses relative URLs to work with Vite dev server proxy
  - AC: Auto-save with debounce for settings fields (500ms delay)
- [x] **Settings page structure** ✅
  - AC: Tabbed settings layout (General, Indexers, Download Clients, Import, UI, Security)
  - AC: General tab: Naming formats, Root folders, API key
  - AC: UI tab: Theme selection (dark/light/system), page size
  - AC: Security tab: Authentication method dropdown (None/Basic/Forms) - placeholder

## EPIC 7: Mylar3 Migration (BEHAVIORAL PARITY SETUP) ✅ COMPLETED
- [x] Read Mylar3 SQLite DB (read-only) ✅
- [x] Transform to intermediate JSON snapshot ✅
- [x] Import into Shortboxerr DB ✅
- [x] Post-migration scan job ✅ (via SyncMetadataAfterImport option)
- [x] Migration report ✅

### Implementation Details
- **IMylar3MigrationService**: Interface for migration operations
- **Mylar3MigrationService**: Implementation that reads Mylar3 SQLite database
- **Mylar3Snapshot**: Intermediate JSON snapshot model for review
- **Mylar3MigrationOptions**: Configurable options (dry-run, skip/update existing, etc.)

### API Endpoints
- `POST /api/v1/mylar3/migration/analyze` - Analyze Mylar3 database and return snapshot
- `POST /api/v1/mylar3/migration/export` - Export snapshot to JSON file
- `POST /api/v1/mylar3/migration/import` - Import from snapshot into Shortboxerr
- `POST /api/v1/mylar3/migration/migrate` - Full migration (analyze + import)

### Features
- Reads comics table (series with ComicVine IDs, publisher, year)
- Reads issues table (issue number, status, location)
- Maps Mylar3 status (Wanted/Downloaded/Skipped) to Shortboxerr
- Supports dry-run mode for previewing changes
- Optional metadata sync from ComicVine after import
- 10 unit tests covering all scenarios

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

#### 8.1.2 ReadComicOnline Adapter (Secondary)
- [ ] **Determine homepage address**
  - AC: Parse index page for "Go to Homepage" button and update base URL as needed
- [ ] **HTML scraping for ReadComicOnline**
  - AC: Parse search results page for release links
  - AC: Extract all download host links from release pages
  - AC: Handle pagination for search results
  - AC: Parse release details (title, size, date posted, tags)
- [ ] **ReadComicOnline search integration**
  - AC: Search by series name, issue number
  - AC: Search by keyword/tag/category
  - AC: RSS feed polling for new releases (/feed/)
  - AC: Category browsing (DC, Marvel, Image, etc.)
- [ ] **ReadComicOnline link resolution**
  - AC: Follow redirects to actual download URLs
  - AC: Handle multiple mirror options with priority
  - AC: Detect dead/expired links and skip

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

## EPIC 9: ComicVine Integration (Mylar3 Parity)
ComicVine is the primary metadata source for comic series, issues, and collections. Must achieve behavioral parity with Mylar3's ComicVine integration.

### 9.1 ComicVine API Client ✅ COMPLETED
- [x] **API authentication & configuration**
  - AC: Store ComicVine API key securely in settings
  - AC: API key validation endpoint
  - AC: Settings UI for entering/updating API key
  - AC: API endpoint: GET/PUT /api/v1/comicvine/settings
  - AC: API endpoint: GET /api/v1/comicvine/settings/apikey (returns full unmasked key)
  - AC: API endpoint: POST /api/v1/comicvine/test (test connection with saved key)
- [x] **Rate limiting (match Mylar3)**
  - AC: Respect ComicVine's rate limits (200 requests/hour or as documented)
  - AC: Request queuing with backoff
  - AC: Track request count and reset time
  - AC: Graceful handling of 420 (rate limit) responses
  - AC: API endpoint: GET /api/v1/comicvine/ratelimit (returns current usage status)
- [x] **API client implementation**
  - AC: IComicVineClient interface
  - AC: SearchVolumes, SearchIssues, GetVolume, GetIssue, GetPublisher, GetVolumeIssues
  - AC: Response caching (configurable TTL via IMemoryCache)
  - AC: Rate limit exception handling
  - AC: API endpoints for search: GET /api/v1/comicvine/search/volumes, /search/issues
  - AC: API endpoints for entities: GET /api/v1/comicvine/volumes/{id}, /issues/{id}, /publishers/{id}
  - AC: API endpoint: GET /api/v1/comicvine/volumes/{id}/issues (all issues for a volume)
- [x] **Settings UI**
  - AC: ComicVine tab in Settings page
  - AC: API key input field with description "Specify your own ComicVine API key here"
  - AC: No "Enable ComicVine" checkbox - presence of API key implies enabled
  - AC: Link to get API key from comicvine.gamespot.com/api (always shown below input field)
  - AC: Test Connection button - visible when API key is entered or saved
  - AC: Test Connection saves unsaved key before testing (save + test in one action)
  - AC: Rate limit status display (requests used/remaining) - only shown when API key is set
  - AC: Cache duration, auto-match threshold, auto-refresh settings - only shown when API key is set
  - AC: User-friendly message when attempting actions that require ComicVine without API key
  - AC: API key persistence - key must persist across page refresh and app restart
  - AC: Display "Current key: {masked}" when API key is saved (e.g., "e218...201e")
  - AC: API key input shows as plain text (not masked while entering)
  - AC: Eye button to reveal/hide saved API key (fetches full key from backend)
  - AC: Copy button appears when full key is revealed
- [x] **Error handling**
  - AC: Invalid API key returns clear error message "Invalid ComicVine API key"
  - AC: HTML error responses detected and handled gracefully
  - AC: Rate limit exceeded returns user-friendly message
  - AC: Network errors logged and returned with context
  - AC: Base URL must have trailing slash for proper HttpClient path concatenation
- [x] **Tests**
  - AC: 12 unit tests for ComicVineClient
  - AC: Mock HttpMessageHandler for all API calls

### 9.2 Series Metadata ✅ COMPLETED
- [x] **Series search**
  - AC: Search ComicVine by series name
  - AC: Filter by publisher, year range
  - AC: Return top N matches with confidence scores
  - AC: Handle series with same name from different publishers/years
  - AC: API endpoint: GET /api/v1/series/comicvine/search
- [x] **Series matching**
  - AC: Auto-match local series to ComicVine on add
  - AC: Manual search and match (API only, UI in 9.9)
  - AC: Store ComicVine ID (volume ID) in Series entity
  - AC: Unmatch/rematch functionality
  - AC: API endpoints: POST /api/v1/series/{id}/match/{volumeId}, /automatch, /unmatch
  - AC: Bulk auto-match: POST /api/v1/series/match-all
- [x] **Add series by ComicVine ID**
  - AC: Direct entry of ComicVine volume ID to add a series
  - AC: Validate ID exists via ComicVine API before adding
  - AC: Fetch and populate all metadata immediately on add
  - AC: Create wanted list for all issues in series
  - AC: API endpoint: POST /api/v1/series/comicvine/{volumeId}
  - AC: Preview endpoint: GET /api/v1/series/comicvine/{volumeId}
- [x] **Series metadata sync**
  - AC: Fetch: title, sort title, publisher, start year, status (continuing/ended)
  - AC: Fetch: description, issue count, first/last issue dates
  - AC: Fetch: aliases (alternate titles for matching)
  - AC: Store ComicVine metadata on Series entity
  - AC: API endpoint: POST /api/v1/series/{id}/refresh
  - AC: API endpoint: POST /api/v1/series/{id}/sync-issues
- [x] **Entity enhancements**
  - AC: Series entity: ComicVineId, Aliases, ComicVinePublisherId, ComicVineUrl, CoverImageUrl, TotalIssueCount, MetadataLastRefreshed, ComicVineLastUpdated
  - AC: Issue entity: ComicVineId, IssueNumberText, StoreDate, CoverDate, ComicVineUrl, CoverImageUrl, MetadataLastRefreshed
  - AC: EF Core migration: AddComicVineMetadataFields
- [x] **Tests**
  - AC: 14 unit tests for SeriesMetadataService
  - AC: Mock IComicVineClient for all API calls
  - AC: Test confidence scoring algorithm

### 9.3 Issue Metadata ✅ COMPLETED
- [x] **Issue list sync**
  - AC: Fetch all issues for a matched series
  - AC: Create Issue entities for missing issues (wanted list)
  - AC: Store ComicVine issue ID
  - AC: Handle issue number formats (decimals, specials, annuals)
- [x] **Issue detail sync**
  - AC: Fetch: issue number, title, release date, description
  - AC: Fetch: cover date vs. store date (match Mylar3 behavior)
  - AC: Fetch: story arc associations
  - AC: Fetch: character/team appearances (optional, configurable) - DEFERRED
- [x] **Special issues handling (Mylar3 parity)**
  - AC: Annuals linked to parent series
  - AC: One-shots handling
  - AC: Issue #0, negative issues, decimal issues (1.5, etc.)
  - AC: Variant cover detection (optional) - DEFERRED
- [x] **Entity enhancements**
  - AC: IssueStoryArc entity for story arc associations
  - AC: Issue entity: IsAnnual, IsSpecial, SpecialType fields
  - AC: EF Core migration: AddIssueMetadataFields
- [x] **API endpoints**
  - AC: GET /api/v1/issues/comicvine/{id} - preview issue from ComicVine
  - AC: POST /api/v1/issues/{id}/refresh - refresh issue metadata
  - AC: POST /api/v1/issues/{id}/story-arcs/sync - sync story arcs
  - AC: POST /api/v1/series/{id}/issues/refresh - bulk refresh all issues
  - AC: POST /api/v1/series/{id}/issues/detect-specials - detect annuals/specials
- [x] **Tests**
  - AC: 16 unit tests for IssueMetadataService
  - AC: Tests for special issue detection (annuals, one-shots, etc.)
  - AC: Tests for story arc sync

### 9.4 Cover Art ✅ COMPLETED
- [x] **Cover image fetching**
  - AC: Download series cover (primary volume image)
  - AC: Download issue covers (individual issue images)
  - AC: Multiple image sizes (thumb, small, medium, large) - match Mylar3
  - AC: Store in configurable cache directory
- [x] **Cover caching**
  - AC: Check cache before fetching
  - AC: Configurable cache retention (default: indefinite)
  - AC: Cache invalidation on metadata refresh
  - AC: Cache statistics (total covers, size, dates)
- [x] **Cover fallbacks**
  - AC: Use series cover if issue cover missing
  - AC: Placeholder image for missing covers
  - AC: Cover priority: issue > series > placeholder
- [x] **API endpoints**
  - AC: GET /api/v1/covers/series/{id} - get series cover image
  - AC: GET /api/v1/covers/issues/{id} - get issue cover image
  - AC: DELETE /api/v1/covers/series/{id} - clear series cache
  - AC: DELETE /api/v1/covers/issues/{id} - clear issue cache
  - AC: GET /api/v1/covers/cache/stats - cache statistics
  - AC: DELETE /api/v1/covers/cache - clear all cache
  - AC: POST /api/v1/covers/{type}/{id}/refresh - refresh cover
- [x] **Tests**
  - AC: 17 unit tests for CoverService
  - AC: Tests for caching, fallback, download, statistics

### 9.5 Collection/TPB Metadata
- [x] **Volume/TPB search** ✅
  - AC: Search ComicVine for collected editions ✅
  - AC: Match TPB/HC/Omnibus to ComicVine volume entries ✅
  - AC: Handle editions that span multiple series ✅
  - AC: Detect edition type from title (Omnibus, Absolute, HC, TPB) ✅
- [x] **Collection content mapping** ✅
  - AC: Fetch issues contained in collection ✅
  - AC: Map to EditionContent entities ✅
  - AC: Handle issue ranges (e.g., "collects #1-6") ✅
- [x] **Collection cover art** ✅
  - AC: Fetch collection/TPB covers ✅
  - AC: Same caching rules as issue covers ✅ (via existing CoverService)

### 9.6 Auto-Matching & Import Integration
- [x] **Import auto-match** ✅
  - AC: On file import, search ComicVine for series match ✅
  - AC: Confidence threshold for auto-accept (configurable, default 85%) ✅
  - AC: Queue low-confidence matches for manual review ✅
  - AC: Use parsed filename (series, issue, year) for search ✅
- [x] **Bulk matching** ✅
  - AC: "Match All Unmatched" action for series list ✅
  - AC: Progress indicator for bulk operations ✅
  - AC: Summary report of matches/failures ✅
- [x] **Match conflict resolution** ✅
  - AC: PendingMatch entity for storing matches requiring review ✅
  - AC: Store top N candidates with confidence scores ✅
  - AC: Accept/Reject endpoints for pending matches ✅
  - Note: UI deferred to future iteration

### 9.7 Metadata Refresh
- [x] **Scheduled refresh** ✅
  - AC: Configurable refresh interval (default: weekly) ✅
  - AC: Refresh series metadata ✅
  - AC: Refresh issue list (discover new issues) ✅
  - AC: Refresh covers (if changed) ✅
  - AC: Background service with allowed hours ✅
- [x] **Manual refresh** ✅
  - AC: API endpoints for manual refresh ✅
  - AC: "Refresh All" API endpoint ✅
  - AC: Force refresh option (ignore cache) ✅
  - Note: UI buttons deferred to future iteration
- [x] **Refresh history** ✅
  - AC: Log metadata refresh events ✅
  - AC: Track last refresh time per series ✅
  - AC: API endpoint: GET /api/v1/metadata/series/{id}/history ✅

### 9.8 Mylar3 ComicVine Settings Import
- [x] **Import ComicVine config from Mylar3** ✅
  - AC: Parse config.ini for ComicVine API key ✅
  - AC: Import cover cache settings ✅
  - AC: Import refresh interval settings ✅
  - AC: Import auto-match thresholds ✅
  - AC: Track unmapped settings ✅
- [x] **ComicVine ID migration** ✅
  - AC: Map Mylar3 ComicVine IDs to Shortboxerr series ✅
  - AC: Preserve existing metadata matches ✅
  - AC: Validate migrated IDs are still valid ✅
  - AC: Optional metadata sync after migration ✅

### 9.9 ComicVine UI
- [x] **Settings page** ✅ (Completed in EPIC 9.1)
  - AC: API key input (masked, with show/hide toggle)
  - AC: Test connection button
  - AC: Rate limit status display
  - AC: Cache management (clear cache button)
- [x] **Series detail integration** ✅
  - AC: Series detail page with cover, metadata, overview
  - AC: ComicVine link on matched series
  - AC: Issues grid with status indicators (owned/wanted/edition)
  - AC: Clickable series rows navigate to detail page
  - AC: API endpoint: GET /api/v1/series/{id}/issues
  - AC: "Match to ComicVine" button on unmatched series - DEFERRED
  - AC: "Refresh Metadata" button - DEFERRED
- [x] **Search & match modal** ✅
  - AC: Search ComicVine by name
  - AC: Display results with covers and metadata preview
  - AC: Select and confirm to add series
  - AC: Shows API key warning if not configured
  - AC: Handles existing series conflict
- [x] **Issue display enhancements** ✅
  - AC: Toggle between Cover View and List View ✅
  - AC: Cover View: grid of issue covers with status indicator overlay ✅
  - AC: List View: table with issue number, title, release date, status, actions ✅
  - AC: Issue cards/rows show special issue badges (Annual, One-Shot, etc.) ✅
  - AC: Issue cards/rows show story arc tags (if any) ✅
  - AC: Sorting options (issue number, release date, status, title) ✅
  - AC: Filtering by status (owned, wanted, missing, all, skipped) ✅
  - AC: Bulk selection support (mark as owned, mark as wanted, skip - UI present, handlers deferred) ✅
  - AC: Persist view preference (cover/list) in user settings ✅
- [x] **Collection/Edition detail page** ✅
  - AC: Collection detail page showing metadata (title, type, ISBN, publisher) ✅
  - AC: List of contained issues with their series ✅
  - AC: Cover image for collection ✅
  - AC: Status indicator (have file / missing) ✅
  - AC: Link to series for each contained issue ✅
  - AC: API endpoint: GET /api/v1/editions/{id}/detail ✅
  - AC: API endpoint: GET /api/v1/editions/{id}/contents ✅

### 9.10 ComicVine Conformance Tests
- [x] **API client tests** ✅
  - AC: Mock ComicVine responses ✅
  - AC: Test rate limiting behavior ✅
  - AC: Test error handling (404, 420, 500) ✅
- [x] **Matching algorithm tests** ✅
  - AC: Golden test fixtures for series matching ✅
  - AC: Test edge cases (same name, different years) ✅
  - AC: Test confidence scoring ✅
- [x] **Integration tests** ✅
  - AC: Full flow: search → match → sync metadata ✅
  - AC: Cover download and caching ✅
  - AC: Refresh cycle ✅
  - AC: Error handling for API failures ✅
  - AC: Partial failure handling in bulk operations ✅

---

## EPIC 10: NZB/Usenet Support (Mylar3/Sonarr/Radarr Parity)
Usenet (NZB) support for comic acquisition. Must achieve behavioral parity with Mylar3, Sonarr, and Radarr's Usenet integration.

### 10.1 NZB Indexer Integration
- [ ] **Newznab API client**
  - AC: Standard Newznab API implementation (used by most NZB indexers)
  - AC: API key authentication
  - AC: Search by series name, issue number, year
  - AC: Category filtering (comics category IDs)
  - AC: Parse NZB search results into candidates
- [ ] **NZBHydra2 support**
  - AC: Aggregate searches across multiple indexers
  - AC: Single API endpoint for multiple backends
  - AC: Respect indexer priorities from NZBHydra
- [ ] **Built-in indexer presets**
  - AC: Pre-configured settings for popular NZB indexers
  - AC: NZBgeek, DrunkenSlug, NZBFinder, etc.
  - AC: Easy setup with just API key
- [ ] **Indexer health monitoring**
  - AC: Track indexer response times
  - AC: Detect and handle rate limiting
  - AC: Automatic failover to backup indexers

### 10.2 NZB Download Client Integration
- [ ] **SABnzbd integration**
  - AC: Add NZB to SABnzbd via API
  - AC: Category assignment for comics
  - AC: Priority configuration
  - AC: Monitor download progress
  - AC: Detect completion and trigger import
- [ ] **NZBGet integration**
  - AC: Add NZB to NZBGet via API
  - AC: Category and priority support
  - AC: Progress monitoring
  - AC: Post-processing script integration
- [ ] **Download client health checks**
  - AC: Verify connectivity on startup
  - AC: Monitor disk space warnings
  - AC: Handle client unavailability gracefully

### 10.3 NZB Candidate Processing
- [ ] **NZB release parsing**
  - AC: Parse NZB release names (similar to DDL parser)
  - AC: Extract series, issue, year, quality, format
  - AC: Handle Usenet naming conventions
- [ ] **NZB candidate model**
  - AC: Store indexer source, NZB URL, size, age
  - AC: Quality scoring aligned with DecisionEngine
  - AC: Integrate with existing Candidate model
- [ ] **NZB filtering rules**
  - AC: Minimum/maximum age limits
  - AC: Size limits (same as DDL)
  - AC: Banned/required words (same as DDL)
  - AC: Prefer certain indexers

### 10.4 NZB → Import Handoff
- [ ] **Post-download detection**
  - AC: Monitor SABnzbd/NZBGet for completed downloads
  - AC: Detect completed comic files in download directory
  - AC: Handle unpacking (RAR, ZIP) automatically
- [ ] **Import integration**
  - AC: Move completed files to staging
  - AC: Auto-match to series/issue
  - AC: Create HistoryEvent linking NZB → import
  - AC: Handle failed downloads (incomplete, password-protected)

### 10.5 NZB Configuration & Settings
- [ ] **Indexer configuration**
  - AC: Add/edit/delete NZB indexers
  - AC: Test indexer connectivity
  - AC: Priority ordering for multiple indexers
  - AC: Enable/disable per indexer
- [ ] **Download client configuration**
  - AC: SABnzbd: URL, API key, category, priority
  - AC: NZBGet: URL, username, password, category
  - AC: Test connection button
  - AC: Default download client selection
- [ ] **Mylar3 NZB settings import**
  - AC: Parse Mylar3 config.ini for NZB settings
  - AC: Import indexer configurations
  - AC: Import SABnzbd/NZBGet settings
  - AC: Validation report

### 10.6 NZB UI
- [ ] **Indexers settings page**
  - AC: NZB Indexers section (separate from DDL)
  - AC: Add indexer modal with Newznab fields
  - AC: Preset selection for popular indexers
  - AC: Test and status indicators
- [ ] **Download clients settings page**
  - AC: SABnzbd configuration panel
  - AC: NZBGet configuration panel
  - AC: Connection test results
- [ ] **Activity integration**
  - AC: Show NZB downloads in activity feed
  - AC: Download progress from SABnzbd/NZBGet
  - AC: Queue management (pause, remove, priority)

### 10.7 NZB Conformance Tests
- [ ] **Newznab API tests**
  - AC: Mock indexer responses
  - AC: Test search parameter encoding
  - AC: Test result parsing
- [ ] **Download client tests**
  - AC: Mock SABnzbd API responses
  - AC: Mock NZBGet API responses
  - AC: Test add/status/remove operations
- [ ] **Integration tests**
  - AC: Full flow: search → download → import
  - AC: Multi-indexer aggregation
  - AC: Download client failover

---

## EPIC 11: Weekly Pull List (Mylar3 Parity)
Track upcoming comic releases and automate wanted list management. Must achieve full behavioral parity with Mylar3's weekly pull list functionality.

### 11.1 Release Date Tracking ✅ COMPLETED
- [x] **ComicVine release date sync** ✅
  - AC: Fetch store date (release date) for all issues in monitored series ✅
  - AC: Differentiate between cover date and store date (match Mylar3) ✅
  - AC: Handle TBD/unknown release dates gracefully ✅
  - AC: Track release date changes (delays, moved up) - via metadata refresh
- [x] **Release date caching** ✅
  - AC: Release dates stored locally in Issue entity ✅
  - AC: Configurable refresh interval (via metadata refresh settings) ✅
  - AC: Track last sync time per series ✅
  - AC: Force refresh option ✅
- [x] **Release calendar data model** ✅
  - AC: IssueStatus enum (Wanted, Owned, Skipped, etc.) ✅
  - AC: Link to Series and Issue entities ✅
  - AC: API endpoint: GET /api/v1/pulllist/calendar ✅

### 11.2 Weekly Pull List Generation ✅ COMPLETED
- [x] **This week's releases** ✅
  - AC: List all issues releasing this week for monitored series ✅
  - AC: Week start on Sunday (US standard for comics) ✅
  - AC: Comic release day awareness (Wednesday in US) ✅
  - AC: API endpoint: GET /api/v1/pulllist/week ✅
- [x] **Upcoming releases** ✅
  - AC: List releases for next N weeks (configurable, default: 4) ✅
  - AC: Filter by publisher, series status ✅
  - AC: API endpoint: GET /api/v1/pulllist/upcoming ✅
- [x] **Past releases** ✅
  - AC: List recent releases (last N weeks) ✅
  - AC: Show owned vs. missing status ✅
  - AC: API endpoint: GET /api/v1/pulllist/past ✅

### 11.3 Wanted List Automation ✅ COMPLETED
- [x] **Issue status management** ✅
  - AC: Mark issues as Wanted/Owned/Skipped ✅
  - AC: Bulk status updates ✅
  - AC: Per-series monitoring settings ✅
- [x] **Series monitoring modes (Mylar3 parity)** ✅
  - AC: "All Issues" - want all issues in series ✅
  - AC: "Future Issues" - only want new issues going forward ✅
  - AC: "Manual" - never auto-add, user selects individually ✅
  - AC: "First Issue" - only want #1 issues (for new series discovery) ✅
  - AC: "None" - don't monitor this series at all ✅
- [x] **Auto-add to wanted list** ✅
  - AC: Automatically add new issues to wanted list on release day ✅
  - AC: Configurable: on release day, X days before, or manual only ✅
  - AC: `ReleaseDayBackgroundService` calls `ProcessReleaseDayAsync` on release days ✅
  - AC: Configurable schedule via `ReleaseDayProcessingHours` (default: 6am, 12pm) ✅
  - AC: Tracks last processed date to avoid duplicate processing ✅
  - AC: API endpoints for manual trigger and status check ✅
    - POST /api/v1/pulllist/releaseday/process
    - GET /api/v1/pulllist/releaseday/status
  - Note: ComicVine discovery refresh already implemented in `ComicVineRefreshBackgroundService` ✅
- [ ] **Auto-search on release** (deferred)
  - AC: Trigger search when issue is added to wanted list
  - AC: Respect rate limits and search intervals
  - Note: Requires DDL/NZB integration from EPICs 8/10

### 11.4 Pull List Notifications (PARTIAL)
In-app notification system implemented. External notification channels (email, webhooks) deferred.

- [x] **In-app notifications (notification center)** ✅
  - AC: Notification entity with types (Info, Success, Warning, Error, NewRelease, Grabbed, WeeklySummary, Health, Update) ✅
  - AC: Create/read/delete notifications ✅
  - AC: Mark as read (single/all) ✅
  - AC: Filter by type, read status, series ✅
  - AC: Unread count endpoint ✅
  - AC: Auto-delete old notifications (configurable) ✅
  - AC: Max notifications limit (configurable) ✅

- [x] **New release notifications** ✅
  - AC: `SendNewReleaseNotificationAsync` method ✅
  - AC: Summary notification (aggregated) vs. individual (configurable) ✅
  - AC: Configurable notification day ✅
  - AC: Links to pull list page ✅

- [x] **Grabbed notifications** ✅
  - AC: `SendGrabbedNotificationAsync` method ✅
  - AC: Links to series page ✅
  - AC: Shows download source ✅

- [x] **Notification settings** ✅
  - AC: Enable/disable in-app notifications ✅
  - AC: Enable/disable per notification type (NewRelease, Grabbed, WeeklySummary) ✅
  - AC: Aggregate release notifications toggle ✅
  - AC: Auto-delete read after N days ✅
  - AC: Max notifications limit ✅

- [x] **API endpoints** ✅
  - GET /api/v1/notifications ✅
  - GET /api/v1/notifications/unread/count ✅
  - GET /api/v1/notifications/{id} ✅
  - POST /api/v1/notifications/{id}/read ✅
  - POST /api/v1/notifications/read-all ✅
  - DELETE /api/v1/notifications/{id} ✅
  - DELETE /api/v1/notifications/read ✅
  - GET/PUT /api/v1/notifications/settings ✅

- [x] **Unit tests** ✅ (20 tests)

- [ ] **External notification channels** (deferred)
  - AC: Email notifications (SMTP configuration)
  - AC: Webhook notifications (for Discord, Slack, etc.)
  - AC: Pushover/Pushbullet support
  - Note: Follows Sonarr/Radarr notification provider pattern

### 11.9 Pull List UX Improvements ✅ COMPLETED
- [x] **Empty state improvements** ✅
  - AC: "My Pull List" empty state shows:
    - Check if API key configured → "Configure ComicVine API" button ✅
    - Check if any series exist → "Add your first series" button ✅
    - If series exist but unmatched → "Match series to ComicVine" guidance ✅
    - "Try All Releases mode to discover new comics" suggestion ✅
  - AC: "All Releases" empty state shows:
    - Check if API key configured → "Configure ComicVine API" button ✅
    - "No releases found" with date confirmation ✅
    - Check for API errors and display friendly message ✅
    
- [x] **Manual refresh controls** ✅
  - AC: "Refresh from ComicVine" button in Pull List toolbar ✅
  - AC: Shows last refresh timestamp ✅
  - AC: Triggers immediate metadata sync for monitored series ✅ (refetches data)
  - AC: Shows progress indicator during refresh ✅ (spinning icon)
  
- [x] **Configuration status indicator** ✅
  - AC: Visual indicator if ComicVine not configured ✅
  - AC: Warning banner at top of Pull List if API key missing ✅
  - AC: Quick link to Settings → ComicVine page ✅
  
- [ ] **First-time user experience** (deferred)
  - AC: Guided onboarding when Pull List first visited with no data
  - AC: Step-by-step: 1) Configure API key, 2) Add series, 3) View releases
  - AC: "Skip" option to dismiss onboarding
  - Note: Empty states with actionable buttons provide sufficient guidance; full wizard deferred

### 11.5 Pull List UI ✅ COMPLETED
- [x] **List view**
  - AC: This week's releases prominently displayed ✅
  - AC: Upcoming releases list (next 4 weeks) ✅
  - AC: Past releases with status ✅
  - AC: Filter by series, publisher, owned/missing ✅ (status filter implemented)
  - AC: Sortable columns (series, issue, publisher, release date, status) ✅
  - AC: Default sort by series title, then issue number ✅
- [x] **Pull list management**
  - AC: Mark issue as "Skip" (don't want this issue) ✅
  - AC: Mark issue as "Owned" (have it already, outside system) ✅
  - AC: "Add to Wanted" button for manual additions ✅
  - AC: Bulk actions (select multiple, mark as skipped/wanted) ✅
- [x] **Dashboard integration**
  - AC: "This Week" widget on dashboard ✅
  - AC: "Coming Soon" widget ✅
  - AC: Release count badges ✅
- [x] **Navigation improvements** ✅
  - AC: Consolidated week navigation (< / dropdown / >) ✅
  - AC: Dropdown combines: This Week, +/-N Weeks, Upcoming (4 weeks), Past (4 weeks) ✅
  - AC: Arrows always navigate by week (switches to week view if in Upcoming/Past) ✅
  - AC: Display shows Release Day date (Wednesday) not week range ✅
- [x] **Caching & data freshness** ✅
  - AC: React Query staleTime 30 minutes (matches backend cache) ✅
  - AC: Uses isFetching (not just isLoading) to show spinner during refetch ✅
  - AC: Prevents showing stale data when navigating between weeks ✅
  - AC: Manual refresh button forces fresh fetch ✅
  - AC: Cache-Control: no-cache header on API requests ✅

### 11.8 This Week Discovery (Mylar3 Parity) ✅ COMPLETED
- [x] **All releases view (not just monitored series)** ✅
  - AC: Fetch all ComicVine releases for the week (not limited to monitored series) ✅
  - AC: Show issues from unmonitored series alongside monitored ones ✅
  - AC: Visual distinction between "in library" vs "discoverable" issues ✅
  - AC: Toggle to show "All Releases" vs "My Pull List Only" ✅
  - AC: Cover view and list view options (same as existing pull list) ✅
- [x] **Add issue one-off** ✅
  - AC: "Add Issue" button to add a single issue as wanted without adding the series ✅
  - AC: Creates issue in database with status=Wanted, series with monitored=false ✅
  - AC: Issue appears in Wanted list for search/download ✅
  - AC: API endpoint: POST /api/v1/pulllist/discover/add-issue ✅
- [x] **Add series from discovery** ✅
  - AC: "Add Series" button to add full series and start monitoring ✅
  - AC: Uses existing AddSeriesByComicVineIdAsync functionality ✅
  - AC: Option to set monitoring mode when adding (All/Future/Manual/FirstIssue) ✅
  - AC: After adding, issue status updates to reflect monitoring mode ✅
- [x] **ComicVine weekly releases integration** ✅
  - AC: Fetch this week's releases from ComicVine API ✅
  - AC: Cache results to minimize API calls (30-minute TTL) ✅
  - AC: Handle pagination for large release weeks ✅
  - AC: Filter by publisher (in-library only for now, full publisher filter deferred) ✅
  - **Data Source Parity with Mylar3**: Uses same ComicVine `issues/` endpoint with
    `store_date` filter (format: `YYYY-MM-DD|YYYY-MM-DD`). Mylar3 refreshes every 4 hours
    via background scheduler; Shortboxerr uses 30-min cache with on-demand refresh.
    Full parity requires `ReleaseDayBackgroundService` (see 11.3).
- [x] **UI enhancements** ✅
  - AC: Discovery filter (All/New to Me/In Library) ✅
  - AC: "NEW" badge for series not in library ✅
  - AC: Quick-add buttons in cover view and list view ✅
  - AC: Confirmation modal for adding series with monitoring mode selection ✅
  - Note: Publisher filter dropdown deferred - requires additional API work to fetch publishers from ComicVine releases

### 11.6 Pull List Configuration ✅ COMPLETED
- [x] **Settings**
  - AC: Week start day (Sunday/Monday) ✅
  - AC: Default add-to-wanted behavior ✅
  - AC: Search delay after release ✅
  - AC: Notification preferences (deferred to 11.4)
  - AC: API endpoint: GET/PUT /api/v1/pulllist/settings ✅
- [x] **Per-series settings**
  - AC: Override monitoring mode per series ✅
  - AC: Skip variants per series ✅
  - AC: Priority per series (for search ordering) ✅
- [x] **Mylar3 settings import** ✅
  - AC: Parse config.ini for pull list settings ✅
    - Weekly export folder, format, enabled
    - Default monitoring mode
    - Auto-add settings, annuals, specials, variants
    - Search delay, week start day
  - AC: Import series monitoring modes ✅
    - Maps Mylar3 monitor modes (all, future, manual, none, first) to Shortboxerr
    - Applied during database migration
    - Option to import monitoring modes during migration
  - AC: API endpoints ✅
    - POST /api/v1/mylar3/pulllist/parse
    - POST /api/v1/mylar3/pulllist/parse-file
    - POST /api/v1/mylar3/pulllist/import
    - POST /api/v1/mylar3/pulllist/import-from-file
  - AC: Unit tests (7 tests) ✅
  - Note: Notification preferences deferred - external notifications not yet implemented

### 11.10 Weekly Pull List Export (Mylar3 Parity) ✅ COMPLETED
Mylar3 offers an option to save weekly release data to a file in a designated directory.
This creates a persistent record of each week's releases for reference or integration with other tools.

- [x] **Weekly pull list file export** ✅
  - AC: New setting: "Export Weekly Pull List" (boolean, default: false) ✅
  - AC: New setting: "Weekly Export Directory" (path under comics root) ✅
  - AC: When enabled, writes release data to `{export_dir}/{YYYY}-{WW}/releases.json` ✅
  - AC: Directory format: `{YEAR}-{WEEK_OF_YEAR}` (e.g., `2026-06` for week 6 of 2026) ✅
  - AC: File contains: release date, series, issues, status, publishers ✅
  - AC: API endpoint: GET/PUT /api/v1/pulllist/settings (add export settings) ✅
  
- [x] **Export file format options** ✅
  - AC: JSON format (default) - structured data for programmatic access ✅
  - AC: Plain text format (human-readable list) ✅
  - AC: CSV format for spreadsheet import ✅
  - AC: New setting: "Weekly Export Format" (Json/Text/Csv) ✅
  
- [x] **Export triggers** ✅
  - AC: Auto-export on release day (when pull list is processed) - setting added ✅
  - AC: Manual export via API endpoint: POST /api/v1/pulllist/export/{date} ✅
  - AC: Export current week: POST /api/v1/pulllist/export ✅
  - AC: Export history: GET /api/v1/pulllist/export/history ✅
  
- [x] **Export file contents** ✅
  - AC: Week metadata: year, week number, release day date ✅
  - AC: For each issue: series title, issue number, publisher, status, ComicVine ID ✅
  - AC: Summary: total count, wanted count, owned count, by publisher, by status ✅
  - AC: Timestamp of export ✅
  
- [x] **Settings UI** ✅
  - AC: Weekly Export section in Pull List settings tab ✅
  - AC: Enable/disable toggle ✅
  - AC: Export directory input ✅
  - AC: Export format selector ✅
  - AC: Auto-export toggle ✅
  - AC: Manual export button with status feedback ✅

### 11.11 ComicVine Sync Parity (Mylar3) ✅ COMPLETED
Implement background refresh service to match Mylar3's ComicVine synchronization behavior.

**Implementation:**
- [x] **Research: Mylar3 ComicVine refresh interval** ✅
  - Web search inconclusive (specific config settings not documented publicly)
  - Based on community knowledge: ~4-hour refresh interval for weekly releases
  - Implemented with conservative 4-hour default to match Mylar3

- [x] **Background refresh service** ✅
  - `ComicVineRefreshBackgroundService` implemented
  - Configurable refresh interval (default: 4 hours - Mylar3 parity)
  - Configurable allowed hours (optional time window restriction)
  - Track last refresh time in settings for persistence
  - Skip refresh if within minimum interval
  - Pre-fetches current week + 3 weeks ahead by default

- [x] **API endpoints** ✅
  - POST `/api/v1/pulllist/discovery/refresh` - trigger manual refresh
  - GET `/api/v1/pulllist/discovery/status` - get refresh status

- [x] **Settings added to ComicVineSettings** ✅
  - `DiscoveryRefreshEnabled` (default: true)
  - `DiscoveryRefreshIntervalHours` (default: 4)
  - `DiscoveryRefreshAllowedHours` (empty = all hours)
  - `DiscoveryRefreshWeeksAhead` (default: 4)

- [x] **Unit tests** ✅ (7 tests)
  - Test disabled state
  - Test API not configured
  - Test multiple weeks refresh
  - Test allowed hours filtering
  - Test default settings
  - Test partial failure handling

### 11.7 Pull List Conformance Tests ✅ COMPLETED
- [x] **Calendar generation tests**
  - AC: Test week boundary calculations ✅ (5 tests)
  - AC: Test release date grouping ✅ (4 tests)
  - AC: Test status calculation (owned/wanted/missing) ✅ (5 tests)
- [ ] **Automation tests** (deferred - depends on EPIC 4)
  - AC: Test auto-add to wanted list timing
  - AC: Test auto-search trigger
  - AC: Test notification generation
- [x] **Integration tests** (partial)
  - AC: Full flow: ComicVine sync → calendar update → auto-add → search → grab (deferred - search depends on EPIC 4)
  - AC: Multi-series weekly pull list generation ✅ (2 tests)
  - AC: UI calendar interaction (manual testing complete)

---

## EPIC 12: Performance & Caching Strategy

### Overview
Implement comprehensive caching to minimize database queries and external API calls, improving responsiveness and reducing load on ComicVine API.

### Current State Analysis
**Existing Caching:**
- ✅ ComicVine API responses (IMemoryCache, 30min-7days TTL)
- ✅ Cover images (disk-based, permanent until manually cleared)
- ✅ Discovery results (IMemoryCache, 30min TTL - backend)
- ✅ Pull list/Discovery UI (React Query, 30min staleTime - frontend)
- ✅ Cache-Control: no-cache header on API requests (prevents browser HTTP caching)
- ❌ Pull list queries (no server-side caching - queries DB on every request)
- ❌ Series/Issue lists (no caching)
- ❌ Dashboard stats/aggregates (no caching)

**Frontend Caching Strategy (React Query):**
- staleTime: 30 minutes for pull list and discovery queries
- Uses `isFetching` check to show loading spinner during refetch
- Prevents showing stale cached data when navigating between weeks
- Manual refresh button forces fresh fetch
- Rationale: ComicVine release data is set weeks in advance and rarely changes;
  30-minute client cache matches backend cache duration

### 12.1 Data Caching Strategy ✅ PARTIAL
- [x] **Pull list query caching** ✅
  - AC: Cache weekly pull list results with 5-minute TTL ✅ (discovery caching migrated to ICacheService)
  - AC: Cache upcoming/past releases with 10-minute TTL ⏸️ (can be added incrementally)
  - AC: Invalidate on issue status change, series monitoring change ✅
  - AC: Use sliding expiration for frequently accessed data ⏸️ (future enhancement)
  - AC: Cache key includes filter parameters (status, publisher, etc.) ⏸️ (future enhancement)
  
- [x] **Series/Issue list caching** ✅
  - AC: Cache paginated series list with 2-minute TTL ✅
  - AC: Cache series detail (with issues) with 5-minute TTL ✅
  - AC: Invalidate on create/update/delete operations ✅
  - AC: Consider using ETag/Last-Modified headers for conditional requests ✅ (implemented in EPIC 12.3)

- [x] **Dashboard aggregates caching** ✅
  - AC: Cache stats (counts, totals) with 1-minute TTL ✅
  - AC: Cache "This Week" widget data with pull list cache ✅ (invalidated via InvalidatePullListCache)
  - AC: Invalidate on any status change ✅

### 12.2 Cache Implementation Patterns ✅ COMPLETED
- [x] **Cache-aside pattern service** ✅
  - AC: Create `ICacheService` abstraction ✅
  - AC: Implement Get/Set/Remove with TTL support ✅
  - AC: Support cache key generation with prefixes ✅
  - AC: Support bulk invalidation by prefix (e.g., all pull list caches) ✅
  
- [x] **Cache invalidation strategy** ✅
  - AC: Define clear invalidation triggers per data type ✅ (via prefix-based removal)
  - AC: Implement invalidation events/notifications ✅ (via RemoveByPrefix)
  - AC: Consider pub/sub for distributed cache scenarios (deferred - not needed for single-instance)
  - AC: Document invalidation matrix ✅ (CacheKeys constants + /api/v1/cache/keys endpoint)

- [x] **Cache configuration** ✅
  - AC: Configurable TTLs via settings ✅ (CacheSettings class)
  - AC: Ability to disable caching per category (for debugging) ✅ (Enabled flag)
  - AC: Cache statistics endpoint (hit/miss ratios) ✅ (GET /api/v1/cache/stats)

### 12.3 HTTP Response Caching ✅ COMPLETED
- [x] **API response caching** ✅
  - AC: Add Cache-Control headers for read-only endpoints ✅
  - AC: Implement ETag support for series/issue endpoints ✅
  - AC: Support If-None-Match/If-Modified-Since headers ✅
  - AC: Vary header for authenticated vs. public responses (N/A - no auth currently)

- [x] **Static asset caching** ✅
  - AC: Long-lived cache for cover images via HTTP headers ✅ (1-day cache)
  - AC: Cache-busting for UI assets (already handled by Vite build) ✅

### 12.4 ComicVine API Optimization
- [ ] **Request batching** (deferred)
  - AC: Batch multiple issue lookups into single request where API supports
  - AC: Queue and deduplicate concurrent identical requests
  
- [x] **Prefetching** ✅
  - AC: Prefetch next week's releases when viewing current week ✅
  - AC: Background refresh of stale cache entries (proactive refresh) ✅
  - AC: `PrefetchAdjacentWeeksAsync` method in PullListService ✅
  - AC: `prefetch` query parameter on /week and /discover/week endpoints ✅

- [ ] **Rate limit awareness** (deferred)
  - AC: Expose rate limit status in cache service
  - AC: Implement backoff when approaching limits
  - AC: Queue requests during rate limit cooldown

### 12.5 Intelligent Pull List Cache Lifecycle
**Status: READY**

Different caching behavior based on whether a week is "active" (before/on release day) vs "historical" (past release day).

- [ ] **Active week caching (before/on release day)**
  - AC: Background refresh pull list data on schedule while week is active
  - AC: Refresh interval configurable (default: 4 hours, matching ComicVine sync)
  - AC: Cache TTL >= refresh interval (ensures data is always cached between refreshes)
  - AC: Continue refreshing until N days after release day (configurable, default: 2 days)
  - AC: Rationale: Active weeks may have last-minute changes, delays, or additions

- [ ] **Historical week caching (past release day + buffer)**
  - AC: Stop scheduled refreshes after buffer period (release day + N days)
  - AC: Cache data with long TTL (e.g., 7 days or longer)
  - AC: Optional: Infrequent refresh for historical data (e.g., weekly scan of recent history)
  - AC: Rationale: Past releases rarely change; conserve API calls

- [ ] **Cache tier configuration**
  - AC: New setting: `PullListCacheBufferDays` (default: 2)
  - AC: New setting: `HistoricalCacheTtlDays` (default: 7)
  - AC: New setting: `HistoricalRefreshEnabled` (default: false)
  - AC: New setting: `HistoricalRefreshIntervalDays` (default: 7)

- [ ] **Manual refresh always available**
  - AC: "Refresh from ComicVine" button works regardless of cache tier
  - AC: Manual refresh updates cache with new data
  - AC: Manual refresh resets cache TTL

- [ ] **Cache status visibility**
  - AC: API returns cache metadata (last refreshed, next scheduled refresh, tier)
  - AC: UI shows when data was last refreshed
  - AC: UI indicates if viewing cached historical data

**Implementation Notes:**
- Integrate with existing `ComicVineRefreshBackgroundService`
- Track "active" vs "historical" state per week in cache metadata
- Consider storing historical data in database for persistence across restarts

### 12.7 Cache Technology Options

**Recommended: IMemoryCache (In-Process)**
- ✅ Already in use for ComicVine responses
- ✅ Zero additional infrastructure
- ✅ Fast (no serialization/network)
- ✅ Suitable for single-instance deployment
- ⚠️ Lost on restart (acceptable for most data)
- ⚠️ Not suitable for multi-instance (future consideration)

**Alternative: IDistributedCache (Redis/SQL Server)**
- 🔮 Future consideration for multi-instance scaling
- Preserves cache across restarts
- Requires additional infrastructure
- Add serialization overhead

**Decision:** Start with IMemoryCache for all caching. Design abstraction layer to allow future migration to distributed cache if needed.

### Cache TTL Reference Table
| Data Type | Layer | Current TTL | Recommended TTL | Invalidation Trigger |
|-----------|-------|-------------|-----------------|---------------------|
| Pull list (active week) | Frontend | 30 min | 30 min | Issue status change, monitoring change, manual refresh |
| Pull list (historical week) | Frontend | 30 min | 7 days* | Manual refresh only |
| Discovery (active week) | Backend | 30 min | 30 min | Scheduled refresh, manual refresh |
| Discovery (historical week) | Backend | 30 min | 7 days* | Manual refresh only |
| Discovery results | Frontend | 30 min | 30 min | Manual refresh |
| Series list | - | None | 2 minutes | Series CRUD |
| Series detail | - | None | 5 minutes | Series/Issue CRUD |
| Issue detail | - | None | 5 minutes | Issue CRUD, file association |
| Dashboard stats | - | None | 1 minute | Any status change |
| ComicVine volume | Backend | 24 hours | 24 hours | Manual refresh |
| ComicVine issue | Backend | 24 hours | 24 hours | Manual refresh |
| ComicVine publisher | Backend | 7 days | 7 days | Manual refresh |
| Cover images | Disk | Permanent | Permanent | Manual clear |

*Historical weeks (release day + 2 days past) use extended cache with optional infrequent background refresh.

**Rationale for 30-minute cache on ComicVine discovery:**
- Comic release schedules are set weeks/months in advance by publishers
- Data is submitted to distributors (Diamond/Lunar) well ahead of release
- ComicVine is updated by community contributors, not in real-time
- Once a week's releases are set, they essentially never change
- Mylar3 uses ~4-hour background refresh (we use on-demand with 30-min cache)

### 12.6 Monitoring & Diagnostics ✅ COMPLETED
- [x] **Cache metrics** ✅ (implemented in EPIC 12.2)
  - AC: Track hit/miss ratios per cache category ✅ (CacheStatistics)
  - AC: Track cache size and eviction counts ✅ (ItemCount, ItemsEvicted)
  - AC: Expose via /api/v1/cache/stats endpoint ✅
  
- [x] **Debug endpoints** ✅ (implemented in EPIC 12.2)
  - AC: DELETE /api/v1/cache - Clear all caches ✅
  - AC: DELETE /api/v1/cache/{prefix} - Clear specific cache ✅

### Implementation Priority
1. Pull list query caching (immediate performance win)
2. Dashboard aggregates caching (reduces DB load)
3. Series/Issue list caching (improves navigation)
4. HTTP response caching (reduces bandwidth)
5. ComicVine optimization (reduces API usage)
6. Monitoring (operational visibility)

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
- EPIC 9 depends on EPIC 1 (Series/Issue entities) and EPIC 5 (UI shell)
- EPIC 9.6 depends on EPIC 2 (Import Pipeline) for auto-match on import
- EPIC 9.8 depends on EPIC 7 (Mylar3 Migration) for config import patterns
- EPIC 10 depends on EPIC 3 (DecisionEngine) for candidate ranking
- EPIC 10.4 depends on EPIC 2 (Import Pipeline) for import handoff
- EPIC 10.5 depends on EPIC 7 (Mylar3 Migration) for config import patterns
- EPIC 10.6 depends on EPIC 5 (UI shell) for settings pages
- EPIC 11 depends on EPIC 9 (ComicVine Integration) for release date metadata
- EPIC 11.3 depends on EPIC 4 (DDL Provider) for auto-search functionality
- EPIC 11.5 depends on EPIC 5 (UI shell) for calendar and list views
- EPIC 11.6 depends on EPIC 7 (Mylar3 Migration) for config import patterns
- EPIC 12 has no hard dependencies; can be implemented incrementally alongside other work