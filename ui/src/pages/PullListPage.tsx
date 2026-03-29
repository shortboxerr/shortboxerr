import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  Calendar, 
  ChevronLeft, 
  ChevronRight, 
  RefreshCw, 
  Check, 
  X, 
  Eye,
  List,
  Grid,
  Plus,
  BookPlus,
  Library,
  Globe,
  ArrowUp,
  ArrowDown,
  ArrowUpDown,
  AlertTriangle,
  Settings,
  ImageOff,
  Loader2
} from 'lucide-react';
import { api } from '../api/client';
import type { 
  IssueStatus, 
  WeeklyDiscoveryList, 
  DiscoverableIssue,
  SeriesMonitoringMode
} from '../api/client';
import { Link, useNavigate } from 'react-router-dom';

type ViewMode = 'week' | 'upcoming' | 'past';
type DisplayMode = 'list' | 'grid';
type SortColumn = 'series' | 'issue' | 'publisher' | 'release' | 'status';
type SortDirection = 'asc' | 'desc';
type PullListFilter = 'all' | 'pullList' | 'new';

interface AddSeriesModalProps {
  issue: DiscoverableIssue;
  onClose: () => void;
  onAdd: (monitoringMode: SeriesMonitoringMode, markIssueWanted: boolean) => void;
}

function AddSeriesModal({ issue, onClose, onAdd }: AddSeriesModalProps) {
  const [monitoringMode, setMonitoringMode] = useState<SeriesMonitoringMode>('FutureIssues');
  const [markIssueWanted, setMarkIssueWanted] = useState(true);

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>Add Series</h3>
          <button className="btn btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>
        <div className="modal-body">
          <p>
            Add <strong>{issue.seriesTitle}</strong>
            {issue.startYear && <span> ({issue.startYear})</span>}
            {issue.publisher && <span> from {issue.publisher}</span>}
          </p>
          
          <div className="form-group">
            <label className="form-label">Monitoring Mode</label>
            <select 
              className="select"
              value={monitoringMode}
              onChange={(e) => setMonitoringMode(e.target.value as SeriesMonitoringMode)}
            >
              <option value="AllIssues">All Issues - Want every issue in the series</option>
              <option value="FutureIssues">Future Issues - Only want new issues going forward</option>
              <option value="Manual">Manual - Pick issues individually</option>
              <option value="FirstIssue">First Issue - Only want #1 issues</option>
              <option value="None">None - Add but don't monitor</option>
            </select>
          </div>

          <div className="form-group">
            <label className="checkbox-label">
              <input 
                type="checkbox" 
                checked={markIssueWanted}
                onChange={(e) => setMarkIssueWanted(e.target.checked)}
              />
              Mark #{issue.issueNumberText || issue.issueNumber} as wanted
            </label>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-secondary" onClick={onClose}>Cancel</button>
          <button 
            className="btn btn-primary" 
            onClick={() => onAdd(monitoringMode, markIssueWanted)}
          >
            Add Series
          </button>
        </div>
      </div>
    </div>
  );
}

