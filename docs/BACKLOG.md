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
  - AC: Series list page (table with status indicators, bulk actions) ✅
  - AC: Collections list page ✅
  - AC: Activity page (thin but functional) ✅
  - AC: Manual Import page ✅
    - Shows staging folder stats (total files, auto-matched, need review) ✅
    - Table with filename, parsed info, match status, confidence scores ✅
    - Select all/individual file selection ✅
    - Import selected matched files ✅
    - Refresh button ✅
    - Edit match button with series search modal ✅
    - Reject button with confirmation and reason input ✅
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

#### 8.1.1 GetComics.org Adapter (Primary) - PARTIAL ✅
- [x] **HTML scraping for GetComics** ✅
  - AC: Parse search results page for release links ✅
  - AC: Extract all download host links from release pages ✅
  - AC: Handle pagination for search results (basic - offset support)
  - AC: Parse release details (title, size, date posted, tags) ✅
- [x] **GetComics search integration** ✅
  - AC: Search by series name, issue number ✅ (via BuildSearchUrl)
  - AC: Search by keyword/tag/category ✅ (via RawQuery)
  - AC: RSS feed polling for new releases (/feed/) ✅
  - AC: Category browsing (DC, Marvel, Image, etc.) ✅
  - Note: IRssFeedService with RSS 2.0/Atom support, 31 unit tests
- [x] **GetComics link resolution** ✅
  - AC: Follow redirects to actual download URLs ✅ (handled by HttpClient)
  - AC: Handle multiple mirror options with priority ✅ (host priority sorting)
  - AC: Detect dead/expired links and skip ✅ (via VerifyLinkAsync)

#### 8.1.2 ReadComicOnline Adapter (Secondary) ✅ COMPLETED
- [x] **Determine homepage address** ✅
  - AC: Parse index page for "Go to Homepage" button and update base URL as needed ✅
  - AC: Support multiple domain variants (li, to, org, cc) ✅
  - AC: DetectHomepageAsync method for dynamic URL discovery ✅
- [x] **HTML scraping for ReadComicOnline** ✅
  - AC: Parse search results page for release links ✅
  - AC: Extract all download host links from release pages ✅
  - AC: Handle pagination for search results ✅ (via BuildSearchUrl)
  - AC: Parse release details (title, size, date posted, tags) ✅
- [x] **ReadComicOnline search integration** ✅
  - AC: Search by series name, issue number ✅
  - AC: Search by keyword/tag/category ✅
  - AC: Category browsing (DC, Marvel, Image, etc.) ✅
  - AC: Publisher browsing with slug mapping ✅
  - AC: GetAvailableCategories with publishers and genres ✅
- [x] **ReadComicOnline link resolution** ✅
  - AC: Follow redirects to actual download URLs ✅ (via BaseDdlSiteAdapter)
  - AC: Handle multiple mirror options with priority ✅
  - AC: Detect dead/expired links and skip ✅ (via VerifyLinkAsync)
  - Note: 25 unit tests, registered in DdlSiteAdapterFactory

### 8.2 Download Host Resolvers (File Acquisition)

#### 8.2.1 Direct/Main Server Downloads ✅ COMPLETED
- [x] **Direct HTTP downloads** ✅
  - AC: Standard HTTP GET with resume support ✅ (DirectDownloadResolver)
  - AC: Handle Content-Disposition filename ✅
  - AC: Verify file integrity (size, magic bytes) ✅ (via HEAD request metadata)

#### 8.2.2 MediaFire Resolver ✅ COMPLETED
- [x] **MediaFire link handling** ✅
  - AC: Parse MediaFire share page ✅
  - AC: Extract direct download URL ✅
  - AC: Handle "Download" button extraction ✅ (multiple patterns)
  - AC: Detect expired/removed files ✅

#### 8.2.3 Mega.nz Resolver
- [ ] **Mega link handling** (deferred - requires encryption handling)
  - AC: Parse mega.nz/#! and mega.nz/file/ URLs
  - AC: Handle Mega's encryption (MEGAcmd or API)
  - AC: Support folder links with file selection
  - AC: Rate limit awareness (free tier limits)

#### 8.2.4 Pixeldrain Resolver ✅ COMPLETED
- [x] **Pixeldrain link handling** ✅
  - AC: Extract file ID from URL ✅
  - AC: Use Pixeldrain API for direct download ✅
  - AC: Handle bandwidth limits ✅ (via API error handling)

#### 8.2.5 Dropbox Resolver ✅ COMPLETED
- [x] **Dropbox link handling** ✅
  - AC: Convert share links to direct download URLs ✅
  - AC: Handle dl=0 to dl=1 conversion ✅
  - AC: Support folder links (detection only - folder download deferred)

#### 8.2.6 Google Drive Resolver ✅ COMPLETED
- [x] **Google Drive link handling** ✅
  - AC: Parse drive.google.com share links ✅
  - AC: Handle virus scan warning bypass ✅ (confirm=t parameter)
  - AC: Extract confirmation token for large files ✅
  - AC: Support folder links with file listing (detection only - folder download deferred)

#### 8.2.7 Legacy/Additional Hosts - PARTIAL ✅
- [x] **Zippyshare resolver** ✅ (defunct, graceful handling)
  - AC: Detect and skip defunct links gracefully ✅
  - AC: Returns HostUnavailable with shutdown date info ✅
  - AC: IsAvailable = false so factory excludes from active resolvers ✅
- [ ] **Rapidgator/Uploaded resolver** (premium) - deferred
  - AC: Support premium account credentials
  - AC: Free tier with wait times (optional)
- [x] **1fichier resolver** ✅
  - AC: Parse download page ✅ (CDN, CZ, FR domains)
  - AC: Handle wait times for free users ✅ (detection)
  - AC: Extract filename from page ✅ (class, title, og:title)
  - AC: Extract file size ✅ (MB/GB, French units MO/GO)
  - AC: Error detection (file not found, password protected, premium only) ✅
- [ ] **Usenet/NZB integration** - deferred
  - AC: NZB file download from DDL sites
  - AC: Pass to configured Usenet downloader (SABnzbd, NZBGet)

### 8.3 Download Host Priority & Fallback ✅ COMPLETED
- [x] **Host priority configuration** ✅
  - AC: User-configurable host preference order ✅ (via resolver Priority property)
  - AC: Default priority: Direct > Mega > MediaFire > Pixeldrain > GDrive > Others ✅
- [x] **Automatic fallback** ✅
  - AC: Try next host on failure ✅ (DdlDownloadService fallback loop)
  - AC: Track host reliability per DDL site (deferred - statistics tracking)
  - AC: Blacklist consistently failing hosts temporarily (deferred)

