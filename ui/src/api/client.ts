// In development, use empty string so Vite proxy handles /api requests
// In production, the UI is served from the same origin as the API
const API_BASE = import.meta.env.VITE_API_URL ?? '';

// API response format for paged results (matches backend PagedResult<T>)
interface ApiPagedResult<T> {
  records: T[];
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
}

// UI-friendly format used by components
interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

// Helper to convert API response to UI format
function toPagedResult<T>(response: ApiPagedResult<T>): PagedResult<T> {
  return {
    items: response.records,
    page: response.page,
    pageSize: response.pageSize,
    totalCount: response.totalRecords,
    totalPages: response.totalPages,
  };
}

interface SystemStatus {
  version: string;
  seriesCount: number;
  collectionsCount: number;
  issuesCount: number;
  filesCount: number;
  databaseStatus: string;
  indexerStatus: string;
  enabledIndexers: number;
  queuedDownloads: number;
}

export interface LogFile {
  fileName: string;
  filePath: string;
  sizeBytes: number;
  sizeFormatted: string;
  lastModified: string;
  created: string;
}

export interface LogLine {
  raw: string;
  timestamp?: string;
  level?: string;
  category?: string;
  message?: string;
}

export interface LogContent {
  fileName: string;
  totalLines: number;
  filteredLines: number;
  returnedLines: number;
  lines: LogLine[];
}

interface Activity {
  id: string;
  title: string;
  type: 'success' | 'warning' | 'info' | 'danger';
  timestamp: string;
}

// API response format for Series list
interface ApiSeries {
  id: number;
  title: string;
  sortTitle: string | null;
  publisher: string | null;
  startYear: number | null;
  endYear: number | null;
  status: number; // Enum: 0=Continuing, 1=Ended
  path: string | null;
  overview: string | null;
  monitored: boolean;
  issueCount: number;
  issueFileCount: number;
  editionCount: number;
  comicVineId: number | null;
  coverImageUrl: string | null;
}

// UI-friendly format for Series
export interface Series {
  id: number;
  title: string;
  year: number | null;
  startYear?: number | null;
  publisher: string | null;
  status: string;
  issueCount: number;
  filesCount: number;
  coverImageUrl: string | null;
}

// Filter options for series list
export interface SeriesFilterOptions {
  statuses: { value: number; label: string; count: number }[];
  publishers: { value: string; label: string; count: number }[];
  sortOptions: { value: string; label: string }[];
  totalSeries: number;
}

// Map API series to UI series
function toSeries(api: ApiSeries): Series {
  // Handle both string (new JSON serialization) and number (legacy) status values
  const statusMap: Record<number, string> = {
    0: 'Continuing',
    1: 'Ended',
  };
  const status = typeof api.status === 'string' 
    ? api.status 
    : (statusMap[api.status] ?? 'Unknown');
  return {
    id: api.id,
    title: api.title,
    year: api.startYear,
    publisher: api.publisher,
    status,
    issueCount: api.issueCount,
    filesCount: api.issueFileCount,
    coverImageUrl: api.coverImageUrl,
  };
}

// API response format for detailed series
interface ApiSeriesDetail {
  id: number;
  title: string;
  sortTitle: string | null;
  publisher: string | null;
  startYear: number | null;
  endYear: number | null;
  status: number; // Enum: 0=Continuing, 1=Ended
  path: string | null;
  overview: string | null;
  monitored: boolean;
  issueCount: number;
  issueFileCount: number;
  editionCount: number;
  createdAt: string;
  updatedAt: string | null;
  comicVineId: number | null;
  coverImageUrl: string | null;
  comicVineUrl: string | null;
  totalIssueCount: number | null;
  metadataLastRefreshed: string | null;
}

// UI-friendly format for detailed series
export interface SeriesDetail {
  id: number;
  title: string;
  sortTitle: string | null;
  publisher: string | null;
  startYear: number | null;
  endYear: number | null;
  status: string;
  path: string | null;
  overview: string | null;
  monitored: boolean;
  issueCount: number;
  issueFileCount: number;
  editionCount: number;
  createdAt: string;
  updatedAt: string | null;
  // ComicVine metadata
  comicVineId: number | null;
  coverImageUrl: string | null;
  comicVineUrl: string | null;
  totalIssueCount: number | null;
  metadataLastRefreshed: string | null;
}

// Map API series detail to UI series detail
function toSeriesDetail(api: ApiSeriesDetail): SeriesDetail {
  // Handle both string (new JSON serialization) and number (legacy) status values
  const statusMap: Record<number, string> = {
    0: 'Continuing',
    1: 'Ended',
  };
  const status = typeof api.status === 'string' 
    ? api.status 
    : (statusMap[api.status] ?? 'Unknown');
  return {
    ...api,
    status,
  };
}

export interface Issue {
  id: number;
  seriesId: number;
  issueNumber: number;
  issueNumberText: string | null;
  title: string | null;
  releaseDate: string | null;
  storeDate: string | null;
  coverDate: string | null;
  overview: string | null;
  monitored: boolean;
  hasFile: boolean;
  satisfiedByEdition: boolean;
  status: IssueStatus;
  createdAt: string;
  updatedAt: string | null;
  // ComicVine metadata
  comicVineId: number | null;
  coverImageUrl: string | null;
  comicVineUrl: string | null;
  metadataLastRefreshed: string | null;
  // Special issue flags
  isAnnual: boolean;
  isSpecial: boolean;
  specialType: string | null;
  storyArcs: string[];
  // Computed
  displayNumber: string;
}

// Pull List types
export type IssueStatus = 'Wanted' | 'Owned' | 'Downloading' | 'Skipped' | 'Missing' | 'Staged';

export interface PullListIssue {
  issueId: number;
  seriesId: number;
  seriesTitle: string;
  publisher: string | null;
  issueNumber: number;
  issueNumberText: string | null;
  issueTitle: string | null;
  storeDate: string | null;
  coverDate: string | null;
  coverImageUrl: string | null;
  status: IssueStatus;
  isAnnual: boolean;
  isSpecial: boolean;
  specialType: string | null;
}

export interface WeeklyPullList {
  weekStart: string;
  weekEnd: string;
  releaseDay: string;
  issues: PullListIssue[];
  totalCount: number;
  wantedCount: number;
  ownedCount: number;
  skippedCount: number;
}

export interface CalendarDay {
  date: string;
  isReleaseDay: boolean;
  issues: PullListIssue[];
}

export interface ReleaseCalendar {
  startDate: string;
  endDate: string;
  days: CalendarDay[];
  byPublisher: Record<string, PullListIssue[]>;
  bySeries: Record<number, PullListIssue[]>;
}

export interface PullListFilter {
  seriesIds?: number[];
  publishers?: string[];
  statuses?: IssueStatus[];
  monitoredOnly?: boolean;
  includeAnnuals?: boolean;
  includeSpecials?: boolean;
}

export interface PullListActionResult {
  success: boolean;
  error?: string;
  issueId?: number;
  newStatus?: IssueStatus;
}

export interface PullListBulkResult {
  success: boolean;
  error?: string;
  totalProcessed: number;
  successCount: number;
  failedCount: number;
  failedIssueIds: number[];
}

export interface PullListStats {
  totalMonitoredSeries: number;
  totalWantedIssues: number;
  totalOwnedIssues: number;
  totalSkippedIssues: number;
  releasingThisWeek: number;
  releasingNextWeek: number;
  missedIssues: number;
  wantedByPublisher: Record<string, number>;
}

export type PullListSuggestedActionType = 
  | 'None' 
  | 'ConfigureApiKey' 
  | 'AddSeries' 
  | 'MatchSeries' 
  | 'TryAllReleases';

