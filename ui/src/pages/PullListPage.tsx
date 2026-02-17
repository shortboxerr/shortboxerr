import { useState, useMemo, useEffect } from 'react';
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
  Filter,
  Plus,
  BookPlus,
  Library,
  Globe,
  ArrowUp,
  ArrowDown,
  ArrowUpDown,
  AlertTriangle,
  Settings,
  BookOpen,
  Link as LinkIcon
} from 'lucide-react';
import { api } from '../api/client';
import type { 
  WeeklyPullList, 
  PullListIssue, 
  IssueStatus, 
  WeeklyDiscoveryList, 
  DiscoverableIssue,
  SeriesMonitoringMode
} from '../api/client';
import { Link, useNavigate } from 'react-router-dom';

type ViewMode = 'week' | 'upcoming' | 'past';
type DisplayMode = 'list' | 'grid';
type SourceMode = 'library' | 'discover';
type SortColumn = 'series' | 'issue' | 'publisher' | 'release' | 'status';
type SortDirection = 'asc' | 'desc';

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
  const [displayMode, setDisplayMode] = useState<DisplayMode>('list');
  const [sourceMode, setSourceMode] = useState<SourceMode>('library');
  const [weekOffset, setWeekOffset] = useState(0);
  const [selectedIssues, setSelectedIssues] = useState<Set<number>>(new Set());
  const [statusFilter, setStatusFilter] = useState<IssueStatus | 'all'>('all');
  const [addSeriesIssue, setAddSeriesIssue] = useState<DiscoverableIssue | null>(null);
  const [discoveryFilter, setDiscoveryFilter] = useState<'all' | 'new' | 'inLibrary'>('all');
  const [sortColumn, setSortColumn] = useState<SortColumn>('series');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');
  const [lastRefresh, setLastRefresh] = useState<Date | null>(null);

  // Load UI settings for view preference persistence
  const { data: uiSettings } = useQuery({
    queryKey: ['settings', 'ui'],
    queryFn: () => api.getUiSettings(),
  });

  // Sync display mode from settings when loaded
  useEffect(() => {
    if (uiSettings?.pullListDisplayMode) {
      setDisplayMode(uiSettings.pullListDisplayMode);
    }
  }, [uiSettings?.pullListDisplayMode]);

  // Save display mode preference mutation
  const saveDisplayModePreference = useMutation({
    mutationFn: async (newDisplayMode: DisplayMode) => {
      await api.updateUiSettings({ pullListDisplayMode: newDisplayMode });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings', 'ui'] });
    },
  });

  // Handle display mode change with persistence
  const handleDisplayModeChange = (newMode: DisplayMode) => {
    setDisplayMode(newMode);
    saveDisplayModePreference.mutate(newMode);
  };

  // Calculate week date based on offset - memoized to ensure stable reference
  const weekDate = useMemo(() => {
    const date = new Date();
    date.setDate(date.getDate() + (weekOffset * 7));
    return date.toISOString().split('T')[0];
  }, [weekOffset]);

  // Library mode queries - use weekDate in query key for consistent caching
  // Cache for 30 minutes to match backend ComicVine cache - release schedules rarely change
  const { data: thisWeek, isLoading: thisWeekLoading, isFetching: thisWeekFetching, refetch: refetchThisWeek } = useQuery({
    queryKey: ['pulllist', 'week', weekDate],
    queryFn: ({ queryKey }) => {
      const date = queryKey[2] as string;
      return api.getPullListWeek(date);
    },
    enabled: viewMode === 'week' && sourceMode === 'library',
    staleTime: 30 * 60 * 1000, // 30 minutes - matches backend cache, release schedules rarely change
  });

  const { data: upcoming, isLoading: upcomingLoading } = useQuery({
    queryKey: ['pulllist', 'upcoming'],
    queryFn: () => api.getPullListUpcoming(4),
    enabled: viewMode === 'upcoming' && sourceMode === 'library',
    staleTime: 30 * 60 * 1000, // 30 minutes
  });

  const { data: past, isLoading: pastLoading } = useQuery({
    queryKey: ['pulllist', 'past'],
    queryFn: () => api.getPullListPast(4),
    enabled: viewMode === 'past' && sourceMode === 'library',
    staleTime: 30 * 60 * 1000, // 30 minutes - past releases don't change
  });

  // Discovery mode queries - use weekDate from queryKey to avoid closure issues
  // Cache for 30 minutes - ComicVine release data is set weeks in advance and rarely changes
  const { data: discovery, isLoading: discoveryLoading, isFetching: discoveryFetching, refetch: refetchDiscovery } = useQuery({
    queryKey: ['pulllist', 'discovery', weekDate, discoveryFilter],
    queryFn: async ({ queryKey }) => {
      const date = queryKey[2] as string;
      const filterType = queryKey[3] as string;
      const filter = {
        inLibraryOnly: filterType === 'inLibrary' ? true : undefined,
        newOnly: filterType === 'new' ? true : undefined,
      };
      return api.getWeeklyDiscoveryByDate(date, filter);
    },
    enabled: sourceMode === 'discover',
    staleTime: 30 * 60 * 1000, // 30 minutes - matches backend cache duration
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

  const bulkUpdate = useMutation({
    mutationFn: ({ issueIds, status }: { issueIds: number[]; status: IssueStatus }) =>
      api.bulkUpdateIssueStatus(issueIds, status),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pulllist'] });
      setSelectedIssues(new Set());
    },
  });

  // Discovery mode mutations
  const addIssueOneOff = useMutation({
    mutationFn: (comicVineIssueId: number) => api.addIssueOneOff(comicVineIssueId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pulllist'] }),
  });

  const addSeriesFromDiscovery = useMutation({
    mutationFn: ({ comicVineVolumeId, markIssueWantedComicVineId, monitoringMode }: {
      comicVineVolumeId: number;
      markIssueWantedComicVineId?: number;
      monitoringMode: SeriesMonitoringMode;
    }) => api.addSeriesFromDiscovery(comicVineVolumeId, markIssueWantedComicVineId, monitoringMode),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pulllist'] });
      setAddSeriesIssue(null);
    },
  });

  // Filter issues by status (library mode)
  const filterIssues = (issues: PullListIssue[]) => {
    if (statusFilter === 'all') return issues;
    return issues.filter(i => i.status === statusFilter);
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

  // Sort library issues
  const sortIssues = (issues: PullListIssue[]): PullListIssue[] => {
    return [...issues].sort((a, b) => {
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

  // Sort and deduplicate discovery issues (ComicVine API sometimes returns duplicates)
  const sortDiscoveryIssues = (issues: DiscoverableIssue[]): DiscoverableIssue[] => {
    // First deduplicate by comicVineIssueId
    const seen = new Set<number>();
    const unique = issues.filter(issue => {
      if (seen.has(issue.comicVineIssueId)) {
        return false;
      }
      seen.add(issue.comicVineIssueId);
      return true;
    });
    
    return unique.sort((a, b) => {
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
  // This prevents showing stale cached data while a new request is in flight
  const isLoading = sourceMode === 'discover' 
    ? (discoveryLoading || discoveryFetching)
    : viewMode === 'week' ? (thisWeekLoading || thisWeekFetching) : 
      viewMode === 'upcoming' ? upcomingLoading : pastLoading;

  // Handle manual refresh with timestamp tracking
  const handleManualRefresh = () => {
    setLastRefresh(new Date());
    if (sourceMode === 'discover') {
      refetchDiscovery();
    } else {
      refetchThisWeek();
    }
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
    if (sourceMode === 'discover') {
      // All Releases empty state
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
      return (
        <div className="empty-state">
          <Calendar size={48} />
          <div className="empty-state-title">No Releases Found</div>
          <div className="empty-state-text">
            No comics are releasing during this week, or the ComicVine API may be temporarily unavailable.
          </div>
          <button 
            className="btn btn-secondary mt-3"
            onClick={handleManualRefresh}
          >
            <RefreshCw size={16} />
            Refresh from ComicVine
          </button>
        </div>
      );
    }

    // My Pull List empty state - based on configuration status
    if (configStatus) {
      switch (configStatus.actionType) {
        case 'ConfigureApiKey':
          return (
            <div className="empty-state">
              <AlertTriangle size={48} className="text-warning" />
              <div className="empty-state-title">ComicVine API Not Configured</div>
              <div className="empty-state-text">
                Configure your ComicVine API key to enable release tracking and discovery.
              </div>
              <div className="empty-state-actions">
                <button 
                  className="btn btn-primary"
                  onClick={() => navigate('/settings?tab=comicvine')}
                >
                  <Settings size={16} />
                  Configure ComicVine
                </button>
              </div>
            </div>
          );
        
        case 'AddSeries':
          return (
            <div className="empty-state">
              <BookOpen size={48} />
              <div className="empty-state-title">No Series in Library</div>
              <div className="empty-state-text">
                Add your first series to start tracking releases.
              </div>
              <div className="empty-state-actions">
                <button 
                  className="btn btn-primary"
                  onClick={() => navigate('/series')}
                >
                  <Plus size={16} />
                  Add Series
                </button>
                <button 
                  className="btn btn-secondary"
                  onClick={() => setSourceMode('discover')}
                >
                  <Globe size={16} />
                  Try All Releases
                </button>
              </div>
            </div>
          );
        
        case 'MatchSeries':
          return (
            <div className="empty-state">
              <LinkIcon size={48} />
              <div className="empty-state-title">Series Not Matched</div>
              <div className="empty-state-text">
                Match your series to ComicVine to track release dates.
              </div>
              <div className="empty-state-actions">
                <button 
                  className="btn btn-primary"
                  onClick={() => navigate('/series')}
                >
                  <LinkIcon size={16} />
                  Match Series
                </button>
                <button 
                  className="btn btn-secondary"
                  onClick={() => setSourceMode('discover')}
                >
                  <Globe size={16} />
                  Try All Releases
                </button>
              </div>
            </div>
          );
        
        case 'TryAllReleases':
          return (
            <div className="empty-state">
              <Calendar size={48} />
              <div className="empty-state-title">No Releases This Week</div>
              <div className="empty-state-text">
                None of your monitored series have releases this week.
              </div>
              <div className="empty-state-actions">
                <button 
                  className="btn btn-primary"
                  onClick={() => setSourceMode('discover')}
                >
                  <Globe size={16} />
                  Discover All Releases
                </button>
              </div>
            </div>
          );
        
        default:
          return (
            <div className="empty-state">
              <Calendar size={48} />
              <div className="empty-state-title">No Releases Found</div>
              <div className="empty-state-text">
                Add some series and match them to ComicVine to see upcoming releases.
              </div>
              <div className="empty-state-actions">
                <button 
                  className="btn btn-secondary"
                  onClick={() => setSourceMode('discover')}
                >
                  <Globe size={16} />
                  Try All Releases
                </button>
              </div>
            </div>
          );
      }
    }

    // Fallback empty state for library mode (when configStatus is not available yet)
    return (
      <div className="empty-state">
        <Calendar size={48} />
        <div className="empty-state-title">No Releases Found</div>
        <div className="empty-state-text">
          Add some series and match them to ComicVine to see upcoming releases.
        </div>
        <div className="empty-state-actions">
          <button 
            className="btn btn-secondary"
            onClick={() => setSourceMode('discover')}
          >
            <Globe size={16} />
            Try All Releases
          </button>
        </div>
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


  // Toggle issue selection (library mode)
  const toggleIssueSelection = (issueId: number) => {
    const newSelected = new Set(selectedIssues);
    if (newSelected.has(issueId)) {
      newSelected.delete(issueId);
    } else {
      newSelected.add(issueId);
    }
    setSelectedIssues(newSelected);
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

  // Render discovery card for grid view
  // Include weekDate in key to prevent React from confusing items across week transitions
  const renderDiscoveryCard = (issue: DiscoverableIssue) => (
    <div 
      key={`${weekDate}-${issue.comicVineIssueId}`} 
      className={`pull-list-card ${issue.isInLibrary ? 'in-library' : 'discoverable'}`}
    >
      <div className="pull-list-card-cover">
        {issue.coverImageUrl ? (
          <img src={issue.coverImageUrl} alt={`${issue.seriesTitle} #${issue.issueNumber}`} />
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

  // Render discovery row for list view
  // Include weekDate in key to prevent React from confusing items across week transitions
  const renderDiscoveryRow = (issue: DiscoverableIssue) => (
    <tr key={`${weekDate}-${issue.comicVineIssueId}`} className={issue.isInLibrary ? 'in-library' : 'discoverable'}>
      <td>
        {issue.coverImageUrl ? (
          <img 
            src={issue.coverImageUrl} 
            alt="" 
            className="pull-list-thumb"
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

  // Render library issue card for grid view
  const renderIssueCard = (issue: PullListIssue) => (
    <div 
      key={issue.issueId} 
      className={`pull-list-card ${selectedIssues.has(issue.issueId) ? 'selected' : ''}`}
      onClick={() => toggleIssueSelection(issue.issueId)}
    >
      <div className="pull-list-card-cover">
        {issue.coverImageUrl ? (
          <img src={issue.coverImageUrl} alt={`${issue.seriesTitle} #${issue.issueNumber}`} />
        ) : (
          <div className="pull-list-card-placeholder">
            <Calendar size={32} />
          </div>
        )}
        <div className="pull-list-card-status">
          {getStatusBadge(issue.status)}
        </div>
        {(issue.isAnnual || issue.isSpecial) && (
          <div className="pull-list-card-special">
            {issue.isAnnual ? 'Annual' : issue.specialType || 'Special'}
          </div>
        )}
      </div>
      <div className="pull-list-card-info">
        <Link to={`/series/${issue.seriesId}`} className="pull-list-card-series" onClick={e => e.stopPropagation()}>
          {issue.seriesTitle}
        </Link>
        <div className="pull-list-card-issue">
          #{issue.issueNumberText || issue.issueNumber}
          {issue.issueTitle && <span className="pull-list-card-title"> - {issue.issueTitle}</span>}
        </div>
        {issue.publisher && <div className="pull-list-card-publisher">{issue.publisher}</div>}
      </div>
      <div className="pull-list-card-actions">
        <button 
          className="btn btn-icon btn-sm" 
          title="Mark as Wanted"
          onClick={(e) => { e.stopPropagation(); markWanted.mutate(issue.issueId); }}
          disabled={issue.status === 'Wanted'}
        >
          <Eye size={14} />
        </button>
        <button 
          className="btn btn-icon btn-sm" 
          title="Mark as Owned"
          onClick={(e) => { e.stopPropagation(); markOwned.mutate(issue.issueId); }}
          disabled={issue.status === 'Owned'}
        >
          <Check size={14} />
        </button>
        <button 
          className="btn btn-icon btn-sm" 
          title="Skip"
          onClick={(e) => { e.stopPropagation(); markSkipped.mutate(issue.issueId); }}
          disabled={issue.status === 'Skipped'}
        >
          <X size={14} />
        </button>
      </div>
    </div>
  );

  // Render library issue row for list view
  const renderIssueRow = (issue: PullListIssue) => (
    <tr key={issue.issueId} className={selectedIssues.has(issue.issueId) ? 'selected' : ''}>
      <td>
        <input 
          type="checkbox" 
          checked={selectedIssues.has(issue.issueId)}
          onChange={() => toggleIssueSelection(issue.issueId)}
        />
      </td>
      <td>
        {issue.coverImageUrl ? (
          <img 
            src={issue.coverImageUrl} 
            alt="" 
            className="pull-list-thumb"
          />
        ) : (
          <div className="pull-list-thumb-placeholder">
            <Calendar size={16} />
          </div>
        )}
      </td>
      <td>
        <Link to={`/series/${issue.seriesId}`} className="text-link">
          {issue.seriesTitle}
        </Link>
      </td>
      <td>
        #{issue.issueNumberText || issue.issueNumber}
        {issue.issueTitle && <span className="text-muted"> - {issue.issueTitle}</span>}
      </td>
      <td>{issue.publisher || '-'}</td>
      <td>{issue.storeDate ? formatDate(issue.storeDate) : '-'}</td>
      <td>{getStatusBadge(issue.status)}</td>
      <td className="table-actions">
        <button 
          className="btn btn-icon btn-sm" 
          title="Mark as Wanted"
          onClick={() => markWanted.mutate(issue.issueId)}
          disabled={issue.status === 'Wanted'}
        >
          <Eye size={14} />
        </button>
        <button 
          className="btn btn-icon btn-sm" 
          title="Mark as Owned"
          onClick={() => markOwned.mutate(issue.issueId)}
          disabled={issue.status === 'Owned'}
        >
          <Check size={14} />
        </button>
        <button 
          className="btn btn-icon btn-sm" 
          title="Skip"
          onClick={() => markSkipped.mutate(issue.issueId)}
          disabled={issue.status === 'Skipped'}
        >
          <X size={14} />
        </button>
      </td>
    </tr>
  );

  // Render discovery section
  const renderDiscoverySection = (data: WeeklyDiscoveryList) => (
    <div className="pull-list-week-section">
      <div className="pull-list-week-header">
        <h3>{formatReleaseDay(data.releaseDay)}</h3>
        <div className="pull-list-week-stats">
          <span className="stat">{data.totalCount} releases</span>
          <span className="stat in-library">{data.inLibraryCount} in library</span>
          <span className="stat new">{data.newCount} new</span>
        </div>
      </div>
      
      {data.issues.length === 0 ? (
        <div className="empty-state-small">No releases this week</div>
      ) : displayMode === 'grid' ? (
        <div className="pull-list-grid">
          {sortDiscoveryIssues(data.issues).map(renderDiscoveryCard)}
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
              {sortDiscoveryIssues(data.issues).map(renderDiscoveryRow)}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );

  // Render library week section
  const renderWeekSection = (week: WeeklyPullList, index?: number) => {
    const filtered = filterIssues(week.issues);
    const sorted = sortIssues(filtered);
    
    return (
      <div key={index ?? 0} className="pull-list-week-section">
        <div className="pull-list-week-header">
          <h3>{formatReleaseDay(week.releaseDay)}</h3>
          <div className="pull-list-week-stats">
            <span className="stat">{sorted.length} issues</span>
            <span className="stat wanted">{week.wantedCount} wanted</span>
            <span className="stat owned">{week.ownedCount} owned</span>
          </div>
        </div>
        
        {sorted.length === 0 ? (
          <div className="empty-state-small">No releases this week</div>
        ) : displayMode === 'grid' ? (
          <div className="pull-list-grid">
            {sorted.map(renderIssueCard)}
          </div>
        ) : (
          <div className="table-container">
            <table className="table">
              <thead>
                <tr>
                  <th style={{ width: 40 }}>
                    <input 
                      type="checkbox" 
                      onChange={() => {
                        const filteredIds = new Set(sorted.map(i => i.issueId));
                        const allSelected = sorted.every(i => selectedIssues.has(i.issueId));
                        if (allSelected) {
                          setSelectedIssues(new Set([...selectedIssues].filter(id => !filteredIds.has(id))));
                        } else {
                          setSelectedIssues(new Set([...selectedIssues, ...filteredIds]));
                        }
                      }}
                      checked={sorted.length > 0 && sorted.every(i => selectedIssues.has(i.issueId))}
                    />
                  </th>
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
                {sorted.map(renderIssueRow)}
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
          {/* Source mode toggle */}
          <div className="toolbar-group btn-group">
            <button 
              className={`btn ${sourceMode === 'library' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setSourceMode('library')}
              title="My Pull List - shows issues from your library"
            >
              <Library size={16} />
              My Pull List
            </button>
            <button 
              className={`btn ${sourceMode === 'discover' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setSourceMode('discover')}
              title="All Releases - discover all comics releasing this week"
            >
              <Globe size={16} />
              All Releases
            </button>
          </div>

          {/* Week/View navigation */}
          <div className="toolbar-group">
            <button 
              className="btn btn-icon" 
              onClick={() => {
                if (sourceMode === 'library' && viewMode !== 'week') {
                  setViewMode('week');
                }
                setWeekOffset(o => o - 1);
              }}
              title="Previous Week"
            >
              <ChevronLeft size={18} />
            </button>
            {sourceMode === 'library' ? (
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
            ) : (
              <button 
                className="btn btn-secondary"
                onClick={() => setWeekOffset(0)}
                disabled={weekOffset === 0}
              >
                This Week
              </button>
            )}
            <button 
              className="btn btn-icon" 
              onClick={() => {
                if (sourceMode === 'library' && viewMode !== 'week') {
                  setViewMode('week');
                }
                setWeekOffset(o => o + 1);
              }}
              title="Next Week"
            >
              <ChevronRight size={18} />
            </button>
          </div>

          {/* Filters */}
          <div className="toolbar-group">
            <Filter size={16} className="text-muted" />
            {sourceMode === 'library' ? (
              <select 
                className="select"
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value as IssueStatus | 'all')}
              >
                <option value="all">All Statuses</option>
                <option value="Wanted">Wanted</option>
                <option value="Owned">Owned</option>
                <option value="Skipped">Skipped</option>
                <option value="Missing">Missing</option>
              </select>
            ) : (
              <select 
                className="select"
                value={discoveryFilter}
                onChange={(e) => setDiscoveryFilter(e.target.value as 'all' | 'new' | 'inLibrary')}
              >
                <option value="all">All Releases</option>
                <option value="new">New to Me</option>
                <option value="inLibrary">In My Library</option>
              </select>
            )}
          </div>

          <div className="toolbar-spacer" />

          {/* Bulk actions (library mode only) */}
          {sourceMode === 'library' && selectedIssues.size > 0 && (
            <div className="toolbar-group">
              <span className="text-muted">{selectedIssues.size} selected</span>
              <button 
                className="btn btn-sm"
                onClick={() => bulkUpdate.mutate({ 
                  issueIds: Array.from(selectedIssues), 
                  status: 'Wanted' 
                })}
              >
                Mark Wanted
              </button>
              <button 
                className="btn btn-sm"
                onClick={() => bulkUpdate.mutate({ 
                  issueIds: Array.from(selectedIssues), 
                  status: 'Owned' 
                })}
              >
                Mark Owned
              </button>
              <button 
                className="btn btn-sm"
                onClick={() => bulkUpdate.mutate({ 
                  issueIds: Array.from(selectedIssues), 
                  status: 'Skipped' 
                })}
              >
                Skip
              </button>
              <button 
                className="btn btn-sm btn-secondary"
                onClick={() => setSelectedIssues(new Set())}
              >
                Clear
              </button>
            </div>
          )}

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
        ) : sourceMode === 'discover' && discovery ? (
          renderDiscoverySection(discovery)
        ) : sourceMode === 'library' && viewMode === 'week' && thisWeek ? (
          renderWeekSection(thisWeek)
        ) : sourceMode === 'library' && viewMode === 'upcoming' && upcoming ? (
          <div className="pull-list-weeks">
            {upcoming.map((week, i) => renderWeekSection(week, i))}
          </div>
        ) : sourceMode === 'library' && viewMode === 'past' && past ? (
          <div className="pull-list-weeks">
            {past.map((week, i) => renderWeekSection(week, i))}
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
            });
          }}
        />
      )}
    </>
  );
}
