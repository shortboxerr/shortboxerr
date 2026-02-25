import { useState, useMemo, useEffect, useRef, useCallback } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  ArrowLeft, ExternalLink, RefreshCw, Calendar, BookOpen, HardDrive, 
  Check, X, Clock, Grid, List, Filter, SortAsc, SortDesc, Star, Zap, Trash2, Settings,
  ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Search, Loader2, Link as LinkIcon
} from 'lucide-react';
import { api } from '../api/client';
import type { Issue, IssueStatus, SeriesPullListSettingsDto, SeriesMatchCandidate } from '../api/client';
import { useToast } from '../components/Toast';

type ViewMode = 'cover' | 'list';
type SortKey = 'issueNumber' | 'releaseDate' | 'status' | 'title';
type SortDir = 'asc' | 'desc';
type StatusFilter = 'all' | 'owned' | 'wanted' | 'missing' | 'skipped';
type PageSize = 9 | 12 | 24 | 48 | 96 | 192;

export function SeriesDetailPage() {
  const { id } = useParams<{ id: string }>();
  const seriesId = parseInt(id ?? '0', 10);
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const toast = useToast();

  // Load UI settings for view preference
  const { data: uiSettings } = useQuery({
    queryKey: ['settings', 'ui'],
    queryFn: () => api.getUiSettings(),
  });

  // View state - initialize from settings
  const [viewMode, setViewMode] = useState<ViewMode>('cover');
  const [sortKey, setSortKey] = useState<SortKey>('issueNumber');
  const [sortDir, setSortDir] = useState<SortDir>('desc');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [selectedIssues, setSelectedIssues] = useState<Set<number>>(new Set());
  const [showAnnuals, setShowAnnuals] = useState(true);
  const [showSettingsModal, setShowSettingsModal] = useState(false);
  const [showMatchModal, setShowMatchModal] = useState(false);
  
  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState<PageSize>(12);
  
  // Fetch global pull list settings (for upcoming releases display options)
  const { data: pullListSettings } = useQuery({
    queryKey: ['pulllist', 'settings'],
    queryFn: () => api.getPullListSettings(),
  });

  // Fetch series-specific pull list settings
  const { data: seriesSettings } = useQuery({
    queryKey: ['series', seriesId, 'pulllist-settings'],
    queryFn: () => api.getSeriesPullListSettings(seriesId),
    enabled: seriesId > 0,
  });

  // Update series settings mutation
  const updateSeriesSettings = useMutation({
    mutationFn: (settings: SeriesPullListSettingsDto) => 
      api.updateSeriesPullListSettings(seriesId, settings),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['series', seriesId, 'pulllist-settings'] });
    },
  });

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
    onSuccess: async (_, variables) => {
      // Use the refetch functions directly from the query hooks
      await Promise.all([
        refetchIssues(),
        refetchAnnuals(),
        refetchSeries(),
      ]);
      // Also invalidate dashboard/pulllist stats so counts update immediately
      queryClient.invalidateQueries({ queryKey: ['pulllist', 'stats'] });
      queryClient.invalidateQueries({ queryKey: ['pulllist', 'week'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard-stats'] });
      setSelectedIssues(new Set());
      
      // Show success toast
      const count = variables.issueIds.length;
      const statusLabel = variables.status === 'Wanted' ? 'wanted' : 'skipped';
      toast.success(count === 1 
        ? `Issue marked as ${statusLabel}` 
        : `${count} issues marked as ${statusLabel}`
      );
    },
    onError: (_, variables) => {
      const count = variables.issueIds.length;
      toast.error(count === 1 
        ? 'Failed to update issue status' 
        : `Failed to update ${count} issues`
      );
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

  // Search for a specific issue
  const searchIssue = useMutation({
    mutationFn: async (issueId: number) => {
      return api.searchIssue(issueId);
    },
    onSuccess: (result) => {
      if (result.success) {
        toast.success(`Found: ${result.selectedCandidateTitle || 'Download started'}`);
      } else if (result.candidatesFound === 0) {
        toast.info(`No results found for #${result.issueNumber}`);
      } else {
        toast.warning(result.error || 'Search completed but no download started');
      }
    },
    onError: () => {
      toast.error('Search failed');
    },
  });

  const handleSearchIssue = (issueId: number) => {
    searchIssue.mutate(issueId);
  };

  // Search all wanted issues in this series
  const searchAllWanted = useMutation({
    mutationFn: async () => {
      return api.searchSeriesWanted(seriesId);
    },
    onSuccess: (result) => {
      if (result.totalSearched === 0) {
        toast.info('No wanted issues to search');
      } else if (result.successCount > 0) {
        toast.success(`Found downloads for ${result.successCount} of ${result.totalSearched} issues`);
      } else {
        toast.warning(`Searched ${result.totalSearched} issues - no results found`);
      }
    },
    onError: () => {
      toast.error('Search failed');
    },
  });

  const handleSearchAllWanted = () => {
    searchAllWanted.mutate();
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
      toast.success('Metadata refreshed from ComicVine');
    },
    onError: () => {
      toast.error('Failed to refresh metadata');
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
      toast.success('Series deleted');
      navigate('/series');
    },
    onError: () => {
      toast.error('Failed to delete series');
    },
  });

  const handleDeleteSeries = () => {
    if (confirm(`Delete "${series?.title}"? This will remove the series and all its issues from your library. This cannot be undone.`)) {
      deleteSeries.mutate();
    }
  };

  const { data: series, isLoading: isLoadingSeries, refetch: refetchSeries } = useQuery({
    queryKey: ['series', seriesId],
    queryFn: () => api.getSeriesById(seriesId),
    enabled: seriesId > 0,
  });

  const { data: issuesData, isLoading: isLoadingIssues, refetch: refetchIssues } = useQuery({
    queryKey: ['series', seriesId, 'issues', sortKey, sortDir],
    queryFn: () => api.getSeriesIssues(seriesId, { pageSize: 500, sortKey, sortDir }),
    enabled: seriesId > 0,
  });

  // Fetch annuals from the dedicated endpoint (includes linked annual series - Mylar3 parity)
  const { data: annualsData, refetch: refetchAnnuals } = useQuery({
    queryKey: ['series', seriesId, 'annuals'],
    queryFn: () => api.getSeriesAnnuals(seriesId),
    enabled: seriesId > 0,
  });

  // Fetch upcoming releases from WalkSoftly (issues not yet in ComicVine)
  // Respects pull list settings for show/hide and weeks ahead
  const weeksAhead = pullListSettings?.upcomingReleasesWeeksAhead ?? 4;
  const showUpcoming = pullListSettings?.showUpcomingReleasesOnSeriesView ?? true;
  
  const { data: upcomingData } = useQuery({
    queryKey: ['series', seriesId, 'upcoming', weeksAhead],
    queryFn: () => api.getSeriesUpcomingReleases(seriesId, weeksAhead),
    enabled: seriesId > 0 && showUpcoming,
    staleTime: 5 * 60 * 1000, // Cache for 5 minutes
  });

  const upcomingReleases = showUpcoming ? (upcomingData?.releases ?? []) : [];

  const allIssues = issuesData?.items ?? [];
  const allAnnuals = annualsData?.annuals ?? [];
  const linkedAnnualSeriesCount = annualsData?.linkedAnnualSeriesCount ?? 0;

  // Type for unified display of both regular issues and upcoming releases
  type DisplayIssue = {
    issueNumber: number;
    issueNumberText?: string;
    title?: string | null;
    coverImageUrl?: string | null;
    storeDate?: string | null;
    releaseDate?: string;
    isUpcoming: boolean;
    // Regular issue fields
    issue?: typeof allIssues[0];
    // Upcoming release fields  
    upcoming?: typeof upcomingReleases[0];
  };

  // Separate regular issues from annuals, and merge with upcoming releases
  const { regularIssues, annualIssues } = useMemo(() => {
    // Filter regular issues (excluding annuals from the main issues list)
    const regularFromDb: DisplayIssue[] = allIssues
      .filter(issue => {
        // Exclude annuals from regular view (they're shown in the Annuals section)
        if (issue.isAnnual) return false;
        
        // Filter by status
        if (statusFilter !== 'all') {
          const status = getIssueStatus(issue);
          if (status !== statusFilter) return false;
        }
        
        return true;
      })
      .map(issue => ({
        issueNumber: issue.issueNumber,
        issueNumberText: issue.issueNumber?.toString(),
        title: issue.title,
        coverImageUrl: issue.coverImageUrl,
        storeDate: issue.storeDate,
        isUpcoming: false,
        issue,
      }));
    
    // Convert upcoming releases to DisplayIssue format
    const upcomingAsDisplay: DisplayIssue[] = upcomingReleases
      .filter(release => !release.isAnnual) // Exclude annual upcoming releases from main list
      .map(release => ({
        issueNumber: release.issueNumber,
        issueNumberText: release.issueNumberText || release.issueNumber?.toString(),
        title: release.title,
        coverImageUrl: release.coverImageUrl,
        releaseDate: release.releaseDate,
        isUpcoming: true,
        upcoming: release,
      }));
    
    // Merge and sort by issue number (respects current sort direction)
    const regular = [...regularFromDb, ...upcomingAsDisplay]
      .sort((a, b) => sortDir === 'desc' 
        ? b.issueNumber - a.issueNumber 
        : a.issueNumber - b.issueNumber);
    
    // Filter annuals from the annuals endpoint
    const annuals = allAnnuals.filter(issue => {
      // Filter by status
      if (statusFilter !== 'all') {
        const status = getIssueStatus(issue);
        if (status !== statusFilter) return false;
      }
      return true;
    });
    
    return { regularIssues: regular, annualIssues: annuals };
  }, [allIssues, allAnnuals, upcomingReleases, statusFilter, sortDir]);

  // Combined filtered issues (for selection purposes) - only includes actual issues, not upcoming
  const filteredIssues = useMemo(() => {
    const regularOnly = regularIssues.filter(d => !d.isUpcoming && d.issue).map(d => d.issue!);
    return showAnnuals ? [...regularOnly, ...annualIssues] : regularOnly;
  }, [regularIssues, annualIssues, showAnnuals]);

  // Count annuals for display
  const annualCount = annualIssues.length;

  // Pagination calculations for regular issues
  const totalRegularIssues = regularIssues.length;
  const totalPages = Math.ceil(totalRegularIssues / pageSize);
  const paginatedRegularIssues = useMemo(() => {
    const startIndex = (currentPage - 1) * pageSize;
    return regularIssues.slice(startIndex, startIndex + pageSize);
  }, [regularIssues, currentPage, pageSize]);

  // Reset to page 1 when filters change
  useEffect(() => {
    setCurrentPage(1);
  }, [statusFilter, sortKey, sortDir, pageSize]);

  // Page navigation handlers
  const goToPage = (page: number) => {
    setCurrentPage(Math.max(1, Math.min(page, totalPages)));
  };

  const goToFirstPage = () => goToPage(1);
  const goToPreviousPage = () => goToPage(currentPage - 1);
  const goToNextPage = () => goToPage(currentPage + 1);
  const goToLastPage = () => goToPage(totalPages);

  // Handle page size change
  const handlePageSizeChange = (newSize: PageSize) => {
    setPageSize(newSize);
    setCurrentPage(1); // Reset to first page
  };

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

  const allIssuesSelected = filteredIssues.length > 0 && selectedIssues.size === filteredIssues.length;
  const someIssuesSelected = selectedIssues.size > 0;

  const toggleSelectAllIssues = () => {
    // If any items are selected, clear selection; otherwise select all visible
    if (someIssuesSelected) {
      setSelectedIssues(new Set());
    } else {
      setSelectedIssues(new Set(filteredIssues.map(i => i.id)));
    }
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
            title="Search All Wanted Issues"
            onClick={handleSearchAllWanted}
            disabled={searchAllWanted.isPending}
          >
            {searchAllWanted.isPending ? <Loader2 size={18} className="spinning" /> : <Search size={18} />}
          </button>
          <button 
            className="btn btn-icon" 
            title="Series Settings (Annual/Special Handling)"
            onClick={() => setShowSettingsModal(true)}
          >
            <Settings size={18} />
          </button>
          {!series?.comicVineId ? (
            <button 
              className="btn btn-icon" 
              title="Match to ComicVine"
              onClick={() => setShowMatchModal(true)}
            >
              <LinkIcon size={18} />
            </button>
          ) : (
            <button 
              className="btn btn-icon" 
              title="Refresh Series Metadata from ComicVine"
              onClick={handleRefreshMetadata}
              disabled={refreshMetadata.isPending}
            >
              <RefreshCw size={18} className={refreshMetadata.isPending ? 'spinning' : ''} />
            </button>
          )}
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
                <span>{series.issueCount + upcomingReleases.length} issues{upcomingReleases.length > 0 && ` (${upcomingReleases.length} upcoming)`}</span>
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

            {series.comicVineUrl ? (
              <a
                href={series.comicVineUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="series-detail-link"
              >
                <ExternalLink size={14} />
                View on ComicVine
              </a>
            ) : (
              <button 
                className="btn btn-primary btn-sm"
                onClick={() => setShowMatchModal(true)}
                style={{ marginTop: '12px' }}
              >
                <LinkIcon size={14} />
                Match to ComicVine
              </button>
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

            {/* Annuals Toggle */}
            {annualCount > 0 && (
              <label className="toolbar-checkbox" title="Show/hide annual issues">
                <input 
                  type="checkbox" 
                  checked={showAnnuals} 
                  onChange={(e) => setShowAnnuals(e.target.checked)}
                />
                <Star size={14} />
                Annuals ({annualCount})
              </label>
            )}

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

            {/* Page Size Dropdown (Mylar3 parity) */}
            <div className="pagesize-dropdown">
              <select 
                value={pageSize} 
                onChange={(e) => handlePageSizeChange(Number(e.target.value) as PageSize)}
                className="pagesize-select"
                title="Issues per page"
              >
                <option value={9}>9 per page</option>
                <option value={12}>12 per page</option>
                <option value={24}>24 per page</option>
                <option value={48}>48 per page</option>
                <option value={96}>96 per page</option>
                <option value={192}>192 per page</option>
              </select>
            </div>

            {/* Select All Button (useful for cover view) */}
            {viewMode === 'cover' && filteredIssues.length > 0 && (
              <button 
                className="btn btn-sm"
                onClick={toggleSelectAllIssues}
                title={someIssuesSelected ? 'Deselect All' : 'Select All'}
              >
                {someIssuesSelected ? 'Deselect All' : 'Select All'}
              </button>
            )}

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
          ) : regularIssues.length === 0 && annualIssues.length === 0 ? (
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
          ) : (
            <>
              {/* Regular Issues Section (now includes merged upcoming releases) */}
              {regularIssues.length > 0 && (
                <>
                  {viewMode === 'cover' ? (
                    <div className="issues-grid">
                      {paginatedRegularIssues.map((displayItem) => (
                        displayItem.isUpcoming ? (
                          // Render upcoming release card
                          <div 
                            key={`upcoming-${displayItem.issueNumber}`}
                            className="issue-card issue-card-wanted upcoming"
                            style={{
                              border: '1px dashed var(--accent-info)',
                            }}
                          >
                            <div className="issue-card-cover-wrapper">
                              <img
                                src={displayItem.coverImageUrl || 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="100" height="150" viewBox="0 0 100 150"%3E%3Crect fill="%232a2d35" width="100" height="150"/%3E%3Ctext fill="%236b7280" font-family="sans-serif" font-size="10" x="50" y="75" text-anchor="middle"%3ENo Cover%3C/text%3E%3C/svg%3E'}
                                alt={`Issue ${displayItem.issueNumberText || displayItem.issueNumber}`}
                                className="issue-card-cover"
                                onError={(e) => {
                                  (e.target as HTMLImageElement).src = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="100" height="150" viewBox="0 0 100 150"%3E%3Crect fill="%232a2d35" width="100" height="150"/%3E%3Ctext fill="%236b7280" font-family="sans-serif" font-size="10" x="50" y="75" text-anchor="middle"%3ENo Cover%3C/text%3E%3C/svg%3E';
                                }}
                              />
                              <div className="issue-card-status" style={{ background: 'var(--accent-info)' }}>
                                <Clock size={14} />
                              </div>
                              <span 
                                className="issue-card-badge"
                                style={{
                                  position: 'absolute',
                                  top: '6px',
                                  left: '6px',
                                  background: 'var(--accent-info)',
                                  color: 'white',
                                  padding: '2px 6px',
                                  borderRadius: '4px',
                                  fontSize: '10px',
                                  fontWeight: 600,
                                }}
                              >
                                UPCOMING
                              </span>
                            </div>
                            <div className="issue-card-info">
                              <div className="issue-card-number">{displayItem.issueNumberText || displayItem.issueNumber}</div>
                              <div className="issue-card-title">{displayItem.title || 'TBA'}</div>
                              {displayItem.upcoming && (
                                <div className="issue-card-date" style={{ fontSize: '11px', color: 'var(--accent-info)', fontWeight: 500 }}>
                                  {displayItem.upcoming.releaseTiming || formatDaysUntilRelease(displayItem.releaseDate!)}
                                </div>
                              )}
                            </div>
                          </div>
                        ) : displayItem.issue ? (
                          // Render regular issue card
                          <IssueCoverCard 
                            key={displayItem.issue.id} 
                            issue={displayItem.issue} 
                            selected={selectedIssues.has(displayItem.issue.id)}
                            onSelect={() => toggleIssueSelection(displayItem.issue!.id)}
                            onMarkWanted={() => handleMarkAsWanted([displayItem.issue!.id])}
                            onMarkSkipped={() => handleMarkAsSkipped([displayItem.issue!.id])}
                            onSearch={() => handleSearchIssue(displayItem.issue!.id)}
                            isUpdating={updateIssueStatus.isPending}
                            isSearching={searchIssue.isPending && searchIssue.variables === displayItem.issue.id}
                          />
                        ) : null
                      ))}
                    </div>
                  ) : (
                    // List view with both regular and upcoming issues
                    <div className="issues-table-wrapper">
                      <table className="issues-table">
                        <thead>
                          <tr>
                            <th className="col-checkbox">
                              <input 
                                type="checkbox" 
                                checked={allIssuesSelected}
                                onChange={toggleSelectAllIssues}
                              />
                            </th>
                            <th className="col-number sortable" onClick={() => toggleSort('issueNumber')}>
                              # {sortKey === 'issueNumber' && (sortDir === 'asc' ? <SortAsc size={12} /> : <SortDesc size={12} />)}
                            </th>
                            <th className="col-title sortable" onClick={() => toggleSort('title')}>
                              Title {sortKey === 'title' && (sortDir === 'asc' ? <SortAsc size={12} /> : <SortDesc size={12} />)}
                            </th>
                            <th className="col-date sortable" onClick={() => toggleSort('releaseDate')}>
                              Release Date {sortKey === 'releaseDate' && (sortDir === 'asc' ? <SortAsc size={12} /> : <SortDesc size={12} />)}
                            </th>
                            <th className="col-status sortable" onClick={() => toggleSort('status')}>
                              Status {sortKey === 'status' && (sortDir === 'asc' ? <SortAsc size={12} /> : <SortDesc size={12} />)}
                            </th>
                            <th className="col-tags">Tags</th>
                            <th className="col-actions">Actions</th>
                          </tr>
                        </thead>
                        <tbody>
                          {paginatedRegularIssues.map((displayItem) => (
                            displayItem.isUpcoming && displayItem.upcoming ? (
                              // Upcoming issue row
                              <tr key={`upcoming-${displayItem.issueNumber}`} className="issue-row upcoming" style={{ background: 'var(--bg-secondary)' }}>
                                <td className="col-checkbox">
                                  {/* No selection for upcoming issues */}
                                </td>
                                <td className="col-number" style={{ fontWeight: 600 }}>
                                  {displayItem.issueNumberText || displayItem.issueNumber}
                                </td>
                                <td className="col-title">
                                  <span style={{ color: displayItem.title ? 'var(--text-primary)' : 'var(--text-muted)' }}>
                                    {displayItem.title || 'TBA'}
                                  </span>
                                </td>
                                <td className="col-date">
                                  <span style={{ color: 'var(--accent-info)', fontWeight: 500 }}>
                                    {displayItem.upcoming.releaseTiming || formatDate(displayItem.releaseDate || null)}
                                  </span>
                                </td>
                                <td className="col-status">
                                  <span className="badge badge-info" style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                                    <Clock size={12} /> Upcoming
                                  </span>
                                </td>
                                <td className="col-tags">
                                  {displayItem.upcoming.isAnnual && <span className="tag tag-annual">Annual</span>}
                                  {displayItem.upcoming.isSpecial && <span className="tag tag-special">Special</span>}
                                </td>
                                <td className="col-actions">
                                  {/* No actions for upcoming issues */}
                                </td>
                              </tr>
                            ) : displayItem.issue ? (
                              // Regular issue row
                              <IssueListRow
                                key={displayItem.issue.id}
                                issue={displayItem.issue}
                                selected={selectedIssues.has(displayItem.issue.id)}
                                onSelect={() => toggleIssueSelection(displayItem.issue!.id)}
                                onMarkWanted={() => handleMarkAsWanted([displayItem.issue!.id])}
                                onMarkSkipped={() => handleMarkAsSkipped([displayItem.issue!.id])}
                                onSearch={() => handleSearchIssue(displayItem.issue!.id)}
                                isUpdating={updateIssueStatus.isPending}
                                isSearching={searchIssue.isPending && searchIssue.variables === displayItem.issue.id}
                              />
                            ) : null
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}

                  {/* Pagination Controls */}
                  {totalPages > 1 && (
                    <div className="pagination-controls">
                      <div className="pagination-info">
                        Showing {((currentPage - 1) * pageSize) + 1}-{Math.min(currentPage * pageSize, totalRegularIssues)} of {totalRegularIssues} issues
                      </div>
                      <div className="pagination-buttons">
                        <button 
                          className="btn btn-icon btn-sm" 
                          onClick={goToFirstPage}
                          disabled={currentPage === 1}
                          title="First page"
                        >
                          <ChevronsLeft size={16} />
                        </button>
                        <button 
                          className="btn btn-icon btn-sm" 
                          onClick={goToPreviousPage}
                          disabled={currentPage === 1}
                          title="Previous page"
                        >
                          <ChevronLeft size={16} />
                        </button>
                        
                        {/* Page number buttons */}
                        <div className="pagination-pages">
                          {getPageNumbers(currentPage, totalPages).map((page, idx) => (
                            page === '...' ? (
                              <span key={`ellipsis-${idx}`} className="pagination-ellipsis">...</span>
                            ) : (
                              <button
                                key={page}
                                className={`btn btn-sm ${currentPage === page ? 'btn-primary' : ''}`}
                                onClick={() => goToPage(page as number)}
                              >
                                {page}
                              </button>
                            )
                          ))}
                        </div>
                        
                        <button 
                          className="btn btn-icon btn-sm" 
                          onClick={goToNextPage}
                          disabled={currentPage === totalPages}
                          title="Next page"
                        >
                          <ChevronRight size={16} />
                        </button>
                        <button 
                          className="btn btn-icon btn-sm" 
                          onClick={goToLastPage}
                          disabled={currentPage === totalPages}
                          title="Last page"
                        >
                          <ChevronsRight size={16} />
                        </button>
                      </div>
                    </div>
                  )}
                </>
              )}

              {/* Upcoming releases are now integrated into the main issues grid above */}

              {/* Annuals Section */}
              {showAnnuals && annualIssues.length > 0 && (
                <div className="annuals-section" style={{ marginTop: regularIssues.length > 0 ? '32px' : '0' }}>
                  <div className="section-header" style={{ 
                    display: 'flex', 
                    alignItems: 'center', 
                    gap: '8px',
                    marginBottom: '16px',
                    paddingBottom: '12px',
                    borderBottom: '1px solid var(--border-color)'
                  }}>
                    <Star size={18} style={{ color: 'var(--accent-primary)' }} />
                    <h3 style={{ 
                      margin: 0, 
                      fontSize: '16px', 
                      fontWeight: 600,
                      color: 'var(--text-primary)'
                    }}>
                      Annuals
                    </h3>
                    <span style={{ 
                      fontSize: '13px', 
                      color: 'var(--text-muted)',
                      marginLeft: '4px'
                    }}>
                      ({annualIssues.length})
                    </span>
                    {linkedAnnualSeriesCount > 0 && (
                      <span style={{ 
                        fontSize: '11px', 
                        color: 'var(--text-muted)',
                        background: 'var(--bg-secondary)',
                        padding: '2px 8px',
                        borderRadius: '10px',
                        marginLeft: '8px'
                      }}>
                        from {linkedAnnualSeriesCount} linked series
                      </span>
                    )}
                  </div>
                  
                  {viewMode === 'cover' ? (
                    <div className="issues-grid">
                      {annualIssues.map((issue) => (
                        <IssueCoverCard 
                          key={issue.id} 
                          issue={issue} 
                          selected={selectedIssues.has(issue.id)}
                          onSelect={() => toggleIssueSelection(issue.id)}
                          onMarkWanted={() => handleMarkAsWanted([issue.id])}
                          onMarkSkipped={() => handleMarkAsSkipped([issue.id])}
                          onSearch={() => handleSearchIssue(issue.id)}
                          isUpdating={updateIssueStatus.isPending}
                          isSearching={searchIssue.isPending && searchIssue.variables === issue.id}
                        />
                      ))}
                    </div>
                  ) : (
                    <IssueListView 
                      issues={annualIssues}
                      selectedIds={selectedIssues}
                      onSelect={toggleIssueSelection}
                      onToggleSelectAll={() => {
                        // Toggle all annuals
                        const allAnnualIds = new Set(annualIssues.map(i => i.id));
                        const allAnnualsSelected = annualIssues.every(i => selectedIssues.has(i.id));
                        if (allAnnualsSelected) {
                          setSelectedIssues(prev => {
                            const next = new Set(prev);
                            annualIssues.forEach(i => next.delete(i.id));
                            return next;
                          });
                        } else {
                          setSelectedIssues(prev => new Set([...prev, ...allAnnualIds]));
                        }
                      }}
                      allSelected={annualIssues.every(i => selectedIssues.has(i.id))}
                      someSelected={annualIssues.some(i => selectedIssues.has(i.id))}
                      sortKey={sortKey}
                      sortDir={sortDir}
                      onSort={toggleSort}
                      onMarkWanted={handleMarkAsWanted}
                      onMarkSkipped={handleMarkAsSkipped}
                      onSearch={handleSearchIssue}
                      isUpdating={updateIssueStatus.isPending}
                      searchingIssueId={searchIssue.isPending ? searchIssue.variables : undefined}
                      showHeader={false}
                    />
                  )}
                </div>
              )}

              {/* Empty state when filter hides all issues */}
              {regularIssues.length === 0 && (!showAnnuals || annualIssues.length === 0) && (
                <div className="empty-state" style={{ padding: '40px 20px' }}>
                  <BookOpen size={48} style={{ opacity: 0.3 }} />
                  <div className="empty-state-title">
                    No {statusFilter} issues
                  </div>
                  <div className="empty-state-text">
                    There are no issues with status "{statusFilter}".
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      </div>

      {/* Series Settings Modal */}
      {showSettingsModal && (
        <SeriesSettingsModal
          seriesTitle={series.title}
          settings={seriesSettings}
          onClose={() => setShowSettingsModal(false)}
          onSave={(newSettings) => {
            updateSeriesSettings.mutate({ ...newSettings, seriesId });
            setShowSettingsModal(false);
          }}
          isSaving={updateSeriesSettings.isPending}
        />
      )}

      {/* Match to ComicVine Modal */}
      {showMatchModal && (
        <MatchToComicVineModal
          seriesId={seriesId}
          seriesTitle={series.title}
          onClose={() => setShowMatchModal(false)}
          onMatched={async () => {
            await refetchSeries();
            await refetchIssues();
            queryClient.invalidateQueries({ queryKey: ['series', seriesId] });
            setShowMatchModal(false);
          }}
        />
      )}
    </>
  );
}

// === Series Settings Modal ===
interface SeriesSettingsModalProps {
  seriesTitle: string;
  settings?: SeriesPullListSettingsDto;
  onClose: () => void;
  onSave: (settings: SeriesPullListSettingsDto) => void;
  isSaving: boolean;
}

function SeriesSettingsModal({ seriesTitle, settings, onClose, onSave, isSaving }: SeriesSettingsModalProps) {
  const [includeAnnuals, setIncludeAnnuals] = useState(settings?.includeAnnuals ?? null);
  const [includeSpecials, setIncludeSpecials] = useState(settings?.includeSpecials ?? null);
  const [skipVariants, setSkipVariants] = useState(settings?.skipVariants ?? null);

  const handleSave = () => {
    onSave({
      seriesId: settings?.seriesId ?? 0,
      includeAnnuals,
      includeSpecials,
      skipVariants,
      searchPriority: settings?.searchPriority ?? 0,
    });
  };

  // Tri-state checkbox helper: null = use global default, true/false = override
  const TriStateCheckbox = ({ 
    value, 
    onChange, 
    label, 
    globalDefault 
  }: { 
    value: boolean | null; 
    onChange: (v: boolean | null) => void; 
    label: string;
    globalDefault: boolean;
  }) => {
    const cycle = () => {
      if (value === null) onChange(true);
      else if (value === true) onChange(false);
      else onChange(null);
    };

    return (
      <div 
        className="tristate-checkbox" 
        onClick={cycle}
        style={{ 
          display: 'flex', 
          alignItems: 'center', 
          gap: '10px', 
          cursor: 'pointer',
          padding: '8px',
          borderRadius: 'var(--radius-sm)',
          background: 'var(--bg-tertiary)',
          marginBottom: '8px'
        }}
      >
        <div style={{
          width: '20px',
          height: '20px',
          borderRadius: '4px',
          border: '2px solid var(--border-color)',
          background: value === null ? 'transparent' : value ? 'var(--accent-primary)' : 'transparent',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: '12px',
          color: 'white'
        }}>
          {value === null && <span style={{ color: 'var(--text-muted)', fontSize: '10px' }}>—</span>}
          {value === true && <Check size={14} />}
          {value === false && <X size={14} />}
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: '13px', color: 'var(--text-primary)' }}>{label}</div>
          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
            {value === null 
              ? `Using global default (${globalDefault ? 'enabled' : 'disabled'})`
              : value ? 'Enabled for this series' : 'Disabled for this series'
            }
          </div>
        </div>
      </div>
    );
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: '480px' }}>
        <div className="modal-header">
          <h2 className="modal-title">Series Settings</h2>
          <button className="btn btn-icon" onClick={onClose}>
            <X size={20} />
          </button>
        </div>
        <div className="modal-body">
          <div style={{ 
            background: 'var(--bg-secondary)', 
            padding: '12px 16px', 
            borderRadius: 'var(--radius-md)', 
            marginBottom: '16px',
            border: '1px solid var(--border-color)'
          }}>
            <div style={{ fontSize: '14px', fontWeight: 500, color: 'var(--text-primary)' }}>
              {seriesTitle}
            </div>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
              Override global annual/special issue handling for this series
            </div>
          </div>

          <div style={{ marginBottom: '16px' }}>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'uppercase', letterSpacing: '0.5px' }}>
              Issue Types
            </div>
            
            <TriStateCheckbox 
              value={includeAnnuals} 
              onChange={setIncludeAnnuals}
              label="Include Annuals"
              globalDefault={true}
            />
            
            <TriStateCheckbox 
              value={includeSpecials} 
              onChange={setIncludeSpecials}
              label="Include Specials"
              globalDefault={false}
            />
            
            <TriStateCheckbox 
              value={skipVariants} 
              onChange={setSkipVariants}
              label="Skip Variant Covers"
              globalDefault={true}
            />
          </div>

          <div style={{ 
            fontSize: '11px', 
            color: 'var(--text-muted)', 
            background: 'var(--bg-tertiary)',
            padding: '10px 12px',
            borderRadius: 'var(--radius-sm)',
            lineHeight: '1.5'
          }}>
            <strong>Click to cycle:</strong> Use global → Enable → Disable → Use global
            <br />
            Changes only affect auto-add behavior for new issues. Existing wanted issues are not affected.
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary" onClick={handleSave} disabled={isSaving}>
            {isSaving ? 'Saving...' : 'Save Settings'}
          </button>
        </div>
      </div>
    </div>
  );
}

// === Match to ComicVine Modal ===
interface MatchToComicVineModalProps {
  seriesId: number;
  seriesTitle: string;
  onClose: () => void;
  onMatched: () => Promise<void>;
}

function MatchToComicVineModal({ seriesId, seriesTitle, onClose, onMatched }: MatchToComicVineModalProps) {
  const [searchQuery, setSearchQuery] = useState(seriesTitle);
  const [debouncedQuery, setDebouncedQuery] = useState(seriesTitle);
  const [selectedVolume, setSelectedVolume] = useState<SeriesMatchCandidate | null>(null);
  const [matchError, setMatchError] = useState<string | null>(null);
  const [isMatching, setIsMatching] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedQuery(searchQuery);
    }, 400);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  const { data: searchResults, isLoading: isSearching } = useQuery({
    queryKey: ['comicvine-search', debouncedQuery],
    queryFn: () => api.searchSeriesFromComicVine(debouncedQuery, { limit: 25 }),
    enabled: debouncedQuery.length >= 2,
    staleTime: 60000,
  });

  const sortedResults = useMemo(() => {
    const results = searchResults?.results ?? [];
    return [...results].sort((a, b) => (b.issueCount || 0) - (a.issueCount || 0));
  }, [searchResults?.results]);

  const matchMutation = useMutation({
    mutationFn: (volumeId: number) => api.matchSeriesToComicVine(seriesId, volumeId),
    onSuccess: async (result) => {
      if (result.success) {
        setIsMatching(true);
        try {
          await onMatched();
        } finally {
          setIsMatching(false);
        }
      } else {
        setMatchError(result.error || 'Failed to match series');
      }
    },
    onError: (e) => {
      setMatchError(e instanceof Error ? e.message : 'Failed to match series');
    },
  });

  const handleMatch = useCallback(() => {
    if (!selectedVolume) return;
    setMatchError(null);
    matchMutation.mutate(selectedVolume.comicVineId);
  }, [selectedVolume, matchMutation]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  const isPending = matchMutation.isPending || isMatching;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal modal-large" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2 className="modal-title">Match to ComicVine</h2>
          <button className="btn btn-icon" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        <div className="modal-body" style={{ display: 'flex', flexDirection: 'column', gap: '16px', minHeight: '400px' }}>
          <div style={{ 
            background: 'var(--bg-secondary)', 
            padding: '12px 16px', 
            borderRadius: 'var(--radius-md)', 
            border: '1px solid var(--border-color)'
          }}>
            <div style={{ fontSize: '14px', fontWeight: 500, color: 'var(--text-primary)' }}>
              {seriesTitle}
            </div>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
              Search ComicVine to find the matching volume
            </div>
          </div>

          <div className="form-group" style={{ marginBottom: 0 }}>
            <div style={{ position: 'relative' }}>
              <Search size={18} style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }} />
              <input
                type="text"
                className="input"
                placeholder="Search ComicVine..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                autoFocus
                style={{ paddingLeft: '40px' }}
              />
              {isSearching && (
                <Loader2 size={18} style={{ position: 'absolute', right: '12px', top: '50%', transform: 'translateY(-50%)', color: 'var(--accent-primary)' }} className="spinning" />
              )}
            </div>
          </div>

          {matchError && (
            <div className="alert alert-danger" style={{ padding: '10px 14px' }}>
              {matchError}
            </div>
          )}

          <div style={{ flex: 1, overflow: 'auto', minHeight: 0 }}>
            {sortedResults.length > 0 ? (
              <div className="match-results-list">
                {sortedResults.map((candidate) => (
                  <div
                    key={candidate.comicVineId}
                    className={`match-result-item ${selectedVolume?.comicVineId === candidate.comicVineId ? 'selected' : ''}`}
                    onClick={() => setSelectedVolume(candidate)}
                    style={{
                      display: 'flex',
                      gap: '12px',
                      padding: '12px',
                      borderRadius: 'var(--radius-sm)',
                      cursor: 'pointer',
                      border: selectedVolume?.comicVineId === candidate.comicVineId 
                        ? '2px solid var(--accent-primary)' 
                        : '1px solid var(--border-color)',
                      background: selectedVolume?.comicVineId === candidate.comicVineId 
                        ? 'var(--bg-tertiary)' 
                        : 'transparent',
                      marginBottom: '8px',
                    }}
                  >
                    <img
                      src={candidate.coverImageUrl || 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="60" height="90" viewBox="0 0 60 90"%3E%3Crect fill="%232a2d35" width="60" height="90"/%3E%3C/svg%3E'}
                      alt={candidate.title}
                      style={{ width: '60px', height: '90px', objectFit: 'cover', borderRadius: '4px' }}
                      onError={(e) => {
                        (e.target as HTMLImageElement).src = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="60" height="90" viewBox="0 0 60 90"%3E%3Crect fill="%232a2d35" width="60" height="90"/%3E%3C/svg%3E';
                      }}
                    />
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontWeight: 500, color: 'var(--text-primary)' }}>{candidate.title}</div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
                        {candidate.publisher && <span>{candidate.publisher} • </span>}
                        {candidate.startYear && <span>{candidate.startYear}</span>}
                        {candidate.issueCount && <span> • {candidate.issueCount} issues</span>}
                      </div>
                      {candidate.aliases && candidate.aliases.length > 0 && (
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '4px', opacity: 0.7 }}>
                          Also known as: {candidate.aliases.slice(0, 2).join(', ')}
                        </div>
                      )}
                    </div>
                    {selectedVolume?.comicVineId === candidate.comicVineId && (
                      <Check size={20} style={{ color: 'var(--accent-primary)', flexShrink: 0 }} />
                    )}
                  </div>
                ))}
              </div>
            ) : debouncedQuery.length >= 2 && !isSearching ? (
              <div className="empty-state" style={{ padding: '40px 20px' }}>
                <Search size={48} style={{ opacity: 0.3 }} />
                <div className="empty-state-title">No results found</div>
                <div className="empty-state-text">Try a different search term.</div>
              </div>
            ) : (
              <div className="empty-state" style={{ padding: '40px 20px' }}>
                <Search size={48} style={{ opacity: 0.3 }} />
                <div className="empty-state-title">Search ComicVine</div>
                <div className="empty-state-text">Enter at least 2 characters to search.</div>
              </div>
            )}
          </div>
        </div>

        <div className="modal-footer">
          <button className="btn" onClick={onClose} disabled={isPending}>
            Cancel
          </button>
          <button 
            className="btn btn-primary" 
            onClick={handleMatch} 
            disabled={!selectedVolume || isPending}
          >
            {isPending ? (
              <>
                <Loader2 size={16} className="spinning" />
                Matching...
              </>
            ) : (
              <>
                <LinkIcon size={16} />
                Match Series
              </>
            )}
          </button>
        </div>
      </div>
    </div>
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
  onSearch: () => void;
  isUpdating: boolean;
  isSearching: boolean;
}