### 8.4 DDL Site Health Monitoring - PARTIAL ✅
- [ ] **Site availability checks** (deferred)
  - AC: Periodic health checks for each configured site
  - AC: Detect site changes that break scraping (CSS/HTML changes)
  - AC: Alert/disable adapter on repeated failures
  - AC: Version detection for known site layouts
- [x] **Rate limiting per site** ✅
  - AC: Respect site-specific rate limits ✅ (IDdlRateLimiter)
  - AC: Configurable delays between requests ✅ (minDelayMs)
  - AC: Request queuing to prevent bans ✅ (AcquireAsync blocks until available)
  - AC: Cloudflare challenge handling (deferred)

### 8.5 DDL Adapter Tests ✅ COMPLETED
- [x] **GetComics fixture tests** ✅
  - AC: Mock HTML responses for search results ✅
  - AC: Mock HTML responses for release pages ✅
  - AC: Verify correct link extraction for all host types ✅
- [x] **Download host resolver tests** ✅
  - AC: Mock responses for each host type ✅ (35 unit tests)
  - AC: Test redirect following and URL extraction ✅
  - AC: Test failure scenarios (expired, removed, rate limited) ✅
- [x] **Integration tests** ✅ (27 tests)
  - AC: Cached real responses for regression testing ✅
  - AC: End-to-end: search → parse → filter → resolve tests ✅
  - AC: Parser edge case tests ✅
  - AC: Filter configuration tests ✅
  - AC: Resolver factory selection tests ✅
  - AC: RSS feed service tests ✅
  - AC: Error handling and failure reason tests ✅

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
  - AC: UI refresh button on Series Detail page ✅
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
  - AC: Bulk selection support (mark as owned, mark as wanted, skip) ✅
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

### 9.11 Series Detail Page - Issues Display ✅ COMPLETED

- [x] **Issues list display** ✅
  - AC: When clicking a series in the library, navigate to series detail page ✅
  - AC: Series detail page fetches and displays all issues for that series ✅
  - AC: API: GET /api/v1/series/{id}/issues returns paginated issues list ✅
  - AC: Handle empty state gracefully (series with no issues yet) ✅
  - AC: Fixed missing Status property in IssueDto ✅

- [x] **Cover view for issues** ✅
  - AC: Grid layout showing issue covers ✅
  - AC: Cover image with fallback for missing covers ✅
  - AC: Issue number overlay on cover ✅
  - AC: Status indicator overlay (colored badge/icon for Wanted/Owned/Skipped/Missing) ✅
  - AC: Hover state showing action buttons ✅
  - AC: Click to select issue ✅

- [x] **List view for issues** ✅
  - AC: Table/list layout with columns: Issue #, Title, Release Date, Status, Tags, Actions ✅
  - AC: Sortable columns (issue number, release date, status, title) ✅
  - AC: Filterable by status (All, Wanted, Owned, Skipped, Missing) ✅

- [x] **Action buttons per issue** ✅
  - AC: "Mark as Wanted" button (adds to wanted list) ✅
  - AC: "Mark as Owned" button (marks as owned without file) ✅
  - AC: "Skip" button (excludes from wanted list) ✅
  - AC: Buttons show current state (disabled when already in that state) ✅
  - Note: "Search" and "Edit" buttons deferred (requires search integration)

- [x] **Status indicators** ✅
  - AC: Visual distinction for each status ✅
    - Wanted: Yellow/warning badge
    - Owned: Green/success badge
    - Skipped: Gray badge
    - Edition: Blue/info badge (satisfied by collected edition)
  - AC: Status badges visible in both cover and list views ✅
  - AC: Bulk selection checkbox for multi-issue actions ✅

- [x] **View toggle and persistence** ✅
  - AC: Toggle button to switch between Cover View and List View ✅
  - AC: Remember user's view preference via UI settings API ✅
  - AC: Default to Cover View ✅

- [x] **Bulk actions** ✅
  - AC: Select multiple issues via checkboxes ✅
  - AC: Bulk "Mark as Wanted" / "Mark as Owned" / "Skip" actions ✅
  - AC: Select all / deselect all toggle ✅
  - AC: Show count of selected issues ✅

### 9.12 Series Status Accuracy ✅ COMPLETED

- [x] **ComicVine status sync** ✅
  - AC: Fetch series status from ComicVine API during metadata sync ✅
  - AC: ComicVine `count_of_issues` vs actual issues can indicate completion ✅
  - AC: ComicVine `date_last_updated` may indicate staleness ✅
  - AC: Added `StatusSource` enum: Auto, ComicVine, Manual ✅

- [x] **Status determination logic** ✅
  - AC: Added `SeriesStatusDeterminer` class with heuristics ✅
  - AC: If last issue was published > 2 years ago, consider Ended ✅
  - AC: If series has no new issues and count matches expected, consider Ended ✅
  - AC: If actively publishing or recent issue, set to Continuing ✅
  - AC: Handle edge cases: mini-series (4-12 issues with no recent activity) ✅

