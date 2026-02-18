import { useState, useMemo, useEffect, useRef } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  ArrowLeft, ExternalLink, RefreshCw, Calendar, BookOpen, HardDrive, 
  Check, X, Clock, Grid, List, Filter, SortAsc, SortDesc, Star, Zap, Trash2, Settings,
  ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight
} from 'lucide-react';
import { api } from '../api/client';
import type { Issue, IssueStatus, SeriesPullListSettingsDto } from '../api/client';

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
  
  // Pagination state
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState<PageSize>(12);
  
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
    onSuccess: async () => {
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

  const allIssues = issuesData?.items ?? [];
  const allAnnuals = annualsData?.annuals ?? [];
  const linkedAnnualSeriesCount = annualsData?.linkedAnnualSeriesCount ?? 0;

  // Separate regular issues from annuals (regular issues exclude those marked as annual)
  const { regularIssues, annualIssues } = useMemo(() => {
    // Filter regular issues (excluding annuals from the main issues list)
    const regular = allIssues.filter(issue => {
      // Exclude annuals from regular view (they're shown in the Annuals section)
      if (issue.isAnnual) return false;
      
      // Filter by status
      if (statusFilter !== 'all') {
        const status = getIssueStatus(issue);
        if (status !== statusFilter) return false;
      }
      
      return true;
    });
    
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
  }, [allIssues, allAnnuals, statusFilter]);

  // Combined filtered issues (for selection purposes)
  const filteredIssues = useMemo(() => {
    return showAnnuals ? [...regularIssues, ...annualIssues] : regularIssues;
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
            title="Series Settings (Annual/Special Handling)"
            onClick={() => setShowSettingsModal(true)}
          >
            <Settings size={18} />
          </button>
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
              {/* Regular Issues Section */}
              {regularIssues.length > 0 && (
                <>
                  {viewMode === 'cover' ? (
                    <div className="issues-grid">
                      {paginatedRegularIssues.map((issue) => (
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
                      issues={paginatedRegularIssues}
                      selectedIds={selectedIssues}
                      onSelect={toggleIssueSelection}
                      onToggleSelectAll={toggleSelectAllIssues}
                      allSelected={allIssuesSelected}
                      someSelected={someIssuesSelected}
                      sortKey={sortKey}
                      sortDir={sortDir}
                      onSort={toggleSort}
                      onMarkWanted={handleMarkAsWanted}
                      onMarkSkipped={handleMarkAsSkipped}
                      isUpdating={updateIssueStatus.isPending}
                    />
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
                          isUpdating={updateIssueStatus.isPending}
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
                      isUpdating={updateIssueStatus.isPending}
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
  isUpdating: boolean;
  showHeader?: boolean;
}

function IssueListView({ 
  issues, selectedIds, onSelect, onToggleSelectAll, allSelected, someSelected,
  sortKey, sortDir, onSort, onMarkWanted, onMarkSkipped, isUpdating, showHeader = true 
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