function IssueCoverCard({ issue, selected, onSelect, onMarkWanted, onMarkSkipped, onSearch, isUpdating, isSearching }: IssueCoverCardProps) {
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
        {/* Selection checkbox - always visible for easy toggle */}
        <div className="issue-card-checkbox" onClick={(e) => { e.stopPropagation(); onSelect(); }}>
          <input 
            type="checkbox" 
            checked={selected} 
            onChange={onSelect}
            onClick={(e) => e.stopPropagation()}
          />
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

        {/* Actions Overlay - shown on hover (desktop) or always (mobile via CSS) */}
        <div className={`issue-card-actions ${showActions ? 'show' : ''}`} onClick={(e) => e.stopPropagation()}>
            {issue.comicVineUrl && (
              <button 
                className="btn btn-icon btn-sm btn-action btn-action-link" 
                onClick={handleOpenComicVine}
                title="View on ComicVine"
              >
                <ExternalLink size={14} />
              </button>
            )}
            {/* Search button - available for wanted/missing issues */}
            {(status === 'wanted' || status === 'missing') && (
              <button 
                className="btn btn-icon btn-sm btn-action" 
                onClick={(e) => { e.stopPropagation(); onSearch(); }}
                disabled={isSearching}
                title="Search for this issue"
              >
                {isSearching ? <Loader2 size={14} className="spinning" /> : <Search size={14} />}
              </button>
            )}
            {/* Status toggle buttons - Mylar3 parity: can mark ANY issue as Wanted/Skipped */}
            {/* Marking an owned issue as Wanted triggers a re-search (replace/upgrade) */}
            {status !== 'wanted' && (
              <button 
                className="btn btn-icon btn-sm btn-action" 
                onClick={(e) => { e.stopPropagation(); onMarkWanted(); }}
                disabled={isUpdating}
                title={status === 'owned' ? "Re-search for this issue" : "Mark as Wanted"}
              >
                <Clock size={14} />
              </button>
            )}
            {status !== 'skipped' && (
              <button 
                className="btn btn-icon btn-sm btn-action" 
                onClick={(e) => { e.stopPropagation(); onMarkSkipped(); }}
                disabled={isUpdating}
                title="Skip this issue"
              >
                <X size={14} />
              </button>
            )}
        </div>
      </div>
      <div className="issue-card-info">
        <div className="issue-card-number">{issue.displayNumber}</div>
        {issue.title && <div className="issue-card-title" title={issue.title}>{issue.title}</div>}
        {/* Linked Annual Series indicator */}
        {issue.linkedAnnualSeriesTitle && (
          <div className="issue-card-linked-series" style={{ 
            fontSize: '10px', 
            color: 'var(--accent-primary)',
            opacity: 0.8,
            marginTop: '2px',
            whiteSpace: 'nowrap',
            overflow: 'hidden',
            textOverflow: 'ellipsis'
          }} title={`From: ${issue.linkedAnnualSeriesTitle}`}>
            {issue.linkedAnnualSeriesTitle}
          </div>
        )}
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
  onToggleSelectAll: () => void;
  allSelected: boolean;
  someSelected: boolean;
  sortKey: SortKey;
  sortDir: SortDir;
  onSort: (key: SortKey) => void;
  onMarkWanted: (ids: number[]) => void;
  onMarkSkipped: (ids: number[]) => void;
  onSearch: (issueId: number) => void;
  isUpdating: boolean;
  searchingIssueId?: number;
  showHeader?: boolean;
}