- [x] **Status refresh** ✅
  - AC: Update series status during metadata refresh ✅
  - AC: Respects manual override (doesn't change if StatusSource=Manual) ✅
  - AC: Manual refresh button already exists on series detail page ✅

- [ ] **UI indicators** (deferred to future iteration)
  - AC: Filter series list by status
  - AC: Sort series by status

- [x] **Status override** ✅
  - AC: PUT /api/v1/series/{id}/status for manual override ✅
  - AC: DELETE /api/v1/series/{id}/status/override to reset to auto ✅
  - AC: StatusSource field tracks how status was determined ✅
  - AC: Manual status not overwritten during auto-refresh ✅

**Implementation:**
- `SeriesStatusDeterminer` class with configurable thresholds
- 14 unit tests covering all scenarios
- Migration for `StatusSource` column

---

## EPIC 10: NZB/Usenet Support (Mylar3/Sonarr/Radarr Parity)
Usenet (NZB) support for comic acquisition. Must achieve behavioral parity with Mylar3, Sonarr, and Radarr's Usenet integration.

### 10.1 NZB Indexer Integration - PARTIAL ✅
- [x] **Newznab API client** ✅
  - AC: Standard Newznab API implementation (used by most NZB indexers) ✅
  - AC: API key authentication ✅
  - AC: Search by series name, issue number, year ✅
  - AC: Category filtering (comics category IDs) ✅
  - AC: Parse NZB search results into candidates ✅
- [ ] **NZBHydra2 support** (deferred)
  - AC: Aggregate searches across multiple indexers
  - AC: Single API endpoint for multiple backends
  - AC: Respect indexer priorities from NZBHydra
- [x] **Built-in indexer presets** ✅
  - AC: Pre-configured settings for popular NZB indexers ✅
  - AC: NZBgeek, DrunkenSlug, NZBFinder, etc. ✅
  - AC: Easy setup with just API key ✅
- [ ] **Indexer health monitoring** (deferred)
  - AC: Track indexer response times
  - AC: Detect and handle rate limiting
  - AC: Automatic failover to backup indexers

### 10.2 NZB Download Client Integration - PARTIAL ✅
- [x] **SABnzbd integration** ✅
  - AC: Add NZB to SABnzbd via API ✅
  - AC: Category assignment for comics ✅
  - AC: Priority configuration ✅
  - AC: Monitor download progress ✅
  - AC: Detect completion and trigger import (via history API) ✅
- [x] **NZBGet integration** ✅ (completed in EPIC 14.2)
  - AC: Add NZB to NZBGet via API ✅
  - AC: Category and priority support ✅
  - AC: Progress monitoring ✅
  - Note: Full implementation in EPIC 14.2
  - AC: Post-processing script integration
- [x] **Download client health checks** ✅
  - AC: Verify connectivity on startup ✅ (TestConnectionAsync)
  - AC: Monitor disk space warnings ✅ (GetDiskSpaceAsync)
  - AC: Handle client unavailability gracefully ✅

### 10.3 NZB Candidate Processing ✅ COMPLETED
- [x] **NZB release parsing** ✅
  - AC: Parse NZB release names (similar to DDL parser) ✅ (NzbReleaseParser)
  - AC: Extract series, issue, year, quality, format ✅
  - AC: Handle Usenet naming conventions ✅ (scene naming with dots, release groups)
  - AC: Publisher detection (Marvel, DC, Image, etc.) ✅
  - AC: Collection detection (TPB, HC, Omnibus, etc.) ✅
  - AC: Release modifier detection (REPACK, PROPER, INTERNAL) ✅
- [x] **NZB candidate model** ✅
  - AC: Store indexer source, NZB URL, size, age ✅ (NzbCandidate)
  - AC: Quality scoring aligned with DecisionEngine ✅ (CalculateQualityScore)
  - AC: Integrate with existing Candidate model ✅ (ToCandidate() conversion)
- [x] **NZB filtering rules** ✅
  - AC: Minimum/maximum age limits ✅
  - AC: Size limits (same as DDL) ✅
  - AC: Banned/required words (same as DDL) ✅
  - AC: Prefer certain indexers ✅
  - AC: Password protection rejection ✅
  - AC: Category include/exclude ✅
  - AC: Parse confidence threshold ✅
  - AC: Format and quality preferences ✅
  - Note: 84 unit tests covering parser and filter service

### 10.4 NZB → Import Handoff ✅ COMPLETED
- [x] **Post-download detection** ✅
  - AC: Monitor SABnzbd/NZBGet for completed downloads ✅
  - AC: Detect completed comic files in download directory ✅
  - AC: Handle unpacking (RAR, ZIP) automatically ✅ (ZIP supported, RAR/7z deferred)
- [x] **Import integration** ✅
  - AC: Move completed files to staging ✅
  - AC: Auto-match to series/issue ✅
  - AC: Create HistoryEvent linking NZB → import ✅
  - AC: Handle failed downloads (incomplete, password-protected) ✅
  - Note: NzbImportService with background polling, 19 unit tests

### 10.5 NZB Configuration & Settings - PARTIAL ✅
- [x] **Indexer configuration** ✅
  - AC: Add/edit/delete NZB indexers ✅
  - AC: Test indexer connectivity ✅
  - AC: Priority ordering for multiple indexers ✅
  - AC: Enable/disable per indexer ✅
- [x] **Download client configuration** ✅
  - AC: SABnzbd: URL, API key, category, priority ✅
  - AC: NZBGet: URL, username, password, category (deferred)
  - AC: Test connection button ✅
  - AC: Default download client selection ✅
- [ ] **Mylar3 NZB settings import** (deferred)
  - AC: Parse Mylar3 config.ini for NZB settings
  - AC: Import indexer configurations
  - AC: Import SABnzbd/NZBGet settings
  - AC: Validation report

### 10.6 NZB UI - PARTIAL ✅
- [x] **Indexers settings page** ✅
  - AC: NZB Indexers section (separate from DDL) ✅
  - AC: Add indexer modal with Newznab fields ✅
  - AC: Preset selection for popular indexers ✅
  - AC: Test and status indicators ✅
- [x] **Download clients settings page** ✅
  - AC: SABnzbd configuration panel ✅
  - AC: NZBGet configuration panel (deferred)
  - AC: Connection test results ✅
- [x] **Unified download client modal** ✅
  - AC: "Add Download Client" button opens a modal with implementation type selector ✅
  - AC: Implementation dropdown includes: SABnzbd ✅, NZBGet (deferred), qBittorrent (deferred), Transmission (deferred), Deluge (deferred)
  - AC: Modal form fields change dynamically based on selected implementation type ✅
  - AC: SABnzbd fields: Host, API Key, Category, Use SSL ✅
  - AC: All download clients (including SABnzbd) managed in single unified list ✅
  - AC: Removed separate SABnzbd section, merged into unified modal ✅
  - Note: SabnzbdDownloadProvider with 21 unit tests
- [x] **Separate host and port fields in download client UI** ✅
  - AC: Split single "Host" field into separate "Host" and "Port" fields ✅
  - AC: If no port provided, default to port 80 ✅
  - AC: If no port provided AND "Use SSL/HTTPS" is enabled, default to port 443 ✅
  - AC: If port is provided, use the provided port regardless of SSL setting ✅
  - AC: If port is provided AND "Use SSL/HTTPS" is enabled, use provided port with HTTPS ✅
  - AC: Port field should show placeholder with default value (80 or 443 based on SSL toggle) ✅
  - AC: Validation: port must be 1-65535 ✅ (HTML5 input validation)
  - AC: Apply to all download client types (SABnzbd, future NZBGet, qBittorrent, etc.) ✅
  - AC: Backend constructs full URL from host + port + SSL setting ✅
  - AC: "Test" button tests connection using currently entered form data (not saved data) ✅
  - AC: If test is successful, automatically save the download client configuration ✅
  - AC: Show success message: "Connection successful. Settings saved." ✅
  - AC: If test fails, do NOT save; show error message with failure reason ✅
  - AC: On successful test, set Status to "Healthy" and clear any previous error message ✅
  - AC: On failed test, set Status to "Unhealthy" and store the error message for display ✅
  - Note: 21 new unit tests for SabnzbdSettings (10 tests) and legacy host format parsing (11 tests)
- [ ] **Activity integration** (deferred)
  - AC: Show NZB downloads in activity feed
  - AC: Download progress from SABnzbd/NZBGet
  - AC: Queue management (pause, remove, priority)
- [x] **Download settings scoping by client type** ✅
  - AC: "Maximum concurrent downloads" setting should ONLY apply to DDL download clients ✅
  - AC: SABnzbd/NZBGet manage their own concurrent downloads - setting should not affect them ✅ (documented)
  - AC: Future torrent clients (qBittorrent, Transmission) manage their own queues - setting should not affect them ✅ (documented)
  - AC: UI should clarify which settings apply to which client types (e.g., "DDL Settings" section) ✅
  - AC: "Automatically retry failed downloads" may have different meanings: ✅
    - For DDL: Retry the HTTP download on network failure (existing behavior) ✅
    - For Usenet: Re-add NZB to queue if download fails (different behavior - may need re-search) ✅ (documented)
    - For Torrent: Resume stalled torrent or find alternative (different behavior) ✅ (documented)
  - AC: Consider splitting into separate settings per client category or adding contextual help ✅
  - AC: Document behavior differences in UI tooltips/descriptions ✅
  - Note: UI section renamed to "DDL Download Settings" with explanatory note and clarified field labels

### 10.7 NZB Conformance Tests ✅ COMPLETED
- [x] **Newznab API tests** ✅
  - AC: Mock indexer responses ✅
  - AC: Test search parameter encoding ✅
  - AC: Test result parsing ✅
- [x] **Download client tests** ✅
  - AC: Mock SABnzbd API responses ✅ (21 tests)
  - AC: Mock NZBGet API responses (deferred - NZBGet not implemented)
  - AC: Test add/status/remove operations ✅
- [x] **Integration tests** ✅ (partial)
  - AC: Full flow: search → download → import (deferred - import not implemented)
  - AC: Multi-indexer aggregation ✅
  - AC: Download client failover (deferred)

**Total NZB tests: 63**
- NewznabClientTests: 35 tests
- SabnzbdClientTests: 21 tests
- NzbIndexerProviderTests: 18 tests
- NzbEndpointsTests: 17 tests

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

- [x] **Webhook notification provider** ✅ COMPLETED
  - AC: `INotificationProvider` interface for external notification services ✅
  - AC: `WebhookNotificationProvider` implementation ✅
    - Supports Discord webhooks (with embeds, colors, fields) ✅
    - Supports Slack webhooks (with blocks, sections, images) ✅
    - Supports generic HTTP webhooks (JSON payload) ✅
  - AC: Auto-detects webhook type from URL ✅
  - AC: Configurable notification events (NewRelease, Grabbed, Imported, etc.) ✅
  - AC: Configurable payload options (include series info, images) ✅
  - AC: Basic authentication support ✅
  - AC: Custom headers support ✅
  - AC: API endpoints for webhook CRUD ✅
    - GET /api/v1/notifications/providers ✅
    - GET /api/v1/notifications/providers/{id} ✅
    - POST /api/v1/notifications/providers ✅
    - PUT /api/v1/notifications/providers/{id} ✅
    - DELETE /api/v1/notifications/providers/{id} ✅
    - POST /api/v1/notifications/providers/{id}/test ✅
    - POST /api/v1/notifications/providers/test ✅
  - AC: Settings UI for managing webhooks ✅
  - AC: 25 unit tests covering all webhook functionality ✅

- [ ] **Additional notification channels** (future)
  - AC: Email notifications (SMTP configuration)
  - AC: Pushover/Pushbullet support

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
  
- [x] **Prefetching** ✅ (REMOVED - Iteration 064)
  - ~~AC: Prefetch next week's releases when viewing current week~~ → Replaced by startup cache population
  - AC: Background refresh of stale cache entries (proactive refresh) ✅
  - ~~AC: `PrefetchAdjacentWeeksAsync` method in PullListService~~ → Removed (caused DbContext disposal errors)
  - ~~AC: `prefetch` query parameter on /week and /discover/week endpoints~~ → Removed
  - Note: Functionality now provided by `ComicVineRefreshBackgroundService` which pre-populates cache on startup and refreshes on schedule

- [ ] **Rate limit awareness** (deferred)
  - AC: Expose rate limit status in cache service
  - AC: Implement backoff when approaching limits
  - AC: Queue requests during rate limit cooldown

### 12.5 Intelligent Pull List Cache Lifecycle ✅ COMPLETED
**Status: COMPLETED**

Different caching behavior based on whether a week is "active" (before/on release day) vs "historical" (past release day).

- [x] **Active week caching (before/on release day)** ✅
  - AC: Background refresh pull list data on schedule while week is active ✅
  - AC: Refresh interval configurable (default: 4 hours, matching ComicVine sync) ✅
  - AC: Cache TTL >= refresh interval (ensures data is always cached between refreshes) ✅
  - AC: Continue refreshing until N days after release day (configurable, default: 2 days) ✅
  - AC: Rationale: Active weeks may have last-minute changes, delays, or additions ✅

- [x] **Historical week caching (past release day + buffer)** ✅
  - AC: Stop scheduled refreshes after buffer period (release day + N days) ✅
  - AC: Cache data with long TTL (e.g., 7 days or longer) ✅
  - AC: Optional: Infrequent refresh for historical data (e.g., weekly scan of recent history) ✅
  - AC: Rationale: Past releases rarely change; conserve API calls ✅

- [x] **Cache tier configuration** ✅
  - AC: New setting: `CacheBufferDays` (default: 2) ✅
  - AC: New setting: `HistoricalCacheTtlDays` (default: 7) ✅
  - AC: New setting: `HistoricalRefreshEnabled` (default: false) ✅
  - AC: New setting: `HistoricalRefreshIntervalDays` (default: 7) ✅
  - AC: New setting: `ActiveCacheTtlMinutes` (default: 30) ✅

- [x] **Manual refresh always available** ✅
  - AC: "Refresh from ComicVine" button works regardless of cache tier ✅
  - AC: Manual refresh updates cache with new data ✅
  - AC: Manual refresh resets cache TTL ✅

- [x] **Cache status visibility** ✅
  - AC: API returns cache metadata (last refreshed, next scheduled refresh, tier) ✅
  - AC: UI shows when data was last refreshed (via CacheMetadata.LastRefreshed) ✅
  - AC: UI indicates if viewing cached historical data (via CacheMetadata.Tier) ✅

**Implementation Notes (COMPLETED):**
- Integrated with existing `ComicVineRefreshBackgroundService` ✅
- Track "active" vs "historical" state per week in cache metadata ✅
- Added `CacheTier` enum and `PullListCacheMetadata` class ✅
- Added cache tier settings to `PullListSettings` ✅
- API responses now include cache metadata ✅

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

## EPIC 13: Logging & Diagnostics (Mylar3/Sonarr/Radarr Parity)

Comprehensive logging system for troubleshooting, monitoring, and operational visibility. Must achieve behavioral parity with Mylar3, Sonarr, and Radarr logging capabilities.

### 13.1 File-Based Logging
**Status: COMPLETE** ✅

⚠️ **CRITICAL SECURITY REQUIREMENT: NEVER LOG SENSITIVE DATA**
- API keys (ComicVine, indexers, download clients) MUST be masked/redacted
- Passwords and authentication tokens MUST be masked
- Use `***REDACTED***` or `***API_KEY***` placeholders in logs
- Apply masking at the logging sink level (not per-call)
- Audit all log statements for potential credential leaks

- [x] **Sensitive data protection** ✅
  - AC: Implement Serilog destructuring policy to mask sensitive fields ✅
  - AC: Auto-detect and mask: `apiKey`, `api_key`, `password`, `token`, `secret`, `credential` ✅
  - AC: Mask query string parameters containing sensitive keys ✅
  - AC: Mask Authorization headers in HTTP logs ✅
  - AC: Mask connection strings (show server/database only, not credentials) ✅
  - AC: Unit tests to verify no credentials appear in log output ✅
  - Note: 35 comprehensive tests covering SensitiveDataDestructuringPolicy, SensitiveDataEnricher, query param masking, and end-to-end log verification

- [x] **Log file configuration** ✅
  - AC: Configurable log directory (default: `{data}/logs/`) ✅ (via SHORTBOXERR_LOG_DIR env var)
  - AC: Log file naming: `shortboxerr.log`, `shortboxerr.txt`, or configurable ✅ (uses shortboxerr.log)
  - AC: Configurable log level: Trace, Debug, Info, Warn, Error, Fatal ✅ (via SHORTBOXERR_LOG_LEVEL env var and UI)
  - AC: Default log level: Info ✅
  - AC: Separate log level for console vs file output ✅ (consoleLevel parameter in SerilogConfiguration)

- [x] **Log rotation** ✅
  - AC: Configurable max log file size (default: 10MB, Sonarr default: 1MB) ✅
  - AC: Configurable number of rotated files to keep (default: 5) ✅
  - AC: Automatic rotation when size limit reached ✅
  - AC: Date-based rotation option (daily/weekly) ✅ (daily implemented)
  - AC: Compressed archive of rotated logs (optional) (deferred)

- [x] **Log format** ✅
  - AC: Timestamp with milliseconds: `2026-02-04 20:30:45.123` ✅
  - AC: Log level indicator: `[Info]`, `[Warn]`, `[Error]`, etc. ✅
  - AC: Source/category: `[PullListService]`, `[ComicVineClient]`, etc. ✅
  - AC: Correlation ID for request tracing ✅
    - CorrelationIdMiddleware reads X-Correlation-ID or X-Request-ID headers
    - Generates unique ID if not provided (format: yyyyMMddHHmmss-random8)
    - CorrelationIdEnricher adds to all log events in request scope
    - New template preset: `SHORTBOXERR_LOG_TEMPLATE=correlation`
    - 17 tests for middleware, enricher, and template handling
  - AC: Structured logging support (JSON format option) ✅
    - JsonOutputTemplate: `{ "timestamp": ..., "level": ..., "correlationId": ..., "source": ..., "message": ..., "properties": ... }`
    - Use `SHORTBOXERR_LOG_TEMPLATE=json` to enable

- [x] **Human-readable log formatting** ✅
  - AC: Consistent column alignment for timestamp, level, and source context ✅
  - AC: Shorten long source context names (e.g., `Shortboxerr.Infrastructure.ComicVine.ComicVineClient` → `ComicVineClient`) ✅
  - AC: Use fixed-width level indicators for alignment (e.g., `[INF]`, `[WRN]`, `[ERR]`) ✅
  - AC: Visual separator between log entries for multi-line messages ✅ (via NewLine in template)
  - AC: Indent continuation lines for stack traces and structured properties ✅ (Serilog default)
  - AC: Color-coded output for console sink (already partial, enhance contrast) ✅ (AnsiConsoleTheme.Code)
  - AC: Configurable output template via settings or environment variable ✅ (SHORTBOXERR_LOG_TEMPLATE)
  - AC: Example format: `[2026-02-09 14:30:45.123] [INF] [ComicVineClient] Cache hit for series 12345` ✅
  - AC: Exception formatting: stack trace on separate indented lines ✅
  - AC: Structured property formatting: key=value pairs on same line when short, wrapped when long ✅
  - Note: 38 tests covering ShortSourceContextEnricher, output template presets, and end-to-end formatting

- [x] **Serilog integration** ✅
  - AC: Use Serilog as logging provider (industry standard for .NET) ✅
  - AC: Configure sinks: Console, File, (optional: Seq, Elasticsearch) ✅
  - AC: Enrichers for context: Machine name, environment, version ✅
  - AC: Async file writing for performance ✅

### 13.2 Log Categories & Content
**Status: COMPLETE** ✅

- [x] **Application lifecycle logs** ✅
  - AC: Startup/shutdown events with version info ✅
  - AC: Configuration loaded events ✅
  - AC: Database migration events ✅
  - AC: Background service start/stop ✅

- [x] **API request logging** ✅
  - AC: HTTP request/response logging (configurable verbosity) ✅
  - AC: Request duration timing ✅
  - AC: Error responses with details ✅
  - AC: **MANDATORY**: Mask API keys, passwords, tokens in all request/response logs ✅
  - AC: **MANDATORY**: Mask Authorization headers (show type only, e.g., "Bearer ***") ✅
  - AC: **MANDATORY**: Mask sensitive query parameters (?apikey=***) ✅

- [x] **ComicVine API logging** ✅
  - AC: API calls with endpoint and parameters ✅
  - AC: Rate limiting events ✅
  - AC: Cache hits/misses ✅
  - AC: Response times and status codes ✅
  - AC: Error responses with retry info ✅

- [x] **Download client logging** ✅
  - AC: Search requests and results count ✅
  - AC: Download initiated/completed/failed events ✅
  - AC: Provider connection status ✅
  - AC: Candidate ranking decisions (verbose mode) ✅

- [x] **Import pipeline logging** ✅
  - AC: File detection events ✅
  - AC: Parsing results (series, issue, format) ✅
  - AC: Match decisions with confidence scores ✅
  - AC: Import success/failure with paths ✅
  - AC: Duplicate detection events ✅

- [x] **Background service logging** ✅
  - AC: Scheduled task execution start/complete ✅
  - AC: Metadata refresh progress ✅
  - AC: Release day processing events ✅
  - AC: Error recovery attempts ✅

### 13.3 Log Viewer UI
**Status: COMPLETE** ✅

- [x] **Logs page** ✅
  - AC: System > Logs navigation item ✅
  - AC: Real-time log streaming (WebSocket or polling) ✅ (polling, 5s interval)
  - AC: Log level filtering (show only errors, warnings, etc.) ✅
  - AC: Text search/filter within logs ✅
  - AC: Category/source filtering (category shown, future enhancement)
  - AC: Time range filtering (line count selector implemented)

- [x] **Log display** ✅
  - AC: Color-coded log levels (red=error, yellow=warn, etc.) ✅
  - AC: Expandable log entries for full details (raw line shown)
  - AC: Copy log entry to clipboard (future enhancement)
  - AC: Monospace font for readability ✅
  - AC: Auto-scroll with pause option ✅

- [x] **Log file management** ✅
  - AC: List of log files with sizes and dates ✅
  - AC: Download log files ✅
  - AC: Delete old log files ✅
  - AC: View rotated/archived logs ✅

### 13.4 Diagnostic Tools
**Status: COMPLETE** ✅

- [x] **System information endpoint** ✅
  - AC: GET /api/v1/system/info returns diagnostic info ✅
  - AC: .NET runtime version ✅
  - AC: OS and architecture ✅
  - AC: Database provider and version ✅
  - AC: Disk space (data directory) ✅
  - AC: Memory usage ✅
  - AC: Uptime ✅

- [x] **Health check logging** ✅
  - AC: Periodic health check results logged ✅
  - AC: Database connectivity ✅
  - AC: External API reachability (ComicVine) ✅
  - AC: Download client connectivity (via ComicVine check)
  - AC: Disk space warnings ✅

- [x] **Debug mode** ✅
  - AC: Command-line flag: `--debug` or `-d` ✅
  - AC: Environment variable: `SHORTBOXERR_DEBUG=true` ✅
  - AC: Enables verbose logging without config change ✅
  - AC: Logs full stack traces ✅
  - AC: Logs SQL queries (EF Core) ✅

### 13.5 Log Settings UI
**Status: COMPLETE** ✅

- [x] **Settings page integration** ✅
  - AC: Settings > General > Logging section ✅
  - AC: Log level dropdown (Trace to Fatal) ✅
  - AC: Log file path configuration ✅ (read-only, env-based)
  - AC: Max file size setting ✅
  - AC: Rotation file count setting ✅
  - AC: Enable/disable console logging ✅

- [x] **Advanced settings** ✅
  - AC: Enable SQL query logging (debug) ✅
  - AC: Enable HTTP request body logging (debug) ✅
  - AC: Enable full stack traces ✅
  - AC: Log retention days (auto-cleanup) ✅

### 13.6 Parity Reference

**Sonarr/Radarr logging features:**
- Log files in `logs/` subdirectory
- `sonarr.txt` / `radarr.txt` main log
- `sonarr.debug.txt` for verbose logging
- Configurable log level in UI
- Log file rotation by size
- Logs page with filtering and search
- Trace ID for request correlation

**Mylar3 logging features:**
- `mylar.log` in data directory
- Configurable log level in config
- Rotation with numbered backups
- Verbose mode for debugging
- Separate logs for post-processing

**Implementation Notes:**
- Use Serilog with File and Console sinks
- Add `Serilog.AspNetCore` for request logging
- Use `ILogger<T>` throughout codebase (already standard)
- Store log settings in `SystemSettings` table
- Consider `Serilog.Sinks.Async` for file performance

**⚠️ Security Implementation (MANDATORY):**
- Implement `IDestructuringPolicy` for Serilog to auto-mask sensitive properties
- Create `SensitiveDataMaskingEnricher` to scrub logs before writing
- Regex patterns for common sensitive field names: `/api[_-]?key/i`, `/password/i`, `/token/i`, `/secret/i`
- Test with actual API keys to verify they NEVER appear in log files
- Code review checklist item: "No credentials in log statements"

---

## EPIC 14: Future Enhancements

### 14.1 Deferred Items Completion Tracking
Track and prioritize completion of deferred items across all EPICs.

- [ ] **Deferred items audit**
  - AC: Review all items marked "(deferred)" across EPICs 4, 8, 10, 11
  - AC: Categorize by effort (small/medium/large) and user impact (high/medium/low)
  - AC: Create prioritized list based on user requests and parity requirements
  - AC: Document which items are blocked and what unblocks them

- [ ] **Deferred item: Variant cover detection** (EPIC 9)
  - Original: "Variant cover detection (optional) - DEFERRED"
  - Effort: Medium
  - Impact: Medium (nice-to-have for collectors)

- [ ] **Deferred item: Site availability checks** (EPIC 8)
  - Original: "Site availability checks (deferred)"
  - Effort: Medium
  - Impact: High (helps maintain DDL reliability)

- [ ] **Deferred item: NZBHydra2 support** (EPIC 10)
  - Original: "NZBHydra2 support (deferred)"
  - Effort: Large
  - Impact: Medium (power user feature)

- [ ] **Deferred item: Mylar3 NZB settings import** (EPIC 10)
  - Original: "Mylar3 NZB settings import (deferred)"
  - Effort: Medium
  - Impact: High (migration convenience)

- [ ] **Deferred item: Activity integration for downloads** (EPIC 10)
  - Original: "Activity integration (deferred)"
  - Effort: Medium
  - Impact: High (user visibility into download progress)

### 14.2 NZBGet Integration (Sonarr/Radarr/Mylar3 Parity) ✅ COMPLETED
Full NZBGet support as an alternative to SABnzbd.

**Reference implementations:**
- Sonarr: `src/NzbDrone.Core/Download/Clients/Nzbget/`
- Radarr: `src/NzbDrone.Core/Download/Clients/Nzbget/`
- Mylar3: `mylar/nzbget.py`

- [x] **NZBGet client implementation** ✅
  - AC: Create `INzbgetClient` interface matching SABnzbd patterns ✅
  - AC: Implement NZBGet JSON-RPC API client ✅
  - AC: Authentication: username/password (not API key like SABnzbd) ✅
  - AC: Methods: append (add NZB), listgroups (get queue), history, editqueue ✅
  - Note: Full implementation in `src/Shortboxerr.Infrastructure/Nzb/NzbgetClient.cs`

- [x] **NZBGet provider** ✅
  - AC: Create `NzbgetDownloadProvider` implementing `IDownloadProvider` ✅
  - AC: Configuration: Host, Port, Username, Password, Category, Priority, Use SSL ✅
  - AC: Test connection validates API access and returns version ✅
  - AC: Health status monitoring ✅
  - Note: Full implementation in `src/Shortboxerr.Infrastructure/Providers/NzbgetDownloadProvider.cs`

- [x] **NZBGet download operations** ✅
  - AC: Add NZB by URL or file content ✅
  - AC: Set category (for post-processing organization) ✅
  - AC: Set priority (Very Low, Low, Normal, High, Very High, Force) ✅
  - AC: Monitor download progress (percentage, speed, ETA) ✅
  - AC: Detect completion via history API ✅
  - AC: Handle download failures (re-queue, retry logic) ✅

- [x] **NZBGet post-processing** ✅
  - AC: Detect post-processing completion (unpack, repair) ✅
  - AC: Handle post-processing failures gracefully ✅
  - AC: Trigger import on successful post-processing ✅
  - AC: Support NZBGet's `nzbToMedia` integration patterns ✅
  - Note: Status mapping handles PP_QUEUED, LOADING_PARS, VERIFYING, REPAIRING, UNPACKING, etc.

- [x] **NZBGet UI** ✅
  - AC: Add NZBGet to implementation dropdown in Download Client modal ✅
  - AC: Dynamic form fields for NZBGet configuration ✅
  - AC: Connection test with version display ✅
  - AC: Priority dropdown with NZBGet-specific values ✅
  - Note: Registered in ProviderFactory with full settings schema

- [x] **NZBGet tests** ✅
  - AC: Unit tests for NZBGet API client (mock responses) ✅
  - AC: Provider tests matching SABnzbd test coverage ✅
  - AC: Integration tests for add/status/remove operations ✅
  - Target: 20+ tests to match SABnzbd coverage ✅
  - Note: 75 unit tests in `tests/Shortboxerr.Tests/NzbgetClientTests.cs` covering:
    - TestConnection (4 tests)
    - Version (1 test)
    - AddNzb (5 tests)
    - Queue (4 tests)
    - History (3 tests)
    - Download Control (5 tests)
    - NZBGet-Specific (10 tests)
    - ClientType (1 test)
    - Status Mapping (22 tests)
    - GetDownloadStatus (2 tests)
    - Settings (12 tests)
    - Priority enum (6 tests)

### 14.3 Torrent Download Client Integration (Sonarr/Radarr Parity) - PARTIAL ✅
Support for torrent-based downloading via popular clients.

**Reference implementations:**
- Sonarr: `src/NzbDrone.Core/Download/Clients/QBittorrent/`
- Sonarr: `src/NzbDrone.Core/Download/Clients/Transmission/`
- Sonarr: `src/NzbDrone.Core/Download/Clients/Deluge/`

**Priority order (based on Sonarr/Radarr popularity):**
1. qBittorrent (most popular, excellent API) ✅
2. Transmission (lightweight, good API) - deferred
3. Deluge (feature-rich, daemon-based) - deferred

- [x] **Torrent client abstraction** ✅
  - AC: Create `ITorrentClient` interface ✅
  - AC: Methods: AddTorrent(url/magnet/file), GetStatus(hash), RemoveTorrent(hash), GetQueue() ✅
  - AC: Common model for torrent status (downloading, seeding, paused, completed) ✅
  - Note: `ITorrentClient` and `IQBittorrentClient` in `Shortboxerr.Core/Torrent/`

- [x] **qBittorrent integration** ✅
  - AC: Implement qBittorrent Web API v2 client ✅
  - AC: Authentication: username/password with session cookie ✅
  - AC: Add torrent by URL, magnet link, or .torrent file ✅
  - AC: Category assignment ✅
  - AC: Download path configuration ✅
  - AC: Monitor progress and completion ✅
  - AC: Handle ratio limits / seeding requirements ✅
  - Note: Full implementation in `QBittorrentClient.cs`

- [ ] **Transmission integration** (deferred)
  - AC: Implement Transmission RPC client
  - AC: Authentication: username/password
  - AC: Session ID handling
  - AC: Add torrent by URL or base64-encoded file
  - AC: Download directory configuration
  - AC: Monitor progress and completion

- [ ] **Deluge integration** (deferred)
  - AC: Implement Deluge JSON-RPC client
  - AC: Authentication: password-based
  - AC: Add torrent with label support
  - AC: Monitor progress and completion

- [ ] **Torrent → Import handoff** (deferred)
  - AC: Detect completed torrents
  - AC: Handle hardlinks vs copy based on configuration
  - AC: Respect seeding requirements (don't remove until ratio met)
  - AC: Support "move completed" scenarios

- [x] **Torrent UI** ✅ (qBittorrent only)
  - AC: Add qBittorrent to implementation dropdown ✅
  - AC: Dynamic form fields for qBittorrent configuration ✅
  - AC: Connection test with version display ✅
  - AC: Category/label configuration ✅
  - Note: Registered in ProviderFactory with full settings schema

- [x] **Torrent tests** ✅ (qBittorrent)
  - AC: Unit tests for qBittorrent API client ✅
  - AC: Provider tests for add/status/remove ✅
  - AC: Mock torrent completion scenarios ✅
  - Note: 69 unit tests in `QBittorrentClientTests.cs` covering:
    - TestConnection (3 tests)
    - Version (2 tests)
    - AddTorrent (6 tests)
    - GetTorrents (4 tests)
    - Download Control (6 tests)
    - qBittorrent-Specific (13 tests)
    - ClientType (1 test)
    - State Mapping (19 tests)
    - Hash Extraction (3 tests)
    - Settings (12 tests)

### 14.5 ReadComicOnline Parity with GetComics ✅ COMPLETED
Enable ReadComicOnline adapter with full feature parity to GetComics.org.

**Current State:**
- ✅ Both GetComics and ReadComicOnline are enabled by default
- ✅ Both adapters have full RSS feed support
- ✅ Multi-site search with priority-based fallback
- ✅ DDL Settings UI for site management

**Parity Status (Completed):**

| Feature | GetComics | ReadComicOnline | Notes |
|---------|-----------|-----------------|-------|
| SearchAsync | ✅ | ✅ | Both implemented |
| GetLatestAsync | ✅ | ✅ | Both implemented |
| GetRssFeedAsync | ✅ | ✅ | Both implemented |
| GetCategoryAsync | ✅ | ✅ | Both implemented |
| GetCategoryRssFeedAsync | ✅ | ✅ | Both implemented |
| GetPublisherAsync | ✅ | ✅ | Both implemented |
| GetPublisherRssFeedAsync | ✅ | ✅ | Both implemented |
| GetAvailableCategories | ✅ | ✅ | Both implemented |
| ExtractLinksAsync | ✅ | ✅ | Both implemented |
| DetectHomepageAsync | ❌ | ✅ | ReadComicOnline has domain detection |

- [x] **Enable ReadComicOnline in production** ✅
  - AC: ReadComicOnline adapter enabled by default (alongside GetComics) ✅
  - AC: DDL Settings UI allows enabling/disabling individual sites ✅
  - AC: Site priority configuration (which site to search first) ✅
  - AC: Fallback to second site if first fails ✅
  - Note: GetComics priority 1, ReadComicOnline priority 2

- [x] **Add RSS feed support to ReadComicOnline** ✅
  - AC: Implement `GetRssFeedAsync` matching GetComics pattern ✅
  - AC: Implement `GetCategoryRssFeedAsync` for category RSS feeds ✅
  - AC: Implement `GetPublisherRssFeedAsync` for publisher RSS feeds ✅
  - AC: Handle ReadComicOnline's RSS feed format (if available) ✅
  - AC: Fallback to HTML scraping if RSS not available ✅
  - Note: Methods try multiple RSS feed paths; gracefully fall back to HTML scraping
  - Note: 8 new unit tests added to ReadComicOnlineAdapterTests.cs

- [x] **DDL Settings UI for site management** ✅
  - AC: Settings > Indexers > DDL Sites section ✅
  - AC: Enable/disable individual DDL sites ✅
  - AC: Site priority shown per site ✅
  - AC: Rate limits displayed per site ✅
  - AC: Test connection button per site ✅
  - AC: Shows enabled/disabled status ✅
  - Note: API endpoints at /api/v1/ddl/sites/*

- [x] **Unit tests for multi-site management** ✅
  - AC: 13 unit tests in DdlSiteManagementTests.cs ✅
  - AC: Tests for enable/disable, priorities, site status ✅
  - AC: Tests for factory default configuration ✅

- [ ] **Site health monitoring** (deferred)
  - AC: Periodic health check for each enabled site
  - AC: Auto-disable site on repeated failures
  - AC: Alert user when site becomes unavailable
  - AC: Automatic re-enable after health check passes

### 14.4 Theme Accessibility & Color Scheme Audit ✅ COMPLETED
Ensure proper contrast and accessibility for both light and dark themes.

**Standards reference:**
- WCAG 2.1 Level AA contrast ratios (4.5:1 for normal text, 3:1 for large text)
- Material Design color guidelines
- Sonarr/Radarr theme patterns

- [x] **Dark theme audit** ✅
  - AC: Verify all text colors meet WCAG 2.1 AA contrast ratios against backgrounds ✅
  - AC: Check badge colors (success/warning/danger/info) are distinguishable ✅
  - AC: Verify form inputs have clear borders/focus states ✅
  - AC: Check disabled state colors are clearly "muted" but still readable ✅
  - AC: Verify link colors are distinguishable from regular text ✅
  - AC: Check table row hover/selected states are clearly visible ✅
  - AC: Verify modal overlays don't obscure content ✅
  - Note: Improved `--text-muted` from #6c7380 (4.5:1) to #8891a0 (5.2:1)
  - Note: Improved `--text-secondary` from #9ba1ab (6.5:1) to #b0b7c3 (8.0:1)
  - Note: Improved `--accent-danger` from #d9534f (4.9:1) to #e74c3c (5.1:1)

- [x] **Light theme audit** ✅
  - AC: Create/verify light theme color palette ✅
  - AC: Same accessibility checks as dark theme ✅
  - AC: Verify light theme doesn't feel "washed out" ✅
  - AC: Check that colored elements (badges, buttons) pop appropriately ✅
  - Note: All light theme colors now defined in CSS via [data-theme="light"]
  - Note: Accent colors adjusted for light backgrounds (darker variants)

- [x] **Color contrast fixes** ✅
  - AC: Document any failing contrast ratios with specific CSS variables ✅
  - AC: Propose color adjustments to meet WCAG AA ✅
  - AC: Implement fixes across all affected components ✅
  - AC: Test with browser accessibility tools (Lighthouse, axe) ✅ (manual verification)

- [x] **Color scheme documentation** ✅
  - AC: Document all CSS variables with purpose and usage ✅
  - AC: Create color palette reference (e.g., in Storybook or style guide) ✅
  - AC: Include accessibility notes for future development ✅
  - Note: Documentation in `ui/src/THEME.md`

- [x] **Theme toggle UX** ✅
  - AC: Verify theme switch is instantaneous (no flash) ✅
  - AC: Check system preference detection works correctly ✅
  - AC: Verify theme persists across page refreshes ✅
  - AC: Test theme in all pages (no unstyled components) ✅
  - Note: Simplified implementation using CSS data-theme attribute

- [ ] **Accessibility testing** (deferred - manual testing complete, automated deferred)
  - AC: Run Lighthouse accessibility audit on key pages (manual complete)
  - AC: Test with screen reader (VoiceOver/NVDA) - deferred
  - AC: Test with high contrast mode (Windows) - deferred
  - AC: Test keyboard navigation for all interactive elements - deferred

### 14.6 Mylar3 Search Settings Parity - PARTIAL ✅
Ensure full feature parity with Mylar3's search configuration options.

**Reference**: Mylar3 `config.ini` [General] and [DDL] sections

- [x] **Search provider configuration** ✅
  - AC: Enable/disable individual DDL providers (GetComics, ReadComicOnline, etc.) ✅
  - AC: Provider priority ordering (which to search first) ✅ (via 14.5)
  - Note: Provider-specific rate limits, timeouts, User-Agent deferred to provider settings

- [x] **Search behavior settings** ✅
  - AC: `search_delay` - Delay between searches to avoid rate limiting ✅
  - AC: `prefer_pack_releases` - Prefer pack/collection releases over singles ✅
  - AC: `search_tier_cutoff` - Number of providers to search before stopping ✅
  - AC: `max_results_per_provider` - Limit results per provider ✅

- [x] **Quality and format preferences** ✅
  - AC: `preferred_quality` - Preferred quality tier (Digital, Scan, Webrip) ✅
  - AC: `format_preference` - Preferred format ordering (CBZ > CBR > PDF) ✅
  - AC: `cbz_only` - Only accept CBZ format ✅

- [x] **Size limits** ✅
  - AC: `minsize` / `maxsize` - Size limits for singles (MB) ✅
  - AC: `minsize_pack` / `maxsize_pack` - Size limits for packs (MB) ✅

- [x] **Naming and filtering** ✅
  - AC: `blacklist_words` - Words that disqualify releases ✅
  - AC: `whitelist_words` - Required words for releases ✅
  - AC: `ignore_words` - Words to strip from release names ✅
  - AC: `enable_ddl_search` / `enable_nzb_search` / `enable_torrent_search` ✅

- [x] **Search automation** ✅
  - AC: `auto_search` - Automatically search for missing issues ✅
  - AC: `auto_search_interval` - Auto-search interval (hours) ✅
  - AC: `search_new_series_on_add` - Search when adding series ✅
  - AC: `stale_search_threshold` - Re-search threshold (days) ✅

- [x] **Search UI** ✅
  - AC: Settings > Search page with all options ✅
  - AC: Group settings by category ✅
  - AC: Inline help text ✅
  - AC: "Reset to Defaults" button ✅
  - Note: Mylar3 config import deferred

- [x] **Unit tests** ✅
  - AC: 20 unit tests for SearchSettingsService ✅
  - AC: Test settings persistence ✅
  - AC: Test validation ✅
  - AC: Test default values ✅

- [ ] **Deferred items**
  - AC: Provider-specific timeout settings
  - AC: Provider-specific User-Agent configuration
  - AC: Import from Mylar3 config.ini
  - AC: `search_32p` / `search_delay_32p` (32pag.es integration)
  - AC: `ignore_havetotal` option

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