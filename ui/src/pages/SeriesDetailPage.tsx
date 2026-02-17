import { useState, useMemo, useEffect } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  ArrowLeft, ExternalLink, RefreshCw, Calendar, BookOpen, HardDrive, 
  Check, X, Clock, Grid, List, Filter, SortAsc, SortDesc, Star, Zap, Trash2
} from 'lucide-react';
import { api } from '../api/client';
import type { Issue, IssueStatus } from '../api/client';

type ViewMode = 'cover' | 'list';
type SortKey = 'issueNumber' | 'releaseDate' | 'status' | 'title';
type SortDir = 'asc' | 'desc';
type StatusFilter = 'all' | 'owned' | 'wanted' | 'missing' | 'skipped';

export function SeriesDetailPage() {
  const { id } = useParams<{ id: string }>();
  const seriesId = parseInt(id ?? '0', 10);
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  // Load UI settings for view preference
  const { data: uiSettings } = useQuery({
    queryKey: ['settings', 'ui'],
    queryFn: () => api.getUiSettings(),
  });

  // View state - initialize from settings
  const [viewMode, setViewMode] = useState<ViewMode>('cover');
  const [sortKey, setSortKey] = useState<SortKey>('issueNumber');
  const [sortDir, setSortDir] = useState<SortDir>('asc');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [selectedIssues, setSelectedIssues] = useState<Set<number>>(new Set());

  // Sync view mode from settings when loaded
  useEffect(() => {
    if (uiSettings?.issueViewMode) {
      setViewMode(uiSettings.issueViewMode);
    }
  }, [uiSettings?.issueViewMode]);

  // Save view preference mutation
  const saveViewPreference = useMutation({
    mutationFn: async (newViewMode: ViewMode) => {
      await api.updateUiSettings({ issueViewMode: newViewMode });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings', 'ui'] });
    },
  });

  // Handle view mode change with persistence
  const handleViewModeChange = (newMode: ViewMode) => {
    setViewMode(newMode);
    saveViewPreference.mutate(newMode);
  };

  // Issue status update mutation
  const updateIssueStatus = useMutation({
    mutationFn: async ({ issueIds, status }: { issueIds: number[]; status: IssueStatus }) => {
      return api.bulkUpdateIssueStatus(issueIds, status);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['series', seriesId, 'issues'] });
      queryClient.invalidateQueries({ queryKey: ['series', seriesId] });
      // Also invalidate dashboard/pulllist stats so counts update immediately
      queryClient.invalidateQueries({ queryKey: ['pulllist', 'stats'] });
      queryClient.invalidateQueries({ queryKey: ['pulllist', 'week'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard-stats'] });
      setSelectedIssues(new Set());
    },
  });

  // Action handlers
  // Note: "Owned" status is set automatically by the import process when a file is added
  // Only "Wanted" and "Skipped" can be toggled manually (Mylar3 parity)
  const handleMarkAsWanted = (issueIds: number[]) => {
    updateIssueStatus.mutate({ issueIds, status: 'Wanted' });
  };

  const handleMarkAsSkipped = (issueIds: number[]) => {
    updateIssueStatus.mutate({ issueIds, status: 'Skipped' });
  };

  const handleBulkAction = (action: 'wanted' | 'skipped') => {
    const ids = Array.from(selectedIssues);
    if (ids.length === 0) return;
    
    switch (action) {
      case 'wanted':
        handleMarkAsWanted(ids);
        break;
      case 'skipped':
        handleMarkAsSkipped(ids);
        break;
    }
  };

  // Refresh this series metadata mutation
  const refreshMetadata = useMutation({
    mutationFn: async () => {
      return api.refreshSeriesMetadata(seriesId, true);
    },
    onSuccess: () => {
      // Invalidate series and issues queries to refetch updated data
      queryClient.invalidateQueries({ queryKey: ['series', seriesId] });
      queryClient.invalidateQueries({ queryKey: ['series', seriesId, 'issues'] });
    },
  });

  const handleRefreshMetadata = () => {
    refreshMetadata.mutate();
  };

  // Delete series mutation
  const deleteSeries = useMutation({
    mutationFn: () => api.deleteSeries(seriesId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['series'] });
      await queryClient.invalidateQueries({ queryKey: ['dashboard-stats'] });
      navigate('/series');
    },
  });

  const handleDeleteSeries = () => {
    if (confirm(`Delete "${series?.title}"? This will remove the series and all its issues from your library. This cannot be undone.`)) {
      deleteSeries.mutate();
    }
  };

  const { data: series, isLoading: isLoadingSeries } = useQuery({
    queryKey: ['series', seriesId],
    queryFn: () => api.getSeriesById(seriesId),
    enabled: seriesId > 0,
  });

  const { data: issuesData, isLoading: isLoadingIssues } = useQuery({
    queryKey: ['series', seriesId, 'issues', sortKey, sortDir],
    queryFn: () => api.getSeriesIssues(seriesId, { pageSize: 500, sortKey, sortDir }),
    enabled: seriesId > 0,
  });

  const allIssues = issuesData?.items ?? [];

  // Filter issues based on status
  const filteredIssues = useMemo(() => {
    if (statusFilter === 'all') return allIssues;
    
    return allIssues.filter(issue => {
      const status = getIssueStatus(issue);
      return status === statusFilter;
    });
  }, [allIssues, statusFilter]);

  // Counts for stats
  const ownedCount = allIssues.filter(i => i.hasFile).length;
  const wantedCount = allIssues.filter(i => i.monitored && !i.hasFile && !i.satisfiedByEdition).length;
  const missingCount = allIssues.filter(i => !i.hasFile && !i.satisfiedByEdition && !i.monitored).length;

  // Selection handlers
  const toggleIssueSelection = (issueId: number) => {
    const newSelected = new Set(selectedIssues);
    if (newSelected.has(issueId)) {
      newSelected.delete(issueId);
    } else {
      newSelected.add(issueId);
    }
    setSelectedIssues(newSelected);
  };

  const selectAllVisible = () => {
    setSelectedIssues(new Set(filteredIssues.map(i => i.id)));
  };

  const clearSelection = () => {
    setSelectedIssues(new Set());
  };

  // Sort toggle
  const toggleSort = (key: SortKey) => {
    if (sortKey === key) {
      setSortDir(sortDir === 'asc' ? 'desc' : 'asc');
    } else {
      setSortKey(key);
      setSortDir('asc');
    }
  };

  if (isLoadingSeries) {
    return (
      <div className="page-content">
        <div className="loading"><div className="spinner" /></div>
      </div>
    );
  }

  if (!series) {
    return (
      <div className="page-content">
        <div className="empty-state">
          <BookOpen size={48} />
          <div className="empty-state-title">Series not found</div>
          <div className="empty-state-text">
            The series you're looking for doesn't exist or has been removed.
          </div>
          <Link to="/series" className="btn btn-primary" style={{ marginTop: '16px' }}>
            <ArrowLeft size={16} />
            Back to Series
          </Link>
        </div>
      </div>
    );
  }

  const placeholderCover = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="200" height="300" viewBox="0 0 200 300"%3E%3Crect fill="%232a2d35" width="200" height="300"/%3E%3Ctext fill="%236b7280" font-family="sans-serif" font-size="14" x="100" y="150" text-anchor="middle"%3ENo Cover%3C/text%3E%3C/svg%3E';

  return (
    <>
      <header className="page-header">
        <Link to="/series" className="btn btn-icon" title="Back to Series">
          <ArrowLeft size={20} />
        </Link>
        <h1 className="page-title">{series.title}</h1>
        <div className="toolbar-group">
          <button 
            className="btn btn-icon" 
            title="Refresh Series Metadata from ComicVine"
            onClick={handleRefreshMetadata}
            disabled={refreshMetadata.isPending}
          >
            <RefreshCw size={18} className={refreshMetadata.isPending ? 'spinning' : ''} />
          </button>
          <button 
            className="btn btn-icon btn-danger" 
            title="Delete Series"
            onClick={handleDeleteSeries}
            disabled={deleteSeries.isPending}
          >
            <Trash2 size={18} />
          </button>
        </div>
      </header>

      <div className="page-content">
        {/* Series Header */}
        <div className="series-detail-header">
          <img
            src={series.coverImageUrl || placeholderCover}
            alt={series.title}
            className="series-detail-cover"
            onError={(e) => {
              (e.target as HTMLImageElement).src = placeholderCover;
            }}
          />
          <div className="series-detail-info">
            <div className="series-detail-meta">
              {series.publisher && (
                <span className="series-detail-publisher">{series.publisher}</span>
              )}
              {series.startYear && (
                <span className="series-detail-year">
                  {series.startYear}{series.endYear && series.endYear !== series.startYear ? ` - ${series.endYear}` : ''}
                </span>
              )}
              <span className={`badge badge-${getStatusBadge(series.status)}`}>
                {series.status}
              </span>
              {series.monitored ? (
                <span className="badge badge-success">Monitored</span>
              ) : (
                <span className="badge badge-muted">Not Monitored</span>
              )}
            </div>

            {series.overview && (
              <p className="series-detail-overview">{stripHtml(series.overview)}</p>
            )}

            <div className="series-detail-stats">
              <div className="series-detail-stat">
                <BookOpen size={16} />
                <span>{series.issueCount} issues</span>
              </div>
              <div className="series-detail-stat">
                <HardDrive size={16} />
                <span>{series.issueFileCount} files</span>
              </div>
              {series.totalIssueCount && series.totalIssueCount !== series.issueCount && (
                <div className="series-detail-stat">
                  <Clock size={16} />
                  <span>{series.totalIssueCount} on ComicVine</span>
                </div>
              )}
            </div>

            {series.comicVineUrl && (
              <a
                href={series.comicVineUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="series-detail-link"
              >
                <ExternalLink size={14} />
                View on ComicVine
              </a>
            )}

            {series.metadataLastRefreshed && (
              <div className="series-detail-refreshed">
                Last updated: {new Date(series.metadataLastRefreshed).toLocaleDateString()}
              </div>
            )}
          </div>
        </div>

        {/* Issues Section */}
        <div className="series-detail-section">
          <div className="series-detail-section-header">
            <h2>Issues</h2>
            <div className="series-detail-section-stats">
              <span className="badge badge-success">{ownedCount} owned</span>
              {wantedCount > 0 && <span className="badge badge-warning">{wantedCount} wanted</span>}
              {missingCount > 0 && <span className="badge badge-muted">{missingCount} missing</span>}
            </div>
          </div>

          {/* Issues Toolbar */}
          <div className="issues-toolbar">
            {/* View Toggle */}
            <div className="view-toggle">
              <button 
                className={`btn btn-icon ${viewMode === 'cover' ? 'active' : ''}`}
                onClick={() => handleViewModeChange('cover')}
                title="Cover View"
              >
                <Grid size={18} />
              </button>
              <button 
                className={`btn btn-icon ${viewMode === 'list' ? 'active' : ''}`}
                onClick={() => handleViewModeChange('list')}
                title="List View"
              >
                <List size={18} />
              </button>
            </div>

            {/* Filter Dropdown */}
            <div className="filter-dropdown">
              <Filter size={16} />
              <select 
                value={statusFilter} 
                onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}
                className="filter-select"
              >
                <option value="all">All Issues</option>
                <option value="owned">Owned ({ownedCount})</option>
                <option value="wanted">Wanted ({wantedCount})</option>
                <option value="missing">Missing</option>
                <option value="skipped">Skipped</option>
              </select>
            </div>

            {/* Sort Dropdown */}
            <div className="sort-dropdown">
              {sortDir === 'asc' ? <SortAsc size={16} /> : <SortDesc size={16} />}
              <select 
                value={sortKey} 
                onChange={(e) => toggleSort(e.target.value as SortKey)}
                className="sort-select"
              >
                <option value="issueNumber">Issue #</option>
                <option value="releaseDate">Release Date</option>
                <option value="title">Title</option>
                <option value="status">Status</option>
              </select>
              <button 
                className="btn btn-icon btn-sm"
                onClick={() => setSortDir(sortDir === 'asc' ? 'desc' : 'asc')}
                title={sortDir === 'asc' ? 'Ascending' : 'Descending'}
              >
                {sortDir === 'asc' ? <SortAsc size={14} /> : <SortDesc size={14} />}
              </button>
            </div>

            {/* Bulk Selection */}
            {selectedIssues.size > 0 && (
              <div className="bulk-actions">
                <span className="selection-count">{selectedIssues.size} selected</span>
                <button 
                  className="btn btn-sm btn-primary" 
                  onClick={() => handleBulkAction('wanted')}
                  disabled={updateIssueStatus.isPending}
                  title="Mark selected as Wanted"
                >
                  <Clock size={14} />
                  Wanted
                </button>
                <button 
                  className="btn btn-sm btn-muted" 
                  onClick={() => handleBulkAction('skipped')}
                  disabled={updateIssueStatus.isPending}
                  title="Skip selected issues"
                >
                  <X size={14} />
                  Skip
                </button>
                <button className="btn btn-sm" onClick={clearSelection}>Clear</button>
              </div>
            )}
          </div>

          {/* Issues Display */}
          {isLoadingIssues ? (
            <div className="loading"><div className="spinner" /></div>
          ) : filteredIssues.length === 0 ? (
            <div className="empty-state" style={{ padding: '40px 20px' }}>
              <BookOpen size={48} style={{ opacity: 0.3 }} />
              <div className="empty-state-title">
                {statusFilter === 'all' ? 'No issues found' : `No ${statusFilter} issues`}
              </div>
              <div className="empty-state-text">
                {statusFilter === 'all' 
                  ? "This series doesn't have any issues yet."
                  : `There are no issues with status "${statusFilter}".`}
              </div>
            </div>
          ) : viewMode === 'cover' ? (
            <div className="issues-grid">
              {filteredIssues.map((issue) => (
                <IssueCoverCard 
                  key={issue.id} 
                  issue={issue} 
                  selected={selectedIssues.has(issue.id)}
                  onSelect={() => toggleIssueSelection(issue.id)}
                  onMarkWanted={() => handleMarkAsWanted([issue.id])}
                  onMarkSkipped={() => handleMarkAsSkipped([issue.id])}
                  isUpdating={updateIssueStatus.isPending}
                />
              ))}
            </div>
          ) : (
            <IssueListView 
              issues={filteredIssues}
              selectedIds={selectedIssues}
              onSelect={toggleIssueSelection}
              onSelectAll={selectAllVisible}
              sortKey={sortKey}
              sortDir={sortDir}
              onSort={toggleSort}
              onMarkWanted={handleMarkAsWanted}
              onMarkSkipped={handleMarkAsSkipped}
              isUpdating={updateIssueStatus.isPending}
            />
          )}
        </div>
      </div>
    </>
  );
}

