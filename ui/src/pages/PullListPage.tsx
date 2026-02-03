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
  Filter
} from 'lucide-react';
import { api } from '../api/client';
import type { WeeklyPullList, PullListIssue, IssueStatus } from '../api/client';
import { Link } from 'react-router-dom';

type ViewMode = 'week' | 'upcoming' | 'past' | 'calendar';
type DisplayMode = 'list' | 'grid';

export function PullListPage() {
  const queryClient = useQueryClient();
  const [viewMode, setViewMode] = useState<ViewMode>('week');
  const [displayMode, setDisplayMode] = useState<DisplayMode>('list');
  const [weekOffset, setWeekOffset] = useState(0);
  const [selectedIssues, setSelectedIssues] = useState<Set<number>>(new Set());
  const [statusFilter, setStatusFilter] = useState<IssueStatus | 'all'>('all');

  // Calculate week date based on offset
  const getWeekDate = () => {
    const date = new Date();
    date.setDate(date.getDate() + (weekOffset * 7));
    return date.toISOString().split('T')[0];
  };

  // Queries
  const { data: thisWeek, isLoading: thisWeekLoading, refetch: refetchThisWeek } = useQuery({
    queryKey: ['pulllist', 'week', weekOffset],
    queryFn: () => weekOffset === 0 
      ? api.getPullListThisWeek() 
      : api.getPullListWeek(getWeekDate()),
    enabled: viewMode === 'week',
  });

  const { data: upcoming, isLoading: upcomingLoading } = useQuery({
    queryKey: ['pulllist', 'upcoming'],
    queryFn: () => api.getPullListUpcoming(4),
    enabled: viewMode === 'upcoming',
  });

  const { data: past, isLoading: pastLoading } = useQuery({
    queryKey: ['pulllist', 'past'],
    queryFn: () => api.getPullListPast(4),
    enabled: viewMode === 'past',
  });

  const { data: stats } = useQuery({
    queryKey: ['pulllist', 'stats'],
    queryFn: () => api.getPullListStats(),
  });

  // Mutations
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

  // Filter issues by status
  const filterIssues = (issues: PullListIssue[]) => {
    if (statusFilter === 'all') return issues;
    return issues.filter(i => i.status === statusFilter);
  };

  // Get current data based on view mode
  const getCurrentData = (): WeeklyPullList | null => {
    if (viewMode === 'week') return thisWeek ?? null;
    return null;
  };

  const isLoading = viewMode === 'week' ? thisWeekLoading : 
                    viewMode === 'upcoming' ? upcomingLoading : 
                    viewMode === 'past' ? pastLoading : false;

  const currentData = getCurrentData();

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

  // Toggle issue selection
  const toggleIssueSelection = (issueId: number) => {
    const newSelected = new Set(selectedIssues);
    if (newSelected.has(issueId)) {
      newSelected.delete(issueId);
    } else {
      newSelected.add(issueId);
    }
    setSelectedIssues(newSelected);
  };

  // Select all visible issues
  const selectAllVisible = () => {
    if (!currentData) return;
    const filtered = filterIssues(currentData.issues);
    setSelectedIssues(new Set(filtered.map(i => i.issueId)));
  };

  // Get status badge
  const getStatusBadge = (status: IssueStatus) => {
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

  // Render issue card for grid view
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

  // Render issue row for list view
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

  // Render week section
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
                      onChange={selectAllVisible}
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
          {/* View mode tabs */}
          <div className="toolbar-group btn-group">
            <button 
              className={`btn ${viewMode === 'week' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setViewMode('week')}
            >
              This Week
            </button>
            <button 
              className={`btn ${viewMode === 'upcoming' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setViewMode('upcoming')}
            >
              Upcoming
            </button>
            <button 
              className={`btn ${viewMode === 'past' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setViewMode('past')}
            >
              Past
            </button>
          </div>

          {/* Week navigation (only for week view) */}
          {viewMode === 'week' && (
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

          {/* Status filter */}
          <div className="toolbar-group">
            <Filter size={16} className="text-muted" />
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
          </div>

          <div className="toolbar-spacer" />

          {/* Bulk actions */}
          {selectedIssues.size > 0 && (
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
              title="Grid View"
            >
              <Grid size={18} />
            </button>
          </div>

          <button 
            className="btn btn-icon" 
            onClick={() => refetchThisWeek()} 
            title="Refresh"
          >
            <RefreshCw size={18} />
          </button>
        </div>
        
        {/* Content */}
        {isLoading ? (
          <div className="loading"><div className="spinner" /></div>
        ) : viewMode === 'week' && currentData ? (
          renderWeekSection(currentData)
        ) : viewMode === 'upcoming' && upcoming ? (
          <div className="pull-list-weeks">
            {upcoming.map((week, i) => renderWeekSection(week, i))}
          </div>
        ) : viewMode === 'past' && past ? (
          <div className="pull-list-weeks">
            {past.map((week, i) => renderWeekSection(week, i))}
          </div>
        ) : (
          <div className="empty-state">
            <Calendar size={48} />
            <div className="empty-state-title">No releases found</div>
            <div className="empty-state-text">
              Add some series and match them to ComicVine to see upcoming releases.
            </div>
          </div>
        )}
      </div>
    </>
  );
}