export interface PullListConfigStatus {
  isComicVineConfigured: boolean;
  totalSeriesCount: number;
  matchedSeriesCount: number;
  monitoredSeriesCount: number;
  discoveryCacheLastRefreshed: string | null;
  hasReleasesThisWeek: boolean;
  suggestedAction: string | null;
  actionType: PullListSuggestedActionType;
}

export interface PullListSettingsDto {
  weekStartDay: number;
  releaseDay: number;
  defaultMonitoringMode: number;
  searchDelayHours: number;
  autoAddToWanted: boolean;
  includeAnnualsInAutoAdd: boolean;
  includeSpecialsInAutoAdd: boolean;
  skipVariantCovers: boolean;
  upcomingWeeksToShow: number;
  pastWeeksToShow: number;
  // Weekly Export Settings (Mylar3 Parity)
  exportWeeklyPullList: boolean;
  weeklyExportDirectory?: string | null;
  weeklyExportFormat: WeeklyExportFormat;
  autoExportOnReleaseDay: boolean;
  exportFields?: string[] | null;
}

export type WeeklyExportFormat = 'Json' | 'Text' | 'Csv';

export interface WeeklyExportResult {
  success: boolean;
  error?: string | null;
  exportDirectory?: string | null;
  exportFilePath?: string | null;
  format: WeeklyExportFormat;
  year: number;
  weekNumber: number;
  releaseDay: string;
  totalIssues: number;
  wantedIssues: number;
  ownedIssues: number;
  exportedAt: string;
}

export interface WeeklyExportInfo {
  year: number;
  weekNumber: number;
  releaseDay: string;
  directoryPath: string;
  filePath: string;
  format: WeeklyExportFormat;
  exportedAt: string;
  fileSizeBytes: number;
  issueCount: number;
}

export interface SeriesPullListSettingsDto {
  seriesId: number;
  monitoringModeOverride?: string | null;
  includeAnnuals?: boolean | null;
  includeSpecials?: boolean | null;
  skipVariants?: boolean | null;
  searchPriority?: number;
}

// Search Settings
export interface SearchSettings {
  // Search Behavior
  searchDelaySeconds: number;
  preferPackReleases: boolean;
  searchTierCutoff: number;
  maxResultsPerProvider: number;
  
  // Quality Preferences
  preferredQuality: PreferredQuality;
  formatPreference: string[];
  cbzOnly: boolean;
  
  // Size Limits
  minSizeMb: number;
  maxSizeMb: number;
  minSizePackMb: number;
  maxSizePackMb: number;
  
  // Filtering
  blacklistWords: string[];
  whitelistWords: string[];
  ignoreWords: string[];
  
  // Provider Toggles
  enableDdlSearch: boolean;
  enableNzbSearch: boolean;
  enableTorrentSearch: boolean;
  
  // Automation
  autoSearchEnabled: boolean;
  autoSearchIntervalHours: number;
  searchNewSeriesOnAdd: boolean;
  staleSearchThresholdDays: number;
}

// PreferredQuality values (matches backend enum)
export type PreferredQuality = 0 | 1 | 2 | 3;
export const PreferredQuality = {
  Any: 0 as const,
  Digital: 1 as const,
  Webrip: 2 as const,
  Scan: 3 as const,
};

// Discovery types for Mylar3 "This Week" parity
export interface WeeklyDiscoveryList {
  weekStart: string;
  weekEnd: string;
  releaseDay: string;
  issues: DiscoverableIssue[];
  totalCount: number;
  inLibraryCount: number;
  newCount: number;
}

export interface DiscoverableIssue {
  comicVineIssueId: number;
  comicVineVolumeId: number;
  seriesTitle: string;
  publisher: string | null;
  startYear: number | null;
  issueNumber: number;
  issueNumberText: string | null;
  issueTitle: string | null;
  storeDate: string | null;
  coverDate: string | null;
  coverImageUrl: string | null;
  isInLibrary: boolean;
  localSeriesId: number | null;
  localIssueId: number | null;
  status: IssueStatus | null;
  isSeriesMonitored: boolean;
}

export interface DiscoveryFilter {
  publishers?: string[];
  inLibraryOnly?: boolean;
  newOnly?: boolean;
  includeAnnuals?: boolean;
  includeSpecials?: boolean;
}

export interface AddOneOffResult {
  success: boolean;
  error?: string;
  issueId?: number;
  seriesId?: number;
  seriesTitle?: string;
  issueNumber?: number;
  seriesCreated?: boolean;
}

export interface AddFromDiscoveryResult {
  success: boolean;
  error?: string;
  seriesId?: number;
  seriesTitle?: string;
  issuesCreated?: number;
  markedWantedIssueId?: number;
  alreadyExists?: boolean;
}

export type SeriesMonitoringMode = 'AllIssues' | 'FutureIssues' | 'Manual' | 'FirstIssue' | 'None';

