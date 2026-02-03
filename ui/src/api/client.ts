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

interface Activity {
  id: string;
  title: string;
  type: 'success' | 'warning' | 'info' | 'danger';
  timestamp: string;
}

interface Series {
  id: number;
  title: string;
  year: number | null;
  publisher: string | null;
  status: string;
  issueCount: number;
  filesCount: number;
}

interface Edition {
  id: number;
  title: string;
  seriesTitle: string;
  editionType: string;
  volumeNumber: number | null;
  year: number | null;
  publisher: string | null;
  hasFile: boolean;
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
  issueNumber?: number;
  volumeNumber?: number;
  editionType?: string;
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

  // Series
  getSeries: async (params: { search?: string; page?: number; pageSize?: number }): Promise<PagedResult<Series>> => {
    const query = new URLSearchParams();
    if (params.search) query.set('search', params.search);
    if (params.page) query.set('page', String(params.page));
    if (params.pageSize) query.set('pageSize', String(params.pageSize));

    try {
      const response = await fetchApi<ApiPagedResult<Series>>(`/api/v1/series?${query}`);
      return toPagedResult(response);
    } catch {
      return { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 };
    }
  },

  deleteSeries: async (id: number): Promise<void> => {
    await fetchApi(`/api/v1/series/${id}`, { method: 'DELETE' });
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

  // Activity
  getActivityQueue: async (): Promise<QueueItem[]> => {
    // This would connect to a real queue endpoint
    // For now, return empty as there's no queue implementation
    return [];
  },

  // Wanted
  getWanted: async (_params: { type: string; search?: string }): Promise<PagedResult<WantedItem>> => {
    // This would filter series/editions without files
    return { items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 };
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
      return { theme: 'dark', pageSize: 50, showFileSizes: true, relativeTimestamps: true };
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
};

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

