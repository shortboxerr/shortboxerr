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
  Filter,
  Plus,
  BookPlus,
  Library,
  Globe
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
import { Link } from 'react-router-dom';

type ViewMode = 'week' | 'upcoming' | 'past';
type DisplayMode = 'list' | 'grid';
type SourceMode = 'library' | 'discover';

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
  const [viewMode, setViewMode] = useState<ViewMode>('week');
  const [displayMode, setDisplayMode] = useState<DisplayMode>('list');
  const [sourceMode, setSourceMode] = useState<SourceMode>('library');
  const [weekOffset, setWeekOffset] = useState(0);
  const [selectedIssues, setSelectedIssues] = useState<Set<number>>(new Set());
  const [statusFilter, setStatusFilter] = useState<IssueStatus | 'all'>('all');
  const [addSeriesIssue, setAddSeriesIssue] = useState<DiscoverableIssue | null>(null);
  const [discoveryFilter, setDiscoveryFilter] = useState<'all' | 'new' | 'inLibrary'>('all');

  // Calculate week date based on offset
  const getWeekDate = () => {
    const date = new Date();
    date.setDate(date.getDate() + (weekOffset * 7));
    return date.toISOString().split('T')[0];
  };

  // Library mode queries
  const { data: thisWeek, isLoading: thisWeekLoading, refetch: refetchThisWeek } = useQuery({
    queryKey: ['pulllist', 'week', weekOffset],
    queryFn: () => weekOffset === 0 
      ? api.getPullListThisWeek() 
      : api.getPullListWeek(getWeekDate()),
    enabled: viewMode === 'week' && sourceMode === 'library',
  });

  const { data: upcoming, isLoading: upcomingLoading } = useQuery({
    queryKey: ['pulllist', 'upcoming'],
    queryFn: () => api.getPullListUpcoming(4),
    enabled: viewMode === 'upcoming' && sourceMode === 'library',
  });

  const { data: past, isLoading: pastLoading } = useQuery({
    queryKey: ['pulllist', 'past'],
    queryFn: () => api.getPullListPast(4),
    enabled: viewMode === 'past' && sourceMode === 'library',
  });

  // Discovery mode queries
  const { data: discovery, isLoading: discoveryLoading, refetch: refetchDiscovery } = useQuery({
    queryKey: ['pulllist', 'discovery', weekOffset, discoveryFilter],
    queryFn: () => {
      const filter = {
        inLibraryOnly: discoveryFilter === 'inLibrary' ? true : undefined,
        newOnly: discoveryFilter === 'new' ? true : undefined,
      };
      return weekOffset === 0
        ? api.getWeeklyDiscovery(filter)
        : api.getWeeklyDiscoveryByDate(getWeekDate(), filter);
    },
    enabled: sourceMode === 'discover',
  });

  const { data: stats } = useQuery({
    queryKey: ['pulllist', 'stats'],
    queryFn: () => api.getPullListStats(),
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

  const isLoading = sourceMode === 'discover' 
    ? discoveryLoading 
    : viewMode === 'week' ? thisWeekLoading : 
      viewMode === 'upcoming' ? upcomingLoading : pastLoading;

  // Format date for display
  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-US', { 
      month: 'short', 
      day: 'numeric',
      year: date.getFullYear() !== new Date().getFullYear() ? 'numeric' : undefined 
    });
  };

  const formatWeekRange = (start: string, end: string) => {
    return `${formatDate(start)} - ${formatDate(end)}`;
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
  const renderDiscoveryCard = (issue: DiscoverableIssue) => (
    <div 
      key={issue.comicVineIssueId} 
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
  const renderDiscoveryRow = (issue: DiscoverableIssue) => (
    <tr key={issue.comicVineIssueId} className={issue.isInLibrary ? 'in-library' : 'discoverable'}>
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
        <h3>{formatWeekRange(data.weekStart, data.weekEnd)}</h3>
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
          {data.issues.map(renderDiscoveryCard)}
        </div>
      ) : (
        <div className="table-container">
          <table className="table">
            <thead>
              <tr>
                <th style={{ width: 50 }}></th>
                <th>Series</th>
                <th>Issue</th>
                <th>Publisher</th>
                <th>Release</th>
                <th>Status</th>
                <th className="table-actions"></th>
              </tr>
            </thead>
            <tbody>
              {data.issues.map(renderDiscoveryRow)}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );

  // Render library week section
  const renderWeekSection = (week: WeeklyPullList, index?: number) => {
    const filtered = filterIssues(week.issues);
    
    return (
      <div key={index ?? 0} className="pull-list-week-section">
        <div className="pull-list-week-header">
          <h3>{formatWeekRange(week.weekStart, week.weekEnd)}</h3>
          <div className="pull-list-week-stats">
            <span className="stat">{filtered.length} issues</span>
            <span className="stat wanted">{week.wantedCount} wanted</span>
            <span className="stat owned">{week.ownedCount} owned</span>
          </div>
        </div>
        
        {filtered.length === 0 ? (
          <div className="empty-state-small">No releases this week</div>
        ) : displayMode === 'grid' ? (
          <div className="pull-list-grid">
            {filtered.map(renderIssueCard)}
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
                        const filteredIds = new Set(filtered.map(i => i.issueId));
                        const allSelected = filtered.every(i => selectedIssues.has(i.issueId));
                        if (allSelected) {
                          setSelectedIssues(new Set([...selectedIssues].filter(id => !filteredIds.has(id))));
                        } else {
                          setSelectedIssues(new Set([...selectedIssues, ...filteredIds]));
                        }
                      }}
                      checked={filtered.length > 0 && filtered.every(i => selectedIssues.has(i.issueId))}
                    />
                  </th>
                  <th style={{ width: 50 }}></th>
                  <th>Series</th>
                  <th>Issue</th>
                  <th>Publisher</th>
                  <th>Release</th>
                  <th>Status</th>
                  <th className="table-actions"></th>
                </tr>
              </thead>
              <tbody>
                {filtered.map(renderIssueRow)}
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

          {/* View mode tabs (library only) */}
          {sourceMode === 'library' && (
            <div className="toolbar-group btn-group">
              <button 
                className={`btn ${viewMode === 'week' ? 'btn-accent' : 'btn-secondary'}`}
                onClick={() => setViewMode('week')}
              >
                This Week
              </button>
              <button 
                className={`btn ${viewMode === 'upcoming' ? 'btn-accent' : 'btn-secondary'}`}
                onClick={() => setViewMode('upcoming')}
              >
                Upcoming
              </button>
              <button 
                className={`btn ${viewMode === 'past' ? 'btn-accent' : 'btn-secondary'}`}
                onClick={() => setViewMode('past')}
              >
                Past
              </button>
            </div>
          )}

          {/* Week navigation */}
          {(sourceMode === 'discover' || viewMode === 'week') && (
            <div className="toolbar-group">
              <button 
                className="btn btn-icon" 
                onClick={() => setWeekOffset(o => o - 1)}
                title="Previous Week"
              >
                <ChevronLeft size={18} />
              </button>
              <button 
                className="btn btn-secondary"
                onClick={() => setWeekOffset(0)}
                disabled={weekOffset === 0}
              >
                Today
              </button>
              <button 
                className="btn btn-icon" 
                onClick={() => setWeekOffset(o => o + 1)}
                title="Next Week"
              >
                <ChevronRight size={18} />
              </button>
            </div>
          )}

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

          {/* Display mode toggle */}
          <div className="toolbar-group btn-group">
            <button 
              className={`btn btn-icon ${displayMode === 'list' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setDisplayMode('list')}
              title="List View"
            >
              <List size={18} />
            </button>
            <button 
              className={`btn btn-icon ${displayMode === 'grid' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setDisplayMode('grid')}
              title="Cover View"
            >
              <Grid size={18} />
            </button>
          </div>

          <button 
            className="btn btn-icon" 
            onClick={() => sourceMode === 'discover' ? refetchDiscovery() : refetchThisWeek()} 
            title="Refresh"
          >
            <RefreshCw size={18} />
          </button>
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
          <div className="empty-state">
            <Calendar size={48} />
            <div className="empty-state-title">No releases found</div>
            <div className="empty-state-text">
              {sourceMode === 'discover' 
                ? 'ComicVine API may be unavailable or no comics are releasing this week.'
                : 'Add some series and match them to ComicVine to see upcoming releases.'}
            </div>
          </div>
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