interface Edition {
  id: number;
  title: string;
  seriesId: number | null;
  seriesTitle: string | null;
  editionType: string;
  volumeNumber: number | null;
  isbn: string | null;
  publisher: string | null;
  releaseDate: string | null;
  pageCount: number | null;
  overview: string | null;
  coverImageUrl: string | null;
  comicVineId: number | null;
  comicVineUrl: string | null;
  monitored: boolean;
  hasFile: boolean;
  contentCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface EditionDetail extends Edition {
  contents: EditionContent[];
}

export interface EditionContent {
  id: number;
  editionTitleId: number;
  issueId: number | null;
  seriesId: number | null;
  seriesTitle: string | null;
  issueNumber: number | null;
  issueNumberText: string | null;
  issueTitle: string | null;
  issueCoverImageUrl: string | null;
  issueHasFile: boolean;
  sortOrder: number;
}

interface QueueItem {
  id: string;
  title: string;
  series: string;
  status: 'downloading' | 'queued' | 'paused' | 'failed';
  progress: number;
  size: string;
  timeRemaining: string | null;
  provider: string;
}

interface WantedItem {
  id: number;
  type: 'issue' | 'collection';
  title: string;
  series: string;
  seriesId?: number;
  issueNumber?: number;
  issueNumberText?: string;
  volumeNumber?: number;
  editionType?: string;
  coverImageUrl?: string;
  comicVineId?: number;
  comicVineUrl?: string;
  dateAdded: string;
}

interface HistoryEvent {
  id: number;
  type: 'grabbed' | 'imported' | 'deleted' | 'failed' | 'renamed';
  title: string;
  series: string;
  details: string;
  timestamp: string;
  source: string | null;
}

interface StagedFile {
  id: string;
  filename: string;
  path: string;
  size: string;
  parsed: {
    series: string | null;
    issue: number | null;
    year: number | null;
    format: string | null;
    isCollection: boolean;
    editionType: string | null;
    confidence: number;
  };
  match: {
    seriesId: number | null;
    seriesTitle: string | null;
    issueId: number | null;
    editionId: number | null;
    confidence: number;
  } | null;
  status: 'pending' | 'matched' | 'unmatched' | 'error';
}

export interface Provider {
  id: number;
  name: string;
  implementation: string;
  category: 'Indexer' | 'DownloadClient';
  type: string;
  isEnabled: boolean;
  priority: number;
  baseUrl: string | null;
  apiKey: string | null;
  username: string | null;
  settings: string | null;
  tags: string | null;
  lastHealthStatus: 'Unknown' | 'Healthy' | 'Unhealthy' | 'Warning';
  lastHealthCheck: string | null;
  lastError: string | null;
  failureCount: number;
}

// === NZB Types ===

export interface NzbIndexer {
  id: string;
  name: string;
  baseUrl: string;
  apiKey: string;
  enabled: boolean;
  priority: number;
  categories: number[];
}

export interface NzbIndexerRequest {
  name?: string;
  baseUrl?: string;
  apiKey?: string;
  enabled?: boolean;
  priority?: number;
  categories?: number[];
}

export interface NzbIndexersResponse {
  indexers: NzbIndexer[];
  totalCount: number;
  enabledCount: number;
}

export interface NzbTestResult {
  success: boolean;
  message?: string;
  capabilities?: {
    supportsSearch: boolean;
    supportsBookSearch: boolean;
  };
}

export interface NzbClientTestResult {
  success: boolean;
  message?: string;
  version?: string;
}

export interface NzbIndexerPreset {
  id: string;
  name: string;
  baseUrl: string;
  defaultCategories: number[];
}

export interface NzbIndexerPresetsResponse {
  presets: NzbIndexerPreset[];
}

export interface NzbDownloadClientResponse {
  clientType: 'SABnzbd' | 'NZBGet';
  sabnzbd?: SabnzbdSettings;
  isConfigured: boolean;
}

export interface SabnzbdSettings {
  host: string;
  apiKey: string;
  category?: string;
  priority?: number;
  useSsl?: boolean;
}

export interface NzbDownloadClientRequest {
  clientType?: 'SABnzbd' | 'NZBGet';
  sabnzbd?: SabnzbdSettings;
}

export interface NzbRelease {
  guid: string;
  title: string;
  nzbUrl: string;
  size: number;
  publishedDate: string;
  categoryId: number;
  group?: string;
  poster?: string;
  comments: number;
  quality?: string;
  detailsUrl?: string;
}

export interface NzbSearchResponse {
  releases: NzbRelease[];
  totalResults: number;
  indexersSearched: number;
  indexersSuccessful: number;
  durationMs: number;
  indexerResults: Array<{
    indexerId: string;
    indexerName: string;
    success: boolean;
    releaseCount: number;
    durationMs: number;
  }>;
}

// Webhook Notification Provider Types
export type NotificationEventType = 
  | 'Test' 
  | 'NewRelease' 
  | 'Grabbed' 
  | 'Imported' 
  | 'WeeklySummary' 
  | 'DownloadFailed' 
  | 'SeriesAdded' 
  | 'Health' 
  | 'Update';

export interface WebhookProviderSettings {
  id: string;
  name: string;
  providerType: 'Webhook';
  enabled: boolean;
  onEvents: NotificationEventType[];
  includeSeries: boolean;
  includeImages: boolean;
  webhookUrl: string;
  method: string;
  contentType: string;
  username?: string;
  password?: string;
  headers?: Record<string, string>;
}

export interface WebhookProviderRequest {
  name: string;
  enabled?: boolean;
  onEvents?: NotificationEventType[];
  includeSeries?: boolean;
  includeImages?: boolean;
  webhookUrl: string;
  method?: string;
  contentType?: string;
  username?: string;
  password?: string;
  headers?: Record<string, string>;
}

export interface WebhookTestResult {
  success: boolean;
  message: string;
  latency?: string;
}

export interface ProviderImplementation {
  name: string;
  displayName: string;
  description: string;
  category: string;
  type: string;
  requiresBaseUrl: boolean;
  requiresApiKey: boolean;
  requiresCredentials: boolean;
  settingsSchema: string | null;
}

export interface ProviderTestResult {
  success: boolean;
  message: string;
  errors: string[];
  latencyMs: number;
}

export interface CreateProviderRequest {
  name: string;
  implementation: string;
  isEnabled: boolean;
  baseUrl?: string;
  apiKey?: string;
  username?: string;
  password?: string;
  settings?: string;
  tags?: string;
}

export interface UiSettings {
  theme: 'dark' | 'light' | 'system';
  pageSize: number;
  showFileSizes: boolean;
  relativeTimestamps: boolean;
  issueViewMode: 'cover' | 'list';
  pullListDisplayMode: 'list' | 'grid';
}

export interface GeneralSettings {
  seriesFolderFormat: string;
  issueFileFormat: string;
  collectionFileFormat: string;
  comicLibraryPath: string;
  downloadFolder: string;
  stagingFolder: string;
  autoMoveToStaging: boolean;
}

export interface FolderSettings {
  comicLibraryPath: string;
  downloadFolder: string;
  stagingFolder: string;
  autoMoveToStaging: boolean;
}

export interface LoggingSettings {
  logLevel: string;
  logPath: string;
  maxFileSizeMb: number;
  rotationFileCount: number;
  consoleLoggingEnabled: boolean;
  sqlQueryLogging: boolean;
  httpRequestBodyLogging: boolean;
  fullStackTraces: boolean;
  retentionDays: number;
}

export interface NamingToken {
  token: string;
  description: string;
  example: string;
}

export interface NamingTokensResponse {
  seriesFolderTokens: NamingToken[];
  issueFileTokens: NamingToken[];
  collectionFileTokens: NamingToken[];
}

export interface ApiKeyInfo {
  isEnabled: boolean;
  maskedKey: string;
  fullKey: string | null;
  createdAt: string;
  lastUsedAt: string | null;
  isNewKey?: boolean;
}

// ComicVine Types
export interface ComicVineSettings {
  enabled: boolean;
  hasApiKey: boolean;
  maskedApiKey: string | null;
  cacheTtlHours: number;
  coverCacheDirectory: string;
  autoMatchThreshold: number;
  autoRefreshEnabled: boolean;
  refreshIntervalDays: number;
}

export interface ComicVineSettingsUpdate {
  apiKey?: string;
  enabled?: boolean;
  cacheTtlHours?: number;
  coverCacheDirectory?: string;
  autoMatchThreshold?: number;
  autoRefreshEnabled?: boolean;
  refreshIntervalDays?: number;
}

export interface ComicVineTestResult {
  success: boolean;
  message: string;
  latencyMs: number | null;
  apiVersion: string | null;
}

export interface ComicVineRateLimitStatus {
  requestsUsed: number;
  requestLimit: number;
  windowResetTime: string;
  isRateLimited: boolean;
  timeUntilReset: string;
}

export interface ComicVineSearchResult<T> {
  success: boolean;
  error: string | null;
  statusCode: number;
  results: T[];
  totalResults: number;
  page: number;
  limit: number;
  numberOfPageResults: number;
}

export interface ComicVineResult<T> {
  success: boolean;
  error: string | null;
  statusCode: number;
  data: T | null;
}

export interface ComicVineVolume {
  id: number;
  name: string;
  aliases: string[];
  startYear: number | null;
  description: string | null;
  deck: string | null;
  publisher: { id: number; name: string } | null;
  issueCount: number;
  image: ComicVineImage | null;
  siteDetailUrl: string | null;
}

export interface ComicVineIssue {
  id: number;
  name: string | null;
  issueNumber: string;
  description: string | null;
  coverDate: string | null;
  storeDate: string | null;
  volume: { id: number; name: string } | null;
  image: ComicVineImage | null;
  siteDetailUrl: string | null;
}

export interface ComicVineImage {
  iconUrl: string | null;
  mediumUrl: string | null;
  screenUrl: string | null;
  originalUrl: string | null;
}

// Series Metadata Types (for adding/matching series via ComicVine)
export interface SeriesMatchCandidate {
  comicVineId: number;
  title: string;
  aliases: string[];
  publisher: string | null;
  startYear: number | null;
  description: string | null;
  coverImageUrl: string | null;
  issueCount: number;
  confidence: number;
  confidenceReasons: string[];
  siteDetailUrl: string | null;
}

export interface SeriesSearchResult {
  success: boolean;
  error: string | null;
  results: SeriesMatchCandidate[];
  totalResults: number;
  page: number;
  limit: number;
}

export interface SeriesAddResult {
  success: boolean;
  error: string | null;
  seriesId: number | null;
  comicVineId: number | null;
  title: string | null;
  issuesCreated: number;
  alreadyExists: boolean;
  existingSeriesId: number | null;
}

export interface AddSeriesFromComicVineRequest {
  rootFolder?: string;
  monitored?: boolean;
  monitoringMode?: 'AllIssues' | 'FutureIssues' | 'Manual' | 'FirstIssue';
}

async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${endpoint}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      'Cache-Control': 'no-cache',
      ...options?.headers,
    },
  });

  if (!response.ok) {
    throw new Error(`API error: ${response.status} ${response.statusText}`);
  }

  return response.json();
}