// === Issue Status Helper ===
function getIssueStatus(issue: Issue): 'owned' | 'wanted' | 'missing' | 'skipped' | 'edition' {
  if (issue.hasFile) return 'owned';
  if (issue.satisfiedByEdition) return 'edition';
  if (issue.monitored) return 'wanted';
  return 'skipped';
}

// === Cover Card Component ===
interface IssueCoverCardProps {
  issue: Issue;
  selected: boolean;
  onSelect: () => void;
  onMarkWanted: () => void;
  onMarkSkipped: () => void;
  isUpdating: boolean;
}

function IssueCoverCard({ issue, selected, onSelect, onMarkWanted, onMarkSkipped, isUpdating }: IssueCoverCardProps) {
  const [showActions, setShowActions] = useState(false);
  const placeholderCover = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="100" height="150" viewBox="0 0 100 150"%3E%3Crect fill="%232a2d35" width="100" height="150"/%3E%3Ctext fill="%236b7280" font-family="sans-serif" font-size="10" x="50" y="75" text-anchor="middle"%3ENo Cover%3C/text%3E%3C/svg%3E';
  
  const status = getIssueStatus(issue);

  const handleOpenComicVine = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (issue.comicVineUrl) {
      window.open(issue.comicVineUrl, '_blank', 'noopener,noreferrer');
    }
  };
  
  return (
    <div 
      className={`issue-card issue-card-${status} ${selected ? 'selected' : ''}`}
      onMouseEnter={() => setShowActions(true)}
      onMouseLeave={() => setShowActions(false)}
    >
      <div className="issue-card-cover-wrapper" onClick={onSelect}>
        <img
          src={issue.coverImageUrl || placeholderCover}
          alt={`Issue ${issue.displayNumber}`}
          className="issue-card-cover"
          onError={(e) => {
            (e.target as HTMLImageElement).src = placeholderCover;
          }}
        />
        <div className="issue-card-status">
          {status === 'owned' && <Check size={14} />}
          {status === 'edition' && <BookOpen size={14} />}
          {status === 'wanted' && <Clock size={14} />}
          {status === 'skipped' && <X size={14} />}
        </div>
        {selected && <div className="issue-card-selected"><Check size={16} /></div>}
        
        {/* Special Issue Badges */}
        {(issue.isAnnual || issue.isSpecial) && (
          <div className="issue-card-badges">
            {issue.isAnnual && (
              <span className="issue-badge issue-badge-annual" title="Annual">
                <Star size={10} />
              </span>
            )}
            {issue.isSpecial && (
              <span className="issue-badge issue-badge-special" title={issue.specialType || 'Special'}>
                <Zap size={10} />
              </span>
            )}
          </div>
        )}

        {/* Hover Actions Overlay */}
        {showActions && (
          <div className="issue-card-actions" onClick={(e) => e.stopPropagation()}>
            {issue.comicVineUrl && (
              <button 
                className="btn btn-icon btn-sm btn-action btn-action-link" 
                onClick={handleOpenComicVine}
                title="View on ComicVine"
              >
                <ExternalLink size={14} />
              </button>
            )}
            {/* Status toggle buttons - Mylar3 parity: can mark ANY issue as Wanted/Skipped */}
            {/* Marking an owned issue as Wanted triggers a re-search (replace/upgrade) */}
            {status !== 'wanted' && (
              <button 
                className="btn btn-icon btn-sm btn-action" 
                onClick={onMarkWanted}
                disabled={isUpdating}
                title={status === 'owned' ? "Re-search for this issue" : "Mark as Wanted"}
              >
                <Clock size={14} />
              </button>
            )}
            {status !== 'skipped' && (
              <button 
                className="btn btn-icon btn-sm btn-action" 
                onClick={onMarkSkipped}
                disabled={isUpdating}
                title="Skip this issue"
              >
                <X size={14} />
              </button>
            )}
          </div>
        )}
      </div>
      <div className="issue-card-info">
        <div className="issue-card-number">{issue.displayNumber}</div>
        {issue.title && <div className="issue-card-title" title={issue.title}>{issue.title}</div>}
        {(issue.releaseDate || issue.storeDate) && (
          <div className="issue-card-date">
            <Calendar size={10} />
            {formatDate(issue.storeDate || issue.releaseDate)}
          </div>
        )}
        {/* Story Arc Tags */}
        {issue.storyArcs && issue.storyArcs.length > 0 && (
          <div className="issue-card-arcs">
            {issue.storyArcs.slice(0, 2).map((arc, idx) => (
              <span key={idx} className="arc-tag" title={arc}>{arc}</span>
            ))}
            {issue.storyArcs.length > 2 && (
              <span className="arc-tag arc-tag-more">+{issue.storyArcs.length - 2}</span>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

// === List View Component ===
interface IssueListViewProps {
  issues: Issue[];
  selectedIds: Set<number>;
  onSelect: (id: number) => void;
  onSelectAll: () => void;
  sortKey: SortKey;
  sortDir: SortDir;
  onSort: (key: SortKey) => void;
  onMarkWanted: (ids: number[]) => void;
  onMarkSkipped: (ids: number[]) => void;
  isUpdating: boolean;
}

function IssueListView({ 
  issues, selectedIds, onSelect, onSelectAll, sortKey, sortDir, onSort,
  onMarkWanted, onMarkSkipped, isUpdating 
}: IssueListViewProps) {
  const SortIcon = sortDir === 'asc' ? SortAsc : SortDesc;
  
  return (
    <div className="issues-table-wrapper">
      <table className="issues-table">
        <thead>
          <tr>
            <th className="col-checkbox">
              <input 
                type="checkbox" 
                checked={selectedIds.size === issues.length && issues.length > 0}
                onChange={onSelectAll}
              />
            </th>
            <th className="col-number sortable" onClick={() => onSort('issueNumber')}>
              # {sortKey === 'issueNumber' && <SortIcon size={12} />}
            </th>
            <th className="col-title sortable" onClick={() => onSort('title')}>
              Title {sortKey === 'title' && <SortIcon size={12} />}
            </th>
            <th className="col-date sortable" onClick={() => onSort('releaseDate')}>
              Release Date {sortKey === 'releaseDate' && <SortIcon size={12} />}
            </th>
            <th className="col-status sortable" onClick={() => onSort('status')}>
              Status {sortKey === 'status' && <SortIcon size={12} />}
            </th>
            <th className="col-tags">Tags</th>
            <th className="col-actions">Actions</th>
          </tr>
        </thead>
        <tbody>
          {issues.map((issue) => (
            <IssueListRow 
              key={issue.id} 
              issue={issue} 
              selected={selectedIds.has(issue.id)}
              onSelect={() => onSelect(issue.id)}
              onMarkWanted={() => onMarkWanted([issue.id])}
              onMarkSkipped={() => onMarkSkipped([issue.id])}
              isUpdating={isUpdating}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}

// === List Row Component ===
interface IssueListRowProps {
  issue: Issue;
  selected: boolean;
  onSelect: () => void;
  onMarkWanted: () => void;
  onMarkSkipped: () => void;
  isUpdating: boolean;
}

function IssueListRow({ issue, selected, onSelect, onMarkWanted, onMarkSkipped, isUpdating }: IssueListRowProps) {
  const status = getIssueStatus(issue);
  const statusLabels: Record<string, string> = {
    owned: 'Owned',
    wanted: 'Wanted',
    missing: 'Missing',
    skipped: 'Skipped',
    edition: 'In Edition'
  };
  
  return (
    <tr className={`issue-row issue-row-${status} ${selected ? 'selected' : ''}`}>
      <td className="col-checkbox">
        <input type="checkbox" checked={selected} onChange={onSelect} />
      </td>
      <td className="col-number">
        <span className="issue-number">{issue.displayNumber}</span>
      </td>
      <td className="col-title">
        <div className="issue-title-cell">
          {issue.comicVineUrl ? (
            <a 
              href={issue.comicVineUrl} 
              target="_blank" 
              rel="noopener noreferrer"
              className="issue-title-link"
              title="View on ComicVine"
            >
              {issue.title || <span className="no-title">Untitled</span>}
              <ExternalLink size={12} className="external-link-icon" />
            </a>
          ) : (
            issue.title || <span className="no-title">Untitled</span>
          )}
        </div>
      </td>
      <td className="col-date">
        {formatDate(issue.storeDate || issue.releaseDate) || '-'}
      </td>
      <td className="col-status">
        <span className={`status-badge status-${status}`}>
          {status === 'owned' && <Check size={12} />}
          {status === 'wanted' && <Clock size={12} />}
          {status === 'edition' && <BookOpen size={12} />}
          {status === 'skipped' && <X size={12} />}
          {statusLabels[status]}
        </span>
      </td>
      <td className="col-tags">
        <div className="issue-tags">
          {issue.isAnnual && <span className="tag tag-annual">Annual</span>}
          {issue.isSpecial && <span className="tag tag-special">{issue.specialType || 'Special'}</span>}
          {issue.storyArcs?.slice(0, 1).map((arc, idx) => (
            <span key={idx} className="tag tag-arc" title={arc}>{arc}</span>
          ))}
          {issue.storyArcs && issue.storyArcs.length > 1 && (
            <span className="tag tag-more">+{issue.storyArcs.length - 1}</span>
          )}
        </div>
      </td>
      <td className="col-actions">
        <div className="action-buttons">
          {issue.comicVineUrl && (
            <a 
              href={issue.comicVineUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="btn btn-icon btn-sm"
              title="View on ComicVine"
            >
              <ExternalLink size={14} />
            </a>
          )}
          {/* Status toggle buttons - Mylar3 parity: can mark ANY issue as Wanted/Skipped */}
          {/* Marking an owned issue as Wanted triggers a re-search (replace/upgrade) */}
          {status !== 'wanted' && (
            <button 
              className="btn btn-icon btn-sm" 
              onClick={onMarkWanted}
              disabled={isUpdating}
              title={status === 'owned' ? "Re-search" : "Mark as Wanted"}
            >
              <Clock size={14} />
            </button>
          )}
          {status !== 'skipped' && (
            <button 
              className="btn btn-icon btn-sm" 
              onClick={onMarkSkipped}
              disabled={isUpdating}
              title="Skip"
            >
              <X size={14} />
            </button>
          )}
        </div>
      </td>
    </tr>
  );
}

// === Helper Functions ===
function getStatusBadge(status: string): string {
  switch (status?.toLowerCase()) {
    case 'continuing':
      return 'success';
    case 'ended':
      return 'muted';
    case 'hiatus':
      return 'warning';
    default:
      return 'info';
  }
}

function stripHtml(html: string): string {
  const doc = new DOMParser().parseFromString(html, 'text/html');
  return doc.body.textContent || '';
}

function formatDate(dateStr: string | null): string {
  if (!dateStr) return '';
  const date = new Date(dateStr);
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}