export function PullListPage() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [viewMode, setViewMode] = useState<ViewMode>('week');
  const [weekOffset, setWeekOffset] = useState(0);
  const [addSeriesIssue, setAddSeriesIssue] = useState<DiscoverableIssue | null>(null);
  const [pullListFilter, setPullListFilter] = useState<PullListFilter>('all');
  const [sortColumn, setSortColumn] = useState<SortColumn>('series');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');
  const [lastRefresh, setLastRefresh] = useState<Date | null>(null);

  // Load UI settings for view preference persistence
  const { data: uiSettings } = useQuery({
    queryKey: ['settings', 'ui'],
    queryFn: () => api.getUiSettings(),
  });

  // Save display mode preference mutation
  const saveDisplayModePreference = useMutation({
    mutationFn: async (newDisplayMode: DisplayMode) => {
      await api.updateUiSettings({ pullListDisplayMode: newDisplayMode });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings', 'ui'] });
    },
  });

  const settingsDisplayMode: DisplayMode = uiSettings?.pullListDisplayMode ?? 'list';
  const displayMode: DisplayMode =
    saveDisplayModePreference.isPending && saveDisplayModePreference.variables !== undefined
      ? saveDisplayModePreference.variables
      : settingsDisplayMode;

  const handleDisplayModeChange = (newMode: DisplayMode) => {
    saveDisplayModePreference.mutate(newMode);
  };

  // Calculate week date based on offset (deterministic string per weekOffset for queryKey).
  const weekDate = (() => {
    const date = new Date();
    date.setDate(date.getDate() + weekOffset * 7);
    return date.toISOString().split('T')[0];
  })();

  // Discovery query - always fetch all releases, filter client-side
  // Short stale time ensures cover enrichment updates are picked up quickly
  const { data: discovery, isLoading: discoveryLoading, isFetching: discoveryFetching, refetch: refetchDiscovery } = useQuery({
    queryKey: ['pulllist', 'discovery', weekDate],
    queryFn: async ({ queryKey }) => {
      const date = queryKey[2] as string;
      return api.getWeeklyDiscoveryByDate(date, {});
    },
    enabled: viewMode === 'week',
    staleTime: 2 * 60 * 1000, // 2 minutes - allows cover enrichment updates to show quickly
    refetchOnWindowFocus: true, // Refresh when user returns to tab
  });

  // Upcoming weeks (for multi-week views) - fetch in parallel
  const { data: upcomingDiscovery, isLoading: upcomingLoading } = useQuery({
    queryKey: ['pulllist', 'discovery', 'upcoming'],
    queryFn: async () => {
      const weekPromises = [1, 2, 3, 4].map((i) => {
        const date = new Date();
        date.setDate(date.getDate() + (i * 7));
        const dateStr = date.toISOString().split('T')[0];
        return api.getWeeklyDiscoveryByDate(dateStr, {});
      });
      return Promise.all(weekPromises);
    },
    enabled: viewMode === 'upcoming',
    staleTime: 2 * 60 * 1000, // 2 minutes - allows cover enrichment updates to show quickly
    refetchOnWindowFocus: true,
  });

  // Past weeks - fetch in parallel
  const { data: pastDiscovery, isLoading: pastLoading } = useQuery({
    queryKey: ['pulllist', 'discovery', 'past'],
    queryFn: async () => {
      const weekPromises = [1, 2, 3, 4].map((i) => {
        const date = new Date();
        date.setDate(date.getDate() - (i * 7));
        const dateStr = date.toISOString().split('T')[0];
        return api.getWeeklyDiscoveryByDate(dateStr, {});
      });
      return Promise.all(weekPromises);
    },
    enabled: viewMode === 'past',
    staleTime: 2 * 60 * 1000, // 2 minutes - allows cover enrichment updates to show quickly
    refetchOnWindowFocus: true,
  });

  const { data: stats } = useQuery({
    queryKey: ['pulllist', 'stats'],
    queryFn: () => api.getPullListStats(),
  });

  // Config status for UX improvements
  const { data: configStatus } = useQuery({
    queryKey: ['pulllist', 'config-status'],
    queryFn: () => api.getPullListConfigStatus(),
    staleTime: 5 * 60 * 1000, // 5 minutes - config doesn't change often
  });

  // Library mode mutations
  const markWanted = useMutation({
    mutationFn: (issueId: number) => api.markIssueWanted(issueId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pulllist'] }),
  });

  const markOwned = useMutation({
    mutationFn: (issueId: number) => api.markIssueOwned(issueId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pulllist'] }),
  });

  const markSkipped = useMutation({
    mutationFn: (issueId: number) => api.markIssueSkipped(issueId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pulllist'] }),
  });

  // Discovery mode mutations
  const addIssueOneOff = useMutation({
    mutationFn: (comicVineIssueId: number) => api.addIssueOneOff(comicVineIssueId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pulllist'] }),
  });

  const addSeriesFromDiscovery = useMutation({
    mutationFn: ({ comicVineVolumeId, markIssueWantedComicVineId, monitoringMode, expectedPublisher, seriesTitle, expectedIssueNumber }: {
      comicVineVolumeId: number;
      markIssueWantedComicVineId?: number;
      monitoringMode: SeriesMonitoringMode;
      expectedPublisher?: string;
      seriesTitle?: string;
      expectedIssueNumber?: number;
    }) => api.addSeriesFromDiscovery(comicVineVolumeId, markIssueWantedComicVineId, monitoringMode, expectedPublisher, seriesTitle, expectedIssueNumber),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pulllist'] });
      setAddSeriesIssue(null);
    },
  });

  // Cover enrichment mutation for refreshing missing covers
  const triggerCoverEnrichment = useMutation({
    mutationFn: (force: boolean) => api.triggerCoverEnrichment(force),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pulllist'] });
    },
  });

  // Filter discovery issues by pull list status
  const filterDiscoveryIssues = (issues: DiscoverableIssue[]) => {
    switch (pullListFilter) {
      case 'pullList':
        return issues.filter(i => i.isInLibrary);
      case 'new':
        return issues.filter(i => !i.isInLibrary);
      default:
        return issues;
    }
  };

  // Toggle sort column/direction
  const handleSort = (column: SortColumn) => {
    if (sortColumn === column) {
      setSortDirection(d => d === 'asc' ? 'desc' : 'asc');
    } else {
      setSortColumn(column);
      setSortDirection('asc');
    }
  };

  // Get sort indicator for column header
  const getSortIcon = (column: SortColumn) => {
    if (sortColumn !== column) {
      return <ArrowUpDown size={14} className="sort-icon inactive" />;
    }
    return sortDirection === 'asc' 
      ? <ArrowUp size={14} className="sort-icon active" />
      : <ArrowDown size={14} className="sort-icon active" />;
  };

  // Sort and deduplicate discovery issues (ComicVine API sometimes returns duplicates)
  // When showing all releases, library items sort to top for visibility
  const sortDiscoveryIssues = (issues: DiscoverableIssue[]): DiscoverableIssue[] => {
    // Deduplicate by unique key: volumeId + issueNumber (or issueId if available)
    // WalkSoftly data has comicVineIssueId=0, so we need a composite key
    const seen = new Set<string>();
    const unique = issues.filter(issue => {
      const key = issue.comicVineIssueId > 0 
        ? `issue:${issue.comicVineIssueId}`
        : `volume:${issue.comicVineVolumeId}:${issue.issueNumber}`;
      if (seen.has(key)) {
        return false;
      }
      seen.add(key);
      return true;
    });
    
    return unique.sort((a, b) => {
      // Primary: Library items first when showing all releases
      if (pullListFilter === 'all') {
        if (a.isInLibrary && !b.isInLibrary) return -1;
        if (!a.isInLibrary && b.isInLibrary) return 1;
      }
      
      let comparison = 0;
      
      switch (sortColumn) {
        case 'series':
          comparison = (a.seriesTitle || '').localeCompare(b.seriesTitle || '');
          // Secondary sort by issue number
          if (comparison === 0) {
            comparison = a.issueNumber - b.issueNumber;
          }
          break;
        case 'issue':
          comparison = a.issueNumber - b.issueNumber;
          break;
        case 'publisher':
          comparison = (a.publisher || '').localeCompare(b.publisher || '');
          break;
        case 'release':
          comparison = (a.storeDate || '').localeCompare(b.storeDate || '');
          break;
        case 'status':
          comparison = (a.status || '').localeCompare(b.status || '');
          break;
      }
      
      return sortDirection === 'asc' ? comparison : -comparison;
    });
  };

  // Use isFetching (not just isLoading) to show spinner when navigating between weeks
  const isLoading = viewMode === 'week' 
    ? (discoveryLoading || discoveryFetching)
    : viewMode === 'upcoming' ? upcomingLoading : pastLoading;

  // Handle manual refresh with timestamp tracking
  const handleManualRefresh = () => {
    setLastRefresh(new Date());
    refetchDiscovery();
  };

  // Format relative time for last refresh
  const formatLastRefresh = (date: Date | null): string => {
    if (!date) return '';
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    const diffHours = Math.floor(diffMins / 60);
    return `${diffHours}h ago`;
  };

  // Render empty state based on configuration status
  const renderEmptyState = () => {
    if (configStatus && !configStatus.isComicVineConfigured) {
      return (
        <div className="empty-state">
          <AlertTriangle size={48} className="text-warning" />
          <div className="empty-state-title">ComicVine API Not Configured</div>
          <div className="empty-state-text">
            Configure your ComicVine API key to discover new releases.
          </div>
          <button 
            className="btn btn-primary mt-3"
            onClick={() => navigate('/settings?tab=comicvine')}
          >
            <Settings size={16} />
            Configure ComicVine
          </button>
        </div>
      );
    }

    if (pullListFilter === 'pullList') {
      return (
        <div className="empty-state">
          <Library size={48} />
          <div className="empty-state-title">No Pull List Items This Week</div>
          <div className="empty-state-text">
            None of your monitored series have releases this week.
          </div>
          <div className="empty-state-actions">
            <button 
              className="btn btn-primary"
              onClick={() => setPullListFilter('all')}
            >
              <Globe size={16} />
              View All Releases
            </button>
            <button 
              className="btn btn-secondary"
              onClick={() => navigate('/series')}
            >
              <Plus size={16} />
              Add Series
            </button>
          </div>
        </div>
      );
    }

    return (
      <div className="empty-state">
        <Calendar size={48} />
        <div className="empty-state-title">No Releases Found</div>
        <div className="empty-state-text">
          No comics are releasing during this week, or the API may be temporarily unavailable.
        </div>
        <button 
          className="btn btn-secondary mt-3"
          onClick={handleManualRefresh}
        >
          <RefreshCw size={16} />
          Refresh
        </button>
      </div>
    );
  };

  // Format date for display (use UTC to avoid timezone shifts)
  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    const currentYear = new Date().getFullYear();
    const dateYear = date.getUTCFullYear();
    return date.toLocaleDateString('en-US', { 
      month: 'short', 
      day: 'numeric',
      year: dateYear !== currentYear ? 'numeric' : undefined,
      timeZone: 'UTC'
    });
  };

  // Format release day with day of week (e.g., "Wednesday, Feb 4, 2026")
  // Use UTC to avoid timezone shifting the date
  const formatReleaseDay = (releaseDayStr: string) => {
    const date = new Date(releaseDayStr);
    const dayOfWeek = date.toLocaleDateString('en-US', { weekday: 'long', timeZone: 'UTC' });
    const dateStr = date.toLocaleDateString('en-US', { 
      month: 'short', 
      day: 'numeric',
      year: 'numeric',
      timeZone: 'UTC'
    });
    return `${dayOfWeek}, ${dateStr}`;
  };

  // Get status badge
  const getStatusBadge = (status: IssueStatus | null) => {
    if (!status) return null;
    switch (status) {
      case 'Owned':
        return <span className="badge badge-success">Owned</span>;
      case 'Wanted':
        return <span className="badge badge-warning">Wanted</span>;
      case 'Skipped':
        return <span className="badge badge-secondary">Skipped</span>;
      case 'Downloading':
        return <span className="badge badge-info">Downloading</span>;
      case 'Missing':
        return <span className="badge badge-danger">Missing</span>;
      case 'Staged':
        return <span className="badge badge-info">Staged</span>;
      default:
        return <span className="badge">{status}</span>;
    }
  };

  // Generate unique key for discovery issues
  // WalkSoftly data has comicVineIssueId=0, so we need a composite key
  const getIssueKey = (issue: DiscoverableIssue) => {
    return issue.comicVineIssueId > 0
      ? `${weekDate}-issue-${issue.comicVineIssueId}`
      : `${weekDate}-vol-${issue.comicVineVolumeId}-${issue.issueNumber}`;
  };

  // Render discovery card for grid view
  const renderDiscoveryCard = (issue: DiscoverableIssue) => {
    const handleCoverClick = () => {
      if (issue.isInLibrary && issue.localSeriesId) {
        navigate(`/series/${issue.localSeriesId}`);
      } else if (issue.comicVineVolumeId) {
        // Open ComicVine page in new tab for items not in library
        window.open(`https://comicvine.gamespot.com/volume/4050-${issue.comicVineVolumeId}/`, '_blank');
      }
    };

    return (
    <div 
      key={getIssueKey(issue)} 
      className={`pull-list-card ${issue.isInLibrary ? 'in-library' : 'discoverable'}`}
    >
      <div 
        className="pull-list-card-cover clickable"
        onClick={handleCoverClick}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => e.key === 'Enter' && handleCoverClick()}
      >
        {issue.coverImageUrl ? (
          <img src={issue.coverImageUrl} alt={`${issue.seriesTitle} #${issue.issueNumber}`} loading="lazy" decoding="async" />
        ) : (
          <div className="pull-list-card-placeholder">
            <Calendar size={32} />
          </div>
        )}
        {issue.isInLibrary ? (
          <div className="pull-list-card-status">
            {getStatusBadge(issue.status)}
          </div>
        ) : (
          <div className="pull-list-card-badge">
            <span className="badge badge-accent">NEW</span>
          </div>
        )}
        {issue.isSeriesMonitored && (
          <div className="pull-list-card-monitored" title="Series is monitored">
            <Eye size={12} />
          </div>
        )}
        {issue.isVolumeFallbackCover && (
          <div className="pull-list-card-fallback" title="Series cover (issue cover unavailable)">
            <ImageOff size={12} />
          </div>
        )}
      </div>
      <div className="pull-list-card-info">
        {issue.isInLibrary && issue.localSeriesId ? (
          <Link to={`/series/${issue.localSeriesId}`} className="pull-list-card-series">
            {issue.seriesTitle}
          </Link>
        ) : (
          <div className="pull-list-card-series">{issue.seriesTitle}</div>
        )}
        <div className="pull-list-card-issue">
          #{issue.issueNumberText || issue.issueNumber}
          {issue.issueTitle && <span className="pull-list-card-title"> - {issue.issueTitle}</span>}
        </div>
        {issue.publisher && <div className="pull-list-card-publisher">{issue.publisher}</div>}
        {issue.startYear && !issue.isInLibrary && (
          <div className="pull-list-card-year">{issue.startYear}</div>
        )}
      </div>
      <div className="pull-list-card-actions">
        {issue.isInLibrary && issue.localIssueId ? (
          // In library - show status actions
          <>
            <button 
              className="btn btn-icon btn-sm" 
              title="Mark as Wanted"
              onClick={() => markWanted.mutate(issue.localIssueId!)}
              disabled={issue.status === 'Wanted'}
            >
              <Eye size={14} />
            </button>
            <button 
              className="btn btn-icon btn-sm" 
              title="Mark as Owned"
              onClick={() => markOwned.mutate(issue.localIssueId!)}
              disabled={issue.status === 'Owned'}
            >
              <Check size={14} />
            </button>
            <button 
              className="btn btn-icon btn-sm" 
              title="Skip"
              onClick={() => markSkipped.mutate(issue.localIssueId!)}
              disabled={issue.status === 'Skipped'}
            >
              <X size={14} />
            </button>
          </>
        ) : (
          // Not in library - show add actions
          <>
            <button 
              className="btn btn-sm btn-accent" 
              title="Add this issue only"
              onClick={() => addIssueOneOff.mutate(issue.comicVineIssueId)}
              disabled={addIssueOneOff.isPending}
            >
              <Plus size={14} />
              <span>Issue</span>
            </button>
            <button 
              className="btn btn-sm btn-primary" 
              title="Add entire series"
              onClick={() => setAddSeriesIssue(issue)}
            >
              <BookPlus size={14} />
              <span>Series</span>
            </button>
          </>
        )}
      </div>
    </div>
  );
  };

  // Render discovery row for list view
  const renderDiscoveryRow = (issue: DiscoverableIssue) => (
    <tr key={getIssueKey(issue)} className={issue.isInLibrary ? 'in-library' : 'discoverable'}>
      <td>
        {issue.coverImageUrl ? (
          <img 
            src={issue.coverImageUrl} 
            alt="" 
            className="pull-list-thumb"
            loading="lazy"
            decoding="async"
          />
        ) : (
          <div className="pull-list-thumb-placeholder">
            <Calendar size={16} />
          </div>
        )}
      </td>
      <td>
        {issue.isInLibrary && issue.localSeriesId ? (
          <Link to={`/series/${issue.localSeriesId}`} className="text-link">
            {issue.seriesTitle}
          </Link>
        ) : (
          <span>{issue.seriesTitle}</span>
        )}
        {issue.startYear && <span className="text-muted"> ({issue.startYear})</span>}
        {!issue.isInLibrary && <span className="badge badge-accent badge-sm ml-2">NEW</span>}
      </td>
      <td>
        #{issue.issueNumberText || issue.issueNumber}
        {issue.issueTitle && <span className="text-muted"> - {issue.issueTitle}</span>}
      </td>
      <td>{issue.publisher || '-'}</td>
      <td>{issue.storeDate ? formatDate(issue.storeDate) : '-'}</td>
      <td>
        {issue.isInLibrary ? (
          getStatusBadge(issue.status)
        ) : (
          <span className="text-muted">-</span>
        )}
        {issue.isSeriesMonitored && (
          <span title="Monitored"><Eye size={12} className="ml-1 text-accent" /></span>
        )}
      </td>
      <td className="table-actions">
        {issue.isInLibrary && issue.localIssueId ? (
          <>
            <button 
              className="btn btn-icon btn-sm" 
              title="Mark as Wanted"
              onClick={() => markWanted.mutate(issue.localIssueId!)}
              disabled={issue.status === 'Wanted'}
            >
              <Eye size={14} />
            </button>
            <button 
              className="btn btn-icon btn-sm" 
              title="Mark as Owned"
              onClick={() => markOwned.mutate(issue.localIssueId!)}
              disabled={issue.status === 'Owned'}
            >
              <Check size={14} />
            </button>
            <button 
              className="btn btn-icon btn-sm" 
              title="Skip"
              onClick={() => markSkipped.mutate(issue.localIssueId!)}
              disabled={issue.status === 'Skipped'}
            >
              <X size={14} />
            </button>
          </>
        ) : (
          <>
            <button 
              className="btn btn-sm" 
              title="Add this issue only"
              onClick={() => addIssueOneOff.mutate(issue.comicVineIssueId)}
              disabled={addIssueOneOff.isPending}
            >
              <Plus size={14} /> Issue
            </button>
            <button 
              className="btn btn-sm btn-primary" 
              title="Add entire series"
              onClick={() => setAddSeriesIssue(issue)}
            >
              <BookPlus size={14} /> Series
            </button>
          </>
        )}
      </td>
    </tr>
  );

  // Render discovery section with filtering
  const renderDiscoverySection = (data: WeeklyDiscoveryList) => {
    const filtered = filterDiscoveryIssues(data.issues);
    const sorted = sortDiscoveryIssues(filtered);
    
    return (
      <div className="pull-list-week-section">
        <div className="pull-list-week-header">
          <h3>{formatReleaseDay(data.releaseDay)}</h3>
          <div className="pull-list-week-stats">
            <span className="stat">{data.totalCount} releases</span>
            <span className="stat in-library">{data.inLibraryCount} in library</span>
            <span className="stat new">{data.newCount} new</span>
            {pullListFilter !== 'all' && (
              <span className="stat filtered">showing {sorted.length}</span>
            )}
          </div>
        </div>
        
        {sorted.length === 0 ? (
          renderEmptyState()
        ) : displayMode === 'grid' ? (
          <div className="pull-list-grid">
            {sorted.map(renderDiscoveryCard)}
          </div>
        ) : (
          <div className="table-container">
            <table className="table">
              <thead>
                <tr>
                  <th style={{ width: 50 }}></th>
                  <th className="sortable" onClick={() => handleSort('series')}>
                    Series {getSortIcon('series')}
                  </th>
                  <th className="sortable" onClick={() => handleSort('issue')}>
                    Issue {getSortIcon('issue')}
                  </th>
                  <th className="sortable" onClick={() => handleSort('publisher')}>
                    Publisher {getSortIcon('publisher')}
                  </th>
                  <th className="sortable" onClick={() => handleSort('release')}>
                    Release {getSortIcon('release')}
                  </th>
                  <th className="sortable" onClick={() => handleSort('status')}>
                    Status {getSortIcon('status')}
                  </th>
                  <th className="table-actions"></th>
                </tr>
              </thead>
              <tbody>
                {sorted.map(renderDiscoveryRow)}
              </tbody>
            </table>
          </div>
        )}
      </div>
    );
  };

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">
          <Calendar className="page-title-icon" size={24} />
          Pull List
        </h1>
        <div className="toolbar-group">
          {stats && (
            <div className="stats-badges">
              <span className="badge badge-warning">{stats.releasingThisWeek} this week</span>
              <span className="badge badge-info">{stats.totalWantedIssues} wanted</span>
              {stats.missedIssues > 0 && (
                <span className="badge badge-danger">{stats.missedIssues} missed</span>
              )}
            </div>
          )}
        </div>
      </header>

      {/* Configuration warning banner */}
      {configStatus && !configStatus.isComicVineConfigured && (
        <div className="alert alert-warning mb-3">
          <AlertTriangle size={18} />
          <span>
            ComicVine API is not configured. 
            <Link to="/settings?tab=comicvine" className="alert-link ml-1">
              Configure now
            </Link> to enable release tracking and discovery.
          </span>
        </div>
      )}
      
      <div className="page-content">
        <div className="toolbar">
          {/* Pull list filter toggle */}
          <div className="toolbar-group btn-group">
            <button 
              className={`btn ${pullListFilter === 'all' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setPullListFilter('all')}
              title="All Releases - all comics releasing this week (your pull list highlighted)"
            >
              <Globe size={16} />
              All Releases
            </button>
            <button 
              className={`btn ${pullListFilter === 'pullList' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setPullListFilter('pullList')}
              title="My Pull List - only comics from your library"
            >
              <Library size={16} />
              My Pull List
            </button>
            <button 
              className={`btn ${pullListFilter === 'new' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setPullListFilter('new')}
              title="New to Me - comics not in your library"
            >
              <Plus size={16} />
              New to Me
            </button>
          </div>

          {/* Week/View navigation */}
          <div className="toolbar-group">
            <button 
              className="btn btn-icon" 
              onClick={() => {
                if (viewMode !== 'week') {
                  setViewMode('week');
                }
                setWeekOffset(o => o - 1);
              }}
              title="Previous Week"
            >
              <ChevronLeft size={18} />
            </button>
            <select
              className="select"
              value={viewMode === 'week' ? `week:${weekOffset}` : viewMode}
              onChange={(e) => {
                const val = e.target.value;
                if (val === 'upcoming') {
                  setViewMode('upcoming');
                } else if (val === 'past') {
                  setViewMode('past');
                } else if (val.startsWith('week:')) {
                  setViewMode('week');
                  setWeekOffset(parseInt(val.split(':')[1], 10));
                }
              }}
            >
              <option value="week:0">This Week</option>
              {weekOffset !== 0 && viewMode === 'week' && (
                <option value={`week:${weekOffset}`}>
                  {weekOffset > 0 ? `+${weekOffset}` : weekOffset} Week{Math.abs(weekOffset) !== 1 ? 's' : ''}
                </option>
              )}
              <option value="upcoming">Upcoming (4 weeks)</option>
              <option value="past">Past (4 weeks)</option>
            </select>
            <button 
              className="btn btn-icon" 
              onClick={() => {
                if (viewMode !== 'week') {
                  setViewMode('week');
                }
                setWeekOffset(o => o + 1);
              }}
              title="Next Week"
            >
              <ChevronRight size={18} />
            </button>
          </div>

          <div className="toolbar-spacer" />

          {/* Display mode toggle - persisted to user settings */}
          <div className="toolbar-group btn-group">
            <button 
              className={`btn btn-icon ${displayMode === 'list' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => handleDisplayModeChange('list')}
              title="List View"
            >
              <List size={18} />
            </button>
            <button 
              className={`btn btn-icon ${displayMode === 'grid' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => handleDisplayModeChange('grid')}
              title="Cover View"
            >
              <Grid size={18} />
            </button>
          </div>

          <div className="toolbar-group refresh-group">
            {lastRefresh && (
              <span className="text-muted text-sm">
                Updated {formatLastRefresh(lastRefresh)}
              </span>
            )}
            <button 
              className="btn btn-sm" 
              onClick={() => triggerCoverEnrichment.mutate(true)} 
              title="Refresh covers from Metron for issues showing series covers"
              disabled={triggerCoverEnrichment.isPending}
            >
              {triggerCoverEnrichment.isPending ? (
                <Loader2 size={14} className="spin" />
              ) : (
                <ImageOff size={14} />
              )}
              <span>Refresh Covers</span>
            </button>
            <button 
              className="btn btn-icon" 
              onClick={handleManualRefresh} 
              title="Refresh from ComicVine"
              disabled={isLoading}
            >
              <RefreshCw size={18} className={isLoading ? 'spin' : ''} />
            </button>
          </div>
        </div>
        
        {/* Content */}
        {isLoading ? (
          <div className="loading"><div className="spinner" /></div>
        ) : viewMode === 'week' && discovery ? (
          renderDiscoverySection(discovery)
        ) : viewMode === 'upcoming' && upcomingDiscovery ? (
          <div className="pull-list-weeks">
            {upcomingDiscovery.map((week, i) => (
              <div key={i}>{renderDiscoverySection(week)}</div>
            ))}
          </div>
        ) : viewMode === 'past' && pastDiscovery ? (
          <div className="pull-list-weeks">
            {pastDiscovery.map((week, i) => (
              <div key={i}>{renderDiscoverySection(week)}</div>
            ))}
          </div>
        ) : (
          renderEmptyState()
        )}
      </div>

      {/* Add Series Modal */}
      {addSeriesIssue && (
        <AddSeriesModal
          issue={addSeriesIssue}
          onClose={() => setAddSeriesIssue(null)}
          onAdd={(monitoringMode, markIssueWanted) => {
            addSeriesFromDiscovery.mutate({
              comicVineVolumeId: addSeriesIssue.comicVineVolumeId,
              markIssueWantedComicVineId: markIssueWanted ? addSeriesIssue.comicVineIssueId : undefined,
              monitoringMode,
              expectedPublisher: addSeriesIssue.publisher ?? undefined,
              seriesTitle: addSeriesIssue.seriesTitle,
              expectedIssueNumber: addSeriesIssue.issueNumber,
            });
          }}
        />
      )}
    </>
  );
}