export const api = {
  // System
  getSystemStatus: async (): Promise<SystemStatus> => {
    try {
      const status = await fetchApi<any>('/api/v1/system/status');
      return {
        version: status.version ?? '1.0.0',
        seriesCount: status.seriesCount ?? 0,
        collectionsCount: status.collectionsCount ?? 0,
        issuesCount: status.issuesCount ?? 0,
        filesCount: status.filesCount ?? 0,
        databaseStatus: status.databaseStatus ?? 'Connected',
        indexerStatus: status.indexerStatus ?? 'healthy',
        enabledIndexers: status.enabledIndexers ?? 0,
        queuedDownloads: status.queuedDownloads ?? 0,
      };
    } catch {
      // Return defaults if API is not available
      return {
        version: '1.0.0',
        seriesCount: 0,
        collectionsCount: 0,
        issuesCount: 0,
        filesCount: 0,
        databaseStatus: 'Connected',
        indexerStatus: 'healthy',
        enabledIndexers: 0,
        queuedDownloads: 0,
      };
    }
  },

  getRecentActivity: async (limit: number): Promise<Activity[]> => {
    try {
      const response = await fetchApi<any[]>(`/api/v1/history?pageSize=${limit}`);
      return response.map((e: any) => ({
        id: String(e.id),
        title: e.description ?? e.sourceTitle ?? 'Unknown event',
        type: mapEventType(e.eventType),
        timestamp: formatTimestamp(e.timestamp),
      }));
    } catch {
      return [];
    }
  },

  // Logs
  getLogFiles: async (): Promise<LogFile[]> => {
    try {
      const response = await fetchApi<{ logDirectory: string; files: any[] }>('/api/v1/system/logs');
      return response.files.map((f: any) => ({
        fileName: f.fileName,
        filePath: f.filePath,
        sizeBytes: f.sizeBytes,
        sizeFormatted: f.sizeFormatted,
        lastModified: f.lastModified,
        created: f.created,
      }));
    } catch {
      return [];
    }
  },

  getLogContent: async (filename: string, lines = 500, level?: string, search?: string): Promise<LogContent> => {
    const params = new URLSearchParams();
    params.set('lines', String(lines));
    if (level) params.set('level', level);
    if (search) params.set('search', search);
    
    try {
      const response = await fetchApi<LogContent>(`/api/v1/system/logs/${encodeURIComponent(filename)}?${params}`);
      return response;
    } catch {
      return { fileName: filename, totalLines: 0, filteredLines: 0, returnedLines: 0, lines: [] };
    }
  },

  getRecentLogs: async (lines = 100, level?: string, search?: string): Promise<LogContent> => {
    const params = new URLSearchParams();
    params.set('lines', String(lines));
    if (level) params.set('level', level);
    if (search) params.set('search', search);
    
    try {
      const response = await fetchApi<LogContent>(`/api/v1/system/logs/recent?${params}`);
      return response;
    } catch {
      return { fileName: 'recent', totalLines: 0, filteredLines: 0, returnedLines: 0, lines: [] };
    }
  },

  deleteLogFile: async (filename: string): Promise<void> => {
    await fetchApi(`/api/v1/system/logs/${encodeURIComponent(filename)}`, { method: 'DELETE' });
  },

  // Series
  getSeries: async (params: { 
    search?: string; 
    page?: number; 
    pageSize?: number;
    sortKey?: string;
    sortDir?: string;
    status?: string;
    publisher?: string;
    monitored?: boolean;
  }): Promise<PagedResult<Series>> => {
    const query = new URLSearchParams();
    if (params.search) query.set('search', params.search);
    if (params.page) query.set('page', String(params.page));
    if (params.pageSize) query.set('pageSize', String(params.pageSize));
    if (params.sortKey) query.set('sortKey', params.sortKey);
    if (params.sortDir) query.set('sortDir', params.sortDir);
    if (params.status) query.set('status', params.status);
    if (params.publisher) query.set('publisher', params.publisher);
    if (params.monitored !== undefined) query.set('monitored', String(params.monitored));

    try {
      const response = await fetchApi<ApiPagedResult<ApiSeries>>(`/api/v1/series?${query}`);
      return {
        items: response.records.map(toSeries),
        page: response.page,
        pageSize: response.pageSize,
        totalCount: response.totalRecords,
        totalPages: response.totalPages,
      };
    } catch {
      return { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 };
    }
  },

  getSeriesFilterOptions: async (): Promise<SeriesFilterOptions> => {
    return fetchApi<SeriesFilterOptions>('/api/v1/series/filter-options');
  },

  deleteSeries: async (id: number): Promise<void> => {
    await fetchApi(`/api/v1/series/${id}`, { method: 'DELETE' });
  },

  getSeriesById: async (id: number): Promise<SeriesDetail | null> => {
    try {
      const response = await fetchApi<ApiSeriesDetail>(`/api/v1/series/${id}`);
      return toSeriesDetail(response);
    } catch {
      return null;
    }
  },

  getSeriesIssues: async (
    seriesId: number,
    params?: { page?: number; pageSize?: number; sortKey?: string; sortDir?: string }
  ): Promise<PagedResult<Issue>> => {
    const query = new URLSearchParams();
    if (params?.page) query.set('page', String(params.page));
    if (params?.pageSize) query.set('pageSize', String(params.pageSize));
    if (params?.sortKey) query.set('sortKey', params.sortKey);
    if (params?.sortDir) query.set('sortDir', params.sortDir);

    try {
      const response = await fetchApi<ApiPagedResult<Issue>>(`/api/v1/series/${seriesId}/issues?${query}`);
      return toPagedResult(response);
    } catch (error) {
      console.error('Failed to fetch series issues:', error);
      return { items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 };
    }
  },

  // Editions (Collections)
  getEditions: async (params: { search?: string; page?: number; pageSize?: number }): Promise<PagedResult<Edition>> => {
    const query = new URLSearchParams();
    if (params.search) query.set('search', params.search);
    if (params.page) query.set('page', String(params.page));
    if (params.pageSize) query.set('pageSize', String(params.pageSize));

    try {
      const response = await fetchApi<ApiPagedResult<Edition>>(`/api/v1/editions?${query}`);
      return toPagedResult(response);
    } catch {
      return { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 };
    }
  },

  deleteEdition: async (id: number): Promise<void> => {
    await fetchApi(`/api/v1/editions/${id}`, { method: 'DELETE' });
  },

  getEditionById: async (id: number): Promise<Edition> => {
    return await fetchApi<Edition>(`/api/v1/editions/${id}`);
  },

  getEditionDetail: async (id: number): Promise<EditionDetail> => {
    return await fetchApi<EditionDetail>(`/api/v1/editions/${id}/detail`);
  },

  getEditionContents: async (id: number): Promise<EditionContent[]> => {
    return await fetchApi<EditionContent[]>(`/api/v1/editions/${id}/contents`);
  },

  // Activity
  getActivityQueue: async (): Promise<QueueItem[]> => {
    // This would connect to a real queue endpoint
    // For now, return empty as there's no queue implementation
    return [];
  },

  // Wanted
  getWanted: async (params: { type: string; search?: string }): Promise<PagedResult<WantedItem>> => {
    const query = new URLSearchParams();
    if (params.search) query.set('search', params.search);
    
    const endpoint = params.type === 'collections' 
      ? '/api/v1/wanted/collections' 
      : '/api/v1/wanted/issues';
    
    try {
      const response = await fetchApi<{
        items: any[];
        page: number;
        pageSize: number;
        totalCount: number;
        totalPages: number;
      }>(`${endpoint}?${query}`);
      
      return {
        items: response.items.map((item: any) => ({
          id: item.id,
          type: params.type === 'collections' ? 'collection' : 'issue',
          title: item.title,
          series: item.series,
          seriesId: item.seriesId,
          issueNumber: item.issueNumber,
          issueNumberText: item.issueNumberText,
          volumeNumber: item.volumeNumber,
          editionType: item.editionType,
          coverImageUrl: item.coverImageUrl,
          comicVineId: item.comicVineId,
          comicVineUrl: item.comicVineUrl,
          dateAdded: item.dateAdded ? new Date(item.dateAdded).toLocaleDateString() : 'Unknown',
        })),
        page: response.page,
        pageSize: response.pageSize,
        totalCount: response.totalCount,
        totalPages: response.totalPages,
      };
    } catch {
      return { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 };
    }
  },

  getWantedCount: async (): Promise<{ issues: number; collections: number; total: number }> => {
    try {
      const response = await fetchApi<{ Issues: number; Collections: number; Total: number }>('/api/v1/wanted/count');
      return {
        issues: response.Issues,
        collections: response.Collections,
        total: response.Total,
      };
    } catch {
      return { issues: 0, collections: 0, total: 0 };
    }
  },

  // History
  getHistory: async (params: { type?: string; search?: string }): Promise<PagedResult<HistoryEvent>> => {
    const query = new URLSearchParams();
    if (params.type && params.type !== 'all') query.set('type', params.type);
    if (params.search) query.set('search', params.search);

    try {
      const response = await fetchApi<any[]>(`/api/v1/history?${query}`);
      return {
        items: response.map((e: any) => ({
          id: e.id,
          type: mapHistoryType(e.eventType),
          title: e.sourceTitle ?? 'Unknown',
          series: e.description ?? '',
          details: e.data ?? '',
          timestamp: formatTimestamp(e.timestamp),
          source: e.downloadClient ?? null,
        })),
        page: 1,
        pageSize: 50,
        totalCount: response.length,
        totalPages: 1,
      };
    } catch {
      return { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 };
    }
  },

  // Staged Files
  getStagedFiles: async (): Promise<PagedResult<StagedFile>> => {
    try {
      const response = await fetchApi<any[]>('/api/v1/manualimport/staged');
      return {
        items: response.map((f: any) => ({
          id: f.path,
          filename: f.filename,
          path: f.path,
          size: formatSize(f.size),
          parsed: {
            series: f.parsedInfo?.seriesTitle ?? null,
            issue: f.parsedInfo?.issueNumber ?? null,
            year: f.parsedInfo?.year ?? null,
            format: f.parsedInfo?.format ?? null,
            isCollection: f.parsedInfo?.isCollection ?? false,
            editionType: f.parsedInfo?.editionType ?? null,
            confidence: f.parsedInfo?.confidence ?? 0,
          },
          match: f.suggestedSeries ? {
            seriesId: f.suggestedSeries.id,
            seriesTitle: f.suggestedSeries.title,
            issueId: null,
            editionId: null,
            confidence: f.matchConfidence ?? 0,
          } : null,
          status: f.suggestedSeries ? 'matched' : 'unmatched',
        })),
        page: 1,
        pageSize: 100,
        totalCount: response.length,
        totalPages: 1,
      };
    } catch {
      return { items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 };
    }
  },

  importFiles: async (ids: string[]): Promise<void> => {
    await fetchApi('/api/v1/manualimport/import', {
      method: 'POST',
      body: JSON.stringify({ files: ids }),
    });
  },

  rejectStagedFile: async (path: string, reason?: string): Promise<void> => {
    await fetchApi('/api/v1/manualimport/reject', {
      method: 'POST',
      body: JSON.stringify({ sourcePath: path, reason }),
    });
  },

  updateStagedMatch: async (path: string, seriesId: number | null, issueId: number | null, editionId: number | null): Promise<void> => {
    await fetchApi('/api/v1/manualimport/update-match', {
      method: 'POST',
      body: JSON.stringify({ sourcePath: path, seriesId, issueId, editionId }),
    });
  },

  // Providers
  getIndexers: async (): Promise<Provider[]> => {
    try {
      return await fetchApi<Provider[]>('/api/v1/providers/indexers');
    } catch {
      return [];
    }
  },

  getDownloadClients: async (): Promise<Provider[]> => {
    try {
      return await fetchApi<Provider[]>('/api/v1/providers/downloadclients');
    } catch {
      return [];
    }
  },

  getProviderImplementations: async (): Promise<ProviderImplementation[]> => {
    try {
      return await fetchApi<ProviderImplementation[]>('/api/v1/providers/implementations');
    } catch {
      return [];
    }
  },

  createIndexer: async (request: CreateProviderRequest): Promise<Provider> => {
    return await fetchApi<Provider>('/api/v1/providers/indexers', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  createDownloadClient: async (request: CreateProviderRequest): Promise<Provider> => {
    return await fetchApi<Provider>('/api/v1/providers/downloadclients', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  updateProvider: async (id: number, updates: Partial<CreateProviderRequest>): Promise<Provider> => {
    return await fetchApi<Provider>(`/api/v1/providers/${id}`, {
      method: 'PUT',
      body: JSON.stringify(updates),
    });
  },

  deleteProvider: async (id: number): Promise<void> => {
    await fetchApi(`/api/v1/providers/${id}`, { method: 'DELETE' });
  },

  setProviderEnabled: async (id: number, enabled: boolean): Promise<void> => {
    await fetchApi(`/api/v1/providers/${id}/enable?enabled=${enabled}`, { method: 'POST' });
  },

  reorderIndexers: async (orderedIds: number[]): Promise<void> => {
    await fetchApi('/api/v1/providers/indexers/reorder', {
      method: 'POST',
      body: JSON.stringify({ orderedIds }),
    });
  },

  reorderDownloadClients: async (orderedIds: number[]): Promise<void> => {
    await fetchApi('/api/v1/providers/downloadclients/reorder', {
      method: 'POST',
      body: JSON.stringify({ orderedIds }),
    });
  },

  testProvider: async (id: number): Promise<ProviderTestResult> => {
    return await fetchApi<ProviderTestResult>(`/api/v1/providers/${id}/test`, { method: 'POST' });
  },

  testNewProvider: async (request: CreateProviderRequest): Promise<ProviderTestResult> => {
    return await fetchApi<ProviderTestResult>('/api/v1/providers/test', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  // Settings
  getUiSettings: async (): Promise<UiSettings> => {
    try {
      return await fetchApi<UiSettings>('/api/v1/settings/ui');
    } catch {
      return { theme: 'dark', pageSize: 50, showFileSizes: true, relativeTimestamps: true, issueViewMode: 'cover', pullListDisplayMode: 'list' };
    }
  },

  updateUiSettings: async (settings: Partial<UiSettings>): Promise<UiSettings> => {
    // Fetch current settings first to merge
    const current = await api.getUiSettings();
    const merged = { ...current, ...settings };
    return await fetchApi<UiSettings>('/api/v1/settings/ui', {
      method: 'PUT',
      body: JSON.stringify(merged),
    });
  },

  getGeneralSettings: async (): Promise<GeneralSettings> => {
    try {
      return await fetchApi<GeneralSettings>('/api/v1/settings/general');
    } catch {
      return {
        seriesFolderFormat: '{Series Title} ({Year})',
        issueFileFormat: '{Series Title} #{Issue} ({Year})',
        collectionFileFormat: '{Series Title} - {Edition Type} Vol. {Volume} ({Year})',
        comicLibraryPath: '/comics',
        downloadFolder: '/downloads',
        stagingFolder: '/staging',
        autoMoveToStaging: true,
      };
    }
  },

  updateGeneralSettings: async (settings: Partial<GeneralSettings>): Promise<GeneralSettings> => {
    const current = await api.getGeneralSettings();
    const merged = { ...current, ...settings };
    return await fetchApi<GeneralSettings>('/api/v1/settings/general', {
      method: 'PUT',
      body: JSON.stringify(merged),
    });
  },

  getFolderSettings: async (): Promise<FolderSettings> => {
    try {
      return await fetchApi<FolderSettings>('/api/v1/settings/folders');
    } catch {
      return {
        comicLibraryPath: '/comics',
        downloadFolder: '/downloads',
        stagingFolder: '/staging',
        autoMoveToStaging: true,
      };
    }
  },

  updateFolderSettings: async (settings: Partial<FolderSettings>): Promise<FolderSettings> => {
    return await fetchApi<FolderSettings>('/api/v1/settings/folders', {
      method: 'PUT',
      body: JSON.stringify(settings),
    });
  },

  // Logging Settings
  getLoggingSettings: async (): Promise<LoggingSettings> => {
    try {
      return await fetchApi<LoggingSettings>('/api/v1/settings/logging');
    } catch {
      return {
        logLevel: 'Information',
        logPath: '',
        maxFileSizeMb: 10,
        rotationFileCount: 5,
        consoleLoggingEnabled: true,
        sqlQueryLogging: false,
        httpRequestBodyLogging: false,
        fullStackTraces: false,
        retentionDays: 30,
      };
    }
  },

  updateLoggingSettings: async (settings: LoggingSettings): Promise<LoggingSettings> => {
    return await fetchApi<LoggingSettings>('/api/v1/settings/logging', {
      method: 'PUT',
      body: JSON.stringify(settings),
    });
  },

  getNamingTokens: async (): Promise<NamingTokensResponse> => {
    return await fetchApi<NamingTokensResponse>('/api/v1/settings/naming/tokens');
  },

  // API Key
  getApiKey: async (): Promise<ApiKeyInfo> => {
    return await fetchApi<ApiKeyInfo>('/api/v1/settings/apikey');
  },

  getApiKeyFull: async (): Promise<ApiKeyInfo> => {
    return await fetchApi<ApiKeyInfo>('/api/v1/settings/apikey/full');
  },

  regenerateApiKey: async (): Promise<ApiKeyInfo> => {
    return await fetchApi<ApiKeyInfo>('/api/v1/settings/apikey/regenerate', { method: 'POST' });
  },

  setApiEnabled: async (enabled: boolean): Promise<ApiKeyInfo> => {
    return await fetchApi<ApiKeyInfo>('/api/v1/settings/apikey/enabled', {
      method: 'PUT',
      body: JSON.stringify({ enabled }),
    });
  },

  // ComicVine
  getComicVineSettings: async (): Promise<ComicVineSettings> => {
    try {
      return await fetchApi<ComicVineSettings>('/api/v1/comicvine/settings');
    } catch {
      return {
        enabled: false,
        hasApiKey: false,
        maskedApiKey: null,
        cacheTtlHours: 24,
        coverCacheDirectory: '/config/covers',
        autoMatchThreshold: 85,
        autoRefreshEnabled: true,
        refreshIntervalDays: 7,
      };
    }
  },

  updateComicVineSettings: async (settings: ComicVineSettingsUpdate): Promise<ComicVineSettings> => {
    return await fetchApi<ComicVineSettings>('/api/v1/comicvine/settings', {
      method: 'PUT',
      body: JSON.stringify(settings),
    });
  },

  getComicVineFullApiKey: async (): Promise<{ apiKey: string }> => {
    return await fetchApi<{ apiKey: string }>('/api/v1/comicvine/settings/apikey');
  },

  testComicVineConnection: async (): Promise<ComicVineTestResult> => {
    return await fetchApi<ComicVineTestResult>('/api/v1/comicvine/test', { method: 'POST' });
  },

  getComicVineRateLimit: async (): Promise<ComicVineRateLimitStatus> => {
    return await fetchApi<ComicVineRateLimitStatus>('/api/v1/comicvine/ratelimit');
  },

  searchComicVineVolumes: async (query: string, page = 1, limit = 10): Promise<ComicVineSearchResult<ComicVineVolume>> => {
    return await fetchApi<ComicVineSearchResult<ComicVineVolume>>(
      `/api/v1/comicvine/search/volumes?q=${encodeURIComponent(query)}&page=${page}&limit=${limit}`
    );
  },

  getComicVineVolume: async (volumeId: number): Promise<ComicVineResult<ComicVineVolume>> => {
    return await fetchApi<ComicVineResult<ComicVineVolume>>(`/api/v1/comicvine/volumes/${volumeId}`);
  },

  getComicVineVolumeIssues: async (volumeId: number, page = 1, limit = 100): Promise<ComicVineSearchResult<ComicVineIssue>> => {
    return await fetchApi<ComicVineSearchResult<ComicVineIssue>>(
      `/api/v1/comicvine/volumes/${volumeId}/issues?page=${page}&limit=${limit}`
    );
  },

  // Series Metadata (ComicVine integration)
  searchSeriesFromComicVine: async (
    query: string,
    options?: { publisher?: string; yearStart?: number; yearEnd?: number; page?: number; limit?: number }
  ): Promise<SeriesSearchResult> => {
    const params = new URLSearchParams({ q: query });
    if (options?.publisher) params.set('publisher', options.publisher);
    if (options?.yearStart) params.set('yearStart', String(options.yearStart));
    if (options?.yearEnd) params.set('yearEnd', String(options.yearEnd));
    if (options?.page) params.set('page', String(options.page));
    if (options?.limit) params.set('limit', String(options.limit));

    try {
      return await fetchApi<SeriesSearchResult>(`/api/v1/series/comicvine/search?${params}`);
    } catch (e) {
      return {
        success: false,
        error: e instanceof Error ? e.message : 'Search failed',
        results: [],
        totalResults: 0,
        page: 1,
        limit: 10,
      };
    }
  },

  previewSeriesFromComicVine: async (volumeId: number): Promise<SeriesMatchCandidate | null> => {
    try {
      return await fetchApi<SeriesMatchCandidate>(`/api/v1/series/comicvine/${volumeId}`);
    } catch {
      return null;
    }
  },

  addSeriesFromComicVine: async (
    volumeId: number,
    options?: AddSeriesFromComicVineRequest
  ): Promise<SeriesAddResult> => {
    try {
      return await fetchApi<SeriesAddResult>(`/api/v1/series/comicvine/${volumeId}`, {
        method: 'POST',
        body: JSON.stringify(options ?? {}),
      });
    } catch (e) {
      return {
        success: false,
        error: e instanceof Error ? e.message : 'Failed to add series',
        seriesId: null,
        comicVineId: null,
        title: null,
        issuesCreated: 0,
        alreadyExists: false,
        existingSeriesId: null,
      };
    }
  },

  // Pull List
  getPullListThisWeek: async (filter?: PullListFilter): Promise<WeeklyPullList> => {
    const params = buildPullListParams(filter);
    return fetchApi<WeeklyPullList>(`/api/v1/pulllist/week${params}`);
  },

  getPullListWeek: async (date: string, filter?: PullListFilter): Promise<WeeklyPullList> => {
    const params = buildPullListParams(filter);
    return fetchApi<WeeklyPullList>(`/api/v1/pulllist/week/${date}${params}`);
  },

  getPullListUpcoming: async (weeks?: number, filter?: PullListFilter): Promise<WeeklyPullList[]> => {
    const params = buildPullListParams(filter, weeks);
    return fetchApi<WeeklyPullList[]>(`/api/v1/pulllist/upcoming${params}`);
  },

  getPullListPast: async (weeks?: number, filter?: PullListFilter): Promise<WeeklyPullList[]> => {
    const params = buildPullListParams(filter, weeks);
    return fetchApi<WeeklyPullList[]>(`/api/v1/pulllist/past${params}`);
  },

  getPullListCalendar: async (startDate?: string, endDate?: string, filter?: PullListFilter): Promise<ReleaseCalendar> => {
    const searchParams = new URLSearchParams();
    if (startDate) searchParams.set('startDate', startDate);
    if (endDate) searchParams.set('endDate', endDate);
    if (filter?.publishers?.length) searchParams.set('publishers', filter.publishers.join(','));
    if (filter?.statuses?.length) searchParams.set('statuses', filter.statuses.join(','));
    if (filter?.monitoredOnly !== undefined) searchParams.set('monitoredOnly', String(filter.monitoredOnly));
    const query = searchParams.toString();
    return fetchApi<ReleaseCalendar>(`/api/v1/pulllist/calendar${query ? `?${query}` : ''}`);
  },

  getPullListStats: async (): Promise<PullListStats> => {
    return fetchApi<PullListStats>('/api/v1/pulllist/stats');
  },

  getPullListConfigStatus: async (): Promise<PullListConfigStatus> => {
    return fetchApi<PullListConfigStatus>('/api/v1/pulllist/config-status');
  },

  markIssueWanted: async (issueId: number): Promise<PullListActionResult> => {
    return fetchApi<PullListActionResult>(`/api/v1/pulllist/issues/${issueId}/wanted`, { method: 'POST' });
  },

  markIssueOwned: async (issueId: number): Promise<PullListActionResult> => {
    return fetchApi<PullListActionResult>(`/api/v1/pulllist/issues/${issueId}/owned`, { method: 'POST' });
  },

  markIssueSkipped: async (issueId: number): Promise<PullListActionResult> => {
    return fetchApi<PullListActionResult>(`/api/v1/pulllist/issues/${issueId}/skipped`, { method: 'POST' });
  },

  bulkUpdateIssueStatus: async (issueIds: number[], status: IssueStatus): Promise<PullListBulkResult> => {
    return fetchApi<PullListBulkResult>('/api/v1/pulllist/issues/bulk', {
      method: 'POST',
      body: JSON.stringify({ issueIds, status }),
    });
  },

  getSeriesMonitoringMode: async (seriesId: number): Promise<{ seriesId: number; monitoringMode: string }> => {
    return fetchApi<{ seriesId: number; monitoringMode: string }>(`/api/v1/pulllist/series/${seriesId}/monitoring`);
  },

  setSeriesMonitoringMode: async (seriesId: number, mode: string): Promise<PullListActionResult> => {
    return fetchApi<PullListActionResult>(`/api/v1/pulllist/series/${seriesId}/monitoring`, {
      method: 'PUT',
      body: JSON.stringify({ mode }),
    });
  },

  // Pull List Settings
  getPullListSettings: async (): Promise<PullListSettingsDto> => {
    return fetchApi<PullListSettingsDto>('/api/v1/pulllist/settings');
  },

  updatePullListSettings: async (settings: PullListSettingsDto): Promise<PullListActionResult> => {
    return fetchApi<PullListActionResult>('/api/v1/pulllist/settings', {
      method: 'PUT',
      body: JSON.stringify(settings),
    });
  },

  getSeriesPullListSettings: async (seriesId: number): Promise<SeriesPullListSettingsDto> => {
    return fetchApi<SeriesPullListSettingsDto>(`/api/v1/pulllist/series/${seriesId}/settings`);
  },

  updateSeriesPullListSettings: async (seriesId: number, settings: SeriesPullListSettingsDto): Promise<PullListActionResult> => {
    return fetchApi<PullListActionResult>(`/api/v1/pulllist/series/${seriesId}/settings`, {
      method: 'PUT',
      body: JSON.stringify(settings),
    });
  },

  // Search Settings
  getSearchSettings: async (): Promise<SearchSettings> => {
    return fetchApi<SearchSettings>('/api/v1/settings/search');
  },

  updateSearchSettings: async (settings: SearchSettings): Promise<{ message: string }> => {
    return fetchApi<{ message: string }>('/api/v1/settings/search', {
      method: 'PUT',
      body: JSON.stringify(settings),
    });
  },

  resetSearchSettings: async (): Promise<{ message: string; settings: SearchSettings }> => {
    return fetchApi<{ message: string; settings: SearchSettings }>('/api/v1/settings/search/reset', {
      method: 'POST',
    });
  },

  getSearchSettingsDefaults: async (): Promise<SearchSettings> => {
    return fetchApi<SearchSettings>('/api/v1/settings/search/defaults');
  },

  // Weekly Export (Mylar3 Parity)
  exportCurrentWeek: async (): Promise<WeeklyExportResult> => {
    return fetchApi<WeeklyExportResult>('/api/v1/pulllist/export', {
      method: 'POST',
    });
  },

  exportWeek: async (date: string): Promise<WeeklyExportResult> => {
    return fetchApi<WeeklyExportResult>(`/api/v1/pulllist/export/${date}`, {
      method: 'POST',
    });
  },

  getExportHistory: async (limit = 10): Promise<WeeklyExportInfo[]> => {
    return fetchApi<WeeklyExportInfo[]>(`/api/v1/pulllist/export/history?limit=${limit}`);
  },

  // Discovery (Mylar3 "This Week" parity)
  getWeeklyDiscovery: async (filter?: DiscoveryFilter): Promise<WeeklyDiscoveryList> => {
    const params = buildDiscoveryParams(filter);
    return fetchApi<WeeklyDiscoveryList>(`/api/v1/pulllist/discover/week${params}`);
  },

  getWeeklyDiscoveryByDate: async (date: string, filter?: DiscoveryFilter): Promise<WeeklyDiscoveryList> => {
    const params = buildDiscoveryParams(filter);
    return fetchApi<WeeklyDiscoveryList>(`/api/v1/pulllist/discover/week/${date}${params}`);
  },

  addIssueOneOff: async (comicVineIssueId: number): Promise<AddOneOffResult> => {
    return fetchApi<AddOneOffResult>('/api/v1/pulllist/discover/add-issue', {
      method: 'POST',
      body: JSON.stringify({ comicVineIssueId }),
    });
  },

  addSeriesFromDiscovery: async (
    comicVineVolumeId: number,
    markIssueWantedComicVineId?: number,
    monitoringMode: SeriesMonitoringMode = 'FutureIssues'
  ): Promise<AddFromDiscoveryResult> => {
    return fetchApi<AddFromDiscoveryResult>('/api/v1/pulllist/discover/add-series', {
      method: 'POST',
      body: JSON.stringify({
        comicVineVolumeId,
        markIssueWantedComicVineId,
        monitoringMode,
      }),
    });
  },

  // Metadata refresh
  refreshSeriesMetadata: async (seriesId: number, force = false): Promise<{ success: boolean; seriesRefreshed?: number; issuesRefreshed?: number; error?: string }> => {
    return fetchApi(`/api/v1/metadata/series/${seriesId}/refresh?force=${force}`, {
      method: 'POST',
    });
  },

  refreshSeriesIssues: async (seriesId: number, force = false): Promise<{ success: boolean; issuesRefreshed?: number; error?: string }> => {
    return fetchApi(`/api/v1/metadata/series/${seriesId}/issues/refresh?force=${force}`, {
      method: 'POST',
    });
  },

  refreshAllSeriesMetadata: async (force = false): Promise<{ success: boolean; error?: string; totalProcessed?: number; refreshed?: number; skipped?: number; errors?: number; newIssuesDiscovered?: number; duration?: string }> => {
    return fetchApi(`/api/v1/metadata/series/refresh-all?force=${force}`, {
      method: 'POST',
    });
  },

  // === NZB Indexers ===
  getNzbIndexers: async (): Promise<NzbIndexersResponse> => {
    return fetchApi<NzbIndexersResponse>('/api/v1/nzb/indexers');
  },

  getNzbIndexer: async (id: string): Promise<NzbIndexer> => {
    return fetchApi<NzbIndexer>(`/api/v1/nzb/indexers/${id}`);
  },

  addNzbIndexer: async (indexer: NzbIndexerRequest): Promise<NzbIndexer> => {
    return fetchApi<NzbIndexer>('/api/v1/nzb/indexers', {
      method: 'POST',
      body: JSON.stringify(indexer),
    });
  },

  updateNzbIndexer: async (id: string, indexer: NzbIndexerRequest): Promise<NzbIndexer> => {
    return fetchApi<NzbIndexer>(`/api/v1/nzb/indexers/${id}`, {
      method: 'PUT',
      body: JSON.stringify(indexer),
    });
  },

  deleteNzbIndexer: async (id: string): Promise<void> => {
    await fetchApi(`/api/v1/nzb/indexers/${id}`, { method: 'DELETE' });
  },

  testNzbIndexer: async (id: string): Promise<NzbTestResult> => {
    return fetchApi<NzbTestResult>(`/api/v1/nzb/indexers/${id}/test`, { method: 'POST' });
  },

  testNzbIndexerConfig: async (config: { baseUrl: string; apiKey: string }): Promise<NzbTestResult> => {
    return fetchApi<NzbTestResult>('/api/v1/nzb/indexers/test', {
      method: 'POST',
      body: JSON.stringify(config),
    });
  },

  getNzbIndexerPresets: async (): Promise<NzbIndexerPresetsResponse> => {
    return fetchApi<NzbIndexerPresetsResponse>('/api/v1/nzb/indexers/presets');
  },

  // === NZB Download Client ===
  getNzbDownloadClient: async (): Promise<NzbDownloadClientResponse> => {
    return fetchApi<NzbDownloadClientResponse>('/api/v1/nzb/download-client');
  },

  updateNzbDownloadClient: async (settings: NzbDownloadClientRequest): Promise<NzbDownloadClientResponse> => {
    return fetchApi<NzbDownloadClientResponse>('/api/v1/nzb/download-client', {
      method: 'PUT',
      body: JSON.stringify(settings),
    });
  },

  testNzbDownloadClient: async (settings: NzbDownloadClientRequest): Promise<NzbClientTestResult> => {
    return fetchApi<NzbClientTestResult>('/api/v1/nzb/download-client/test', {
      method: 'POST',
      body: JSON.stringify(settings),
    });
  },

  // === NZB Search ===
  searchNzb: async (query?: string, title?: string, limit?: number): Promise<NzbSearchResponse> => {
    const params = new URLSearchParams();
    if (query) params.set('query', query);
    if (title) params.set('title', title);
    if (limit) params.set('limit', String(limit));
    const queryStr = params.toString();
    return fetchApi<NzbSearchResponse>(`/api/v1/nzb/search${queryStr ? '?' + queryStr : ''}`);
  },

  // === Webhook Notification Providers ===
  getWebhookProviders: async (): Promise<WebhookProviderSettings[]> => {
    return fetchApi<WebhookProviderSettings[]>('/api/v1/notifications/providers');
  },

  getWebhookProvider: async (id: string): Promise<WebhookProviderSettings> => {
    return fetchApi<WebhookProviderSettings>(`/api/v1/notifications/providers/${id}`);
  },

  addWebhookProvider: async (provider: WebhookProviderRequest): Promise<WebhookProviderSettings> => {
    return fetchApi<WebhookProviderSettings>('/api/v1/notifications/providers', {
      method: 'POST',
      body: JSON.stringify({
        ...provider,
        providerType: 'Webhook',
      }),
    });
  },

  updateWebhookProvider: async (id: string, provider: WebhookProviderRequest): Promise<WebhookProviderSettings> => {
    return fetchApi<WebhookProviderSettings>(`/api/v1/notifications/providers/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        ...provider,
        id,
        providerType: 'Webhook',
      }),
    });
  },

  deleteWebhookProvider: async (id: string): Promise<void> => {
    await fetchApi(`/api/v1/notifications/providers/${id}`, { method: 'DELETE' });
  },

  testWebhookProvider: async (id: string): Promise<WebhookTestResult> => {
    return fetchApi<WebhookTestResult>(`/api/v1/notifications/providers/${id}/test`, { method: 'POST' });
  },

  testWebhookProviderSettings: async (settings: WebhookProviderRequest): Promise<WebhookTestResult> => {
    return fetchApi<WebhookTestResult>('/api/v1/notifications/providers/test', {
      method: 'POST',
      body: JSON.stringify({
        ...settings,
        providerType: 'Webhook',
      }),
    });
  },
};

function buildDiscoveryParams(filter?: DiscoveryFilter): string {
  if (!filter) return '';
  const params = new URLSearchParams();
  if (filter.publishers?.length) params.set('publishers', filter.publishers.join(','));
  if (filter.inLibraryOnly !== undefined) params.set('inLibraryOnly', String(filter.inLibraryOnly));
  if (filter.newOnly !== undefined) params.set('newOnly', String(filter.newOnly));
  if (filter.includeAnnuals !== undefined) params.set('includeAnnuals', String(filter.includeAnnuals));
  if (filter.includeSpecials !== undefined) params.set('includeSpecials', String(filter.includeSpecials));
  const query = params.toString();
  return query ? `?${query}` : '';
}

function buildPullListParams(filter?: PullListFilter, weeks?: number): string {
  const params = new URLSearchParams();
  if (weeks) params.set('weeks', String(weeks));
  if (filter?.publishers?.length) params.set('publishers', filter.publishers.join(','));
  if (filter?.statuses?.length) params.set('statuses', filter.statuses.join(','));
  if (filter?.monitoredOnly !== undefined) params.set('monitoredOnly', String(filter.monitoredOnly));
  const query = params.toString();
  return query ? `?${query}` : '';
}

function mapEventType(type: string): 'success' | 'warning' | 'info' | 'danger' {
  switch (type?.toLowerCase()) {
    case 'grabbed':
    case 'downloadcomplete':
      return 'success';
    case 'downloadfailed':
      return 'danger';
    case 'renamed':
    case 'imported':
      return 'info';
    default:
      return 'info';
  }
}

function mapHistoryType(type: string): 'grabbed' | 'imported' | 'deleted' | 'failed' | 'renamed' {
  switch (type?.toLowerCase()) {
    case 'grabbed':
      return 'grabbed';
    case 'imported':
    case 'downloadcomplete':
      return 'imported';
    case 'deleted':
      return 'deleted';
    case 'downloadfailed':
      return 'failed';
    case 'renamed':
      return 'renamed';
    default:
      return 'imported';
  }
}

function formatTimestamp(timestamp: string): string {
  if (!timestamp) return '';
  const date = new Date(timestamp);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;
  return date.toLocaleDateString();
}

function formatSize(bytes: number | undefined): string {
  if (!bytes) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB'];
  let size = bytes;
  let unitIndex = 0;
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024;
    unitIndex++;
  }
  return `${size.toFixed(1)} ${units[unitIndex]}`;
}