function IssueListView({ 
  issues, selectedIds, onSelect, onToggleSelectAll, allSelected, someSelected,
  sortKey, sortDir, onSort, onMarkWanted, onMarkSkipped, onSearch, isUpdating, searchingIssueId, showHeader = true 
}: IssueListViewProps) {
  const SortIcon = sortDir === 'asc' ? SortAsc : SortDesc;
  const selectAllRef = useRef<HTMLInputElement>(null);

  // Set indeterminate state on checkbox
  useEffect(() => {
    if (selectAllRef.current) {
      selectAllRef.current.indeterminate = someSelected && !allSelected;
    }
  }, [someSelected, allSelected]);
  
  return (
    <div className="issues-table-wrapper">
      <table className="issues-table">
        {showHeader && (
          <thead>
            <tr>
              <th className="col-checkbox">
                <input 
                  ref={selectAllRef}
                  type="checkbox" 
                  checked={allSelected}
                  onChange={onToggleSelectAll}
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
        )}
        <tbody>
          {issues.map((issue) => (
            <IssueListRow 
              key={issue.id} 
              issue={issue} 
              selected={selectedIds.has(issue.id)}
              onSelect={() => onSelect(issue.id)}
              onMarkWanted={() => onMarkWanted([issue.id])}
              onMarkSkipped={() => onMarkSkipped([issue.id])}
              onSearch={() => onSearch(issue.id)}
              isUpdating={isUpdating}
              isSearching={searchingIssueId === issue.id}
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
  onSearch: () => void;
  isUpdating: boolean;
  isSearching: boolean;
}

function IssueListRow({ issue, selected, onSelect, onMarkWanted, onMarkSkipped, onSearch, isUpdating, isSearching }: IssueListRowProps) {
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
          {/* Search button - available for wanted/missing issues */}
          {(status === 'wanted' || status === 'missing') && (
            <button 
              className="btn btn-icon btn-sm" 
              onClick={(e) => { e.stopPropagation(); onSearch(); }}
              disabled={isSearching}
              title="Search for this issue"
            >
              {isSearching ? <Loader2 size={14} className="spinning" /> : <Search size={14} />}
            </button>
          )}
          {/* Status toggle buttons - Mylar3 parity: can mark ANY issue as Wanted/Skipped */}
          {/* Marking an owned issue as Wanted triggers a re-search (replace/upgrade) */}
          {status !== 'wanted' && (
            <button 
              className="btn btn-icon btn-sm" 
              onClick={(e) => { e.stopPropagation(); onMarkWanted(); }}
              disabled={isUpdating}
              title={status === 'owned' ? "Re-search" : "Mark as Wanted"}
            >
              <Clock size={14} />
            </button>
          )}
          {status !== 'skipped' && (
            <button 
              className="btn btn-icon btn-sm" 
              onClick={(e) => { e.stopPropagation(); onMarkSkipped(); }}
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

function formatDaysUntilRelease(releaseDate: string): string {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const release = new Date(releaseDate);
  release.setHours(0, 0, 0, 0);
  
  const diffTime = release.getTime() - today.getTime();
  const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
  
  if (diffDays < 0) return 'Released';
  if (diffDays === 0) return 'Today';
  if (diffDays === 1) return 'Tomorrow';
  if (diffDays <= 7) return `In ${diffDays} days`;
  if (diffDays <= 14) return 'Next week';
  return formatDate(releaseDate);
}

// Generate page numbers with ellipsis for large page counts
function getPageNumbers(current: number, total: number): (number | '...')[] {
  const pages: (number | '...')[] = [];
  const delta = 2; // Number of pages to show on each side of current
  
  // Always show first page
  pages.push(1);
  
  // Calculate range around current page
  const rangeStart = Math.max(2, current - delta);
  const rangeEnd = Math.min(total - 1, current + delta);
  
  // Add ellipsis if there's a gap after first page
  if (rangeStart > 2) {
    pages.push('...');
  }
  
  // Add pages in range
  for (let i = rangeStart; i <= rangeEnd; i++) {
    pages.push(i);
  }
  
  // Add ellipsis if there's a gap before last page
  if (rangeEnd < total - 1) {
    pages.push('...');
  }
  
  // Always show last page (if more than 1 page)
  if (total > 1) {
    pages.push(total);
  }
  
  return pages;
}
