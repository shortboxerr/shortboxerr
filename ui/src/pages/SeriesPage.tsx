import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Plus, RefreshCw, Trash2, Edit, BookOpen, Search, X, Loader2, AlertCircle, ExternalLink, Filter, ArrowUpDown, ArrowUp, ArrowDown, Grid, List, FolderSync, Check, AlertTriangle, FolderX } from 'lucide-react';
import { api } from '../api/client';
import type { SeriesMatchCandidate } from '../api/client';

export function SeriesPage() {
  const [search, setSearch] = useState('');
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [showAddModal, setShowAddModal] = useState(false);
  const [showFilters, setShowFilters] = useState(false);
  const [showOrganizeModal, setShowOrganizeModal] = useState(false);
  
  // Filter and sort state
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [publisherFilter, setPublisherFilter] = useState<string>('all');
  const [sortKey, setSortKey] = useState<string>('title');
  const [sortDir, setSortDir] = useState<string>('asc');
  
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  // Fetch filter options
  const { data: filterOptions } = useQuery({
    queryKey: ['series-filter-options'],
    queryFn: () => api.getSeriesFilterOptions(),
    staleTime: 60000, // Cache for 1 minute
  });

  const { data: seriesData, isLoading } = useQuery({
    queryKey: ['series', search, statusFilter, publisherFilter, sortKey, sortDir],
    queryFn: () => api.getSeries({ 
      search, 
      page: 1, 
      pageSize: 50,
      status: statusFilter !== 'all' ? statusFilter : undefined,
      publisher: publisherFilter !== 'all' ? publisherFilter : undefined,
      sortKey,
      sortDir,
      includePathMismatch: true,
    }),
  });

  const hasActiveFilters = statusFilter !== 'all' || publisherFilter !== 'all';

  const clearFilters = () => {
    setStatusFilter('all');
    setPublisherFilter('all');
  };

  const deleteMutation = useMutation({
    mutationFn: api.deleteSeries,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['series'] });
      setSelectedIds(new Set());
    },
  });

  const refreshAllMutation = useMutation({
    mutationFn: () => api.refreshAllSeriesMetadata(true),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['series'] });
    },
  });

  const series = seriesData?.items ?? [];
  const allSelected = series.length > 0 && selectedIds.size === series.length;
  const someSelected = selectedIds.size > 0;
  const selectAllRef = useRef<HTMLInputElement>(null);

  // Set indeterminate state on checkbox (can't be done via JSX)
  useEffect(() => {
    if (selectAllRef.current) {
      selectAllRef.current.indeterminate = someSelected && !allSelected;
    }
  }, [someSelected, allSelected]);

  const toggleSelectAll = () => {
    // If any items are selected, clear selection; otherwise select all
    if (someSelected) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(series.map((s) => s.id)));
    }
  };

  const toggleSelect = (id: number) => {
    const newSelected = new Set(selectedIds);
    if (newSelected.has(id)) {
      newSelected.delete(id);
    } else {
      newSelected.add(id);
    }
    setSelectedIds(newSelected);
  };

  const handleDeleteSelected = () => {
    if (selectedIds.size === 0) return;
    if (!confirm(`Delete ${selectedIds.size} series? This cannot be undone.`)) return;
    
    selectedIds.forEach((id) => deleteMutation.mutate(id));
  };

  const handleSeriesAdded = useCallback(async () => {
    // Force refetch BEFORE closing modal to ensure new series appears
    await queryClient.refetchQueries({ queryKey: ['series'], type: 'active' });
    await queryClient.invalidateQueries({ queryKey: ['dashboard-stats'] });
    await queryClient.invalidateQueries({ queryKey: ['series-filter-options'] });
    // Now close modal after data is refreshed
    setShowAddModal(false);
  }, [queryClient]);

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">Series</h1>
        <div className="toolbar-group">
          <button className="btn btn-primary" onClick={() => setShowAddModal(true)}>
            <Plus size={16} />
            Add Series
          </button>
        </div>
      </header>
      
      <div className="page-content">
        <div className="toolbar">
          <input
            type="text"
            className="input search-input"
            placeholder="Search series..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          
          {/* Filter Toggle Button */}
          <button 
            className={`btn ${showFilters ? 'btn-primary' : ''} ${hasActiveFilters ? 'btn-active' : ''}`}
            onClick={() => setShowFilters(!showFilters)}
            title="Toggle Filters"
          >
            <Filter size={16} />
            Filters
            {hasActiveFilters && (
              <span className="badge badge-primary" style={{ marginLeft: '4px', padding: '2px 6px', fontSize: '11px' }}>
                {(statusFilter !== 'all' ? 1 : 0) + (publisherFilter !== 'all' ? 1 : 0)}
              </span>
            )}
          </button>

          {/* Sort Dropdown */}
          <div className="dropdown">
            <button className="btn" title="Sort By">
              <ArrowUpDown size={16} />
              Sort: {filterOptions?.sortOptions?.find(o => o.value === sortKey)?.label || 'Title'}
              {sortDir === 'asc' ? <ArrowUp size={14} /> : <ArrowDown size={14} />}
            </button>
            <div className="dropdown-content" style={{ minWidth: '180px' }}>
              {(filterOptions?.sortOptions || [
                { value: 'title', label: 'Title' },
                { value: 'startyear', label: 'Start Year' },
                { value: 'createdat', label: 'Date Added' },
                { value: 'status', label: 'Status' },
                { value: 'publisher', label: 'Publisher' },
                { value: 'issuecount', label: 'Issue Count' },
              ]).map(option => (
                <button
                  key={option.value}
                  className={`dropdown-item ${sortKey === option.value ? 'active' : ''}`}
                  onClick={() => {
                    if (sortKey === option.value) {
                      setSortDir(sortDir === 'asc' ? 'desc' : 'asc');
                    } else {
                      setSortKey(option.value);
                      setSortDir('asc');
                    }
                  }}
                >
                  {option.label}
                  {sortKey === option.value && (
                    sortDir === 'asc' ? <ArrowUp size={14} /> : <ArrowDown size={14} />
                  )}
                </button>
              ))}
            </div>
          </div>
          
          <div className="toolbar-spacer" />
          
          {selectedIds.size > 0 && (
            <div className="toolbar-group">
              <span style={{ color: 'var(--text-muted)', fontSize: '13px' }}>
                {selectedIds.size} selected
              </span>
              <button 
                className="btn" 
                onClick={() => setShowOrganizeModal(true)}
                title="Organize files for selected series"
              >
                <FolderSync size={16} />
                Organize
              </button>
              <button 
                className="btn btn-danger" 
                onClick={handleDeleteSelected}
                disabled={deleteMutation.isPending}
              >
                <Trash2 size={16} />
                Delete
              </button>
            </div>
          )}
          
          <button 
            className="btn btn-icon" 
            onClick={() => refreshAllMutation.mutate()} 
            title="Refresh All Series Metadata from ComicVine"
            disabled={refreshAllMutation.isPending}
          >
            <RefreshCw size={18} className={refreshAllMutation.isPending ? 'spinning' : ''} />
          </button>
        </div>
        
        {/* Filter Panel */}
        {showFilters && (
          <div className="filter-panel" style={{ 
            display: 'flex', 
            gap: '16px', 
            padding: '12px 16px', 
            background: 'var(--bg-secondary)', 
            borderRadius: '8px',
            marginBottom: '16px',
            alignItems: 'center',
            flexWrap: 'wrap'
          }}>
            <div className="form-group" style={{ marginBottom: 0, minWidth: '150px' }}>
              <label className="form-label" style={{ marginBottom: '4px', fontSize: '12px' }}>Status</label>
              <select 
                className="input"
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
              >
                <option value="all">All Statuses</option>
                {(filterOptions?.statuses || []).map(status => (
                  <option key={status.label} value={status.label}>
                    {status.label} ({status.count})
                  </option>
                ))}
              </select>
            </div>
            
            <div className="form-group" style={{ marginBottom: 0, minWidth: '200px' }}>
              <label className="form-label" style={{ marginBottom: '4px', fontSize: '12px' }}>Publisher</label>
              <select 
                className="input"
                value={publisherFilter}
                onChange={(e) => setPublisherFilter(e.target.value)}
              >
                <option value="all">All Publishers</option>
                {(filterOptions?.publishers || []).map(pub => (
                  <option key={pub.value} value={pub.value}>
                    {pub.label}
                  </option>
                ))}
              </select>
            </div>
            
            {hasActiveFilters && (
              <button 
                className="btn btn-ghost" 
                onClick={clearFilters}
                style={{ marginTop: '16px' }}
              >
                <X size={14} />
                Clear Filters
              </button>
            )}
          </div>
        )}
        
        <div className="table-container">
          {isLoading ? (
            <div className="loading"><div className="spinner" /></div>
          ) : series.length === 0 ? (
            <div className="empty-state">
              <BookOpen size={48} />
              <div className="empty-state-title">No series found</div>
              <div className="empty-state-text">
                {search ? 'Try a different search term.' : 'Add a series to get started.'}
              </div>
              {!search && (
                <button className="btn btn-primary" style={{ marginTop: '16px' }} onClick={() => setShowAddModal(true)}>
                  <Plus size={16} />
                  Add Series
                </button>
              )}
            </div>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th className="table-checkbox">
                    <input 
                      ref={selectAllRef}
                      type="checkbox" 
                      checked={allSelected}
                      onChange={toggleSelectAll}
                    />
                  </th>
                  <th>Title</th>
                  <th>Year</th>
                  <th>Publisher</th>
                  <th>Status</th>
                  <th>Issues</th>
                  <th>Files</th>
                  <th style={{ width: '50px', textAlign: 'center' }} title="Path Status">Path</th>
                  <th className="table-actions"></th>
                </tr>
              </thead>
              <tbody>
                {series.map((item) => (
                  <tr key={item.id} className="table-row-clickable" onClick={() => navigate(`/series/${item.id}`)}>
                    <td className="table-checkbox" onClick={(e) => e.stopPropagation()}>
                      <input
                        type="checkbox"
                        checked={selectedIds.has(item.id)}
                        onChange={() => toggleSelect(item.id)}
                      />
                    </td>
                    <td style={{ color: 'var(--text-primary)', fontWeight: 500 }}>
                      {item.title}
                    </td>
                    <td>{item.year ?? '-'}</td>
                    <td>{item.publisher ?? '-'}</td>
                    <td>
                      <span className={`badge badge-${getStatusBadge(item.status)}`}>
                        {item.status}
                      </span>
                    </td>
                    <td>{item.issueCount + item.upcomingIssueCount}{item.upcomingIssueCount > 0 && <span style={{ color: 'var(--text-muted)', fontSize: '11px' }}> ({item.upcomingIssueCount} upcoming)</span>}</td>
                    <td>{item.filesCount}</td>
                    <td style={{ textAlign: 'center' }}>
                      {item.pathMismatch === true ? (
                        <span 
                          title={`Path mismatch!\nCurrent: ${item.currentPath || '(none)'}\nExpected: ${item.expectedPath}`}
                          style={{ color: 'var(--warning)', cursor: 'help' }}
                        >
                          <FolderX size={16} />
                        </span>
                      ) : item.pathMismatch === false ? (
                        <span 
                          title="Path matches format"
                          style={{ color: 'var(--success)', cursor: 'help' }}
                        >
                          <Check size={14} />
                        </span>
                      ) : (
                        <span style={{ color: 'var(--text-muted)' }}>-</span>
                      )}
                    </td>
                    <td className="table-actions" onClick={(e) => e.stopPropagation()}>
                      <button className="btn btn-icon" title="Edit">
                        <Edit size={16} />
                      </button>
                      <button 
                        className="btn btn-icon" 
                        title="Delete Series"
                        onClick={() => {
                          if (confirm(`Delete "${item.title}"? This cannot be undone.`)) {
                            deleteMutation.mutate(item.id);
                          }
                        }}
                      >
                        <Trash2 size={16} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
        
        {seriesData && seriesData.totalCount > seriesData.items.length && (
          <div style={{ marginTop: '16px', textAlign: 'center', color: 'var(--text-muted)' }}>
            Showing {series.length} of {seriesData.totalCount} series
          </div>
        )}
      </div>

      {showAddModal && (
        <AddSeriesModal onClose={() => setShowAddModal(false)} onAdded={handleSeriesAdded} />
      )}

      {showOrganizeModal && (
        <BulkOrganizeModal
          seriesIds={Array.from(selectedIds)}
          seriesList={series.filter(s => selectedIds.has(s.id))}
          onClose={() => setShowOrganizeModal(false)}
          onOrganized={async () => {
            await queryClient.invalidateQueries({ queryKey: ['series'] });
            setSelectedIds(new Set());
            setShowOrganizeModal(false);
          }}
        />
      )}
    </>
  );
}

function getStatusBadge(status: string): string {
  switch (status.toLowerCase()) {
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

// Add Series Modal Component
interface AddSeriesModalProps {
  onClose: () => void;
  onAdded: () => Promise<void>;
}

type SortOption = 'relevance' | 'popularity' | 'year-desc' | 'year-asc' | 'name';

function AddSeriesModal({ onClose, onAdded }: AddSeriesModalProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [selectedSeries, setSelectedSeries] = useState<SeriesMatchCandidate | null>(null);
  const [addError, setAddError] = useState<string | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [sortBy, setSortBy] = useState<SortOption>('popularity');
  const [compactView, setCompactView] = useState(true);

  // Debounce search query
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedQuery(searchQuery);
    }, 400);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  const { data: searchResults, isLoading: isSearching, error: searchError } = useQuery({
    queryKey: ['comicvine-search', debouncedQuery],
    queryFn: () => api.searchSeriesFromComicVine(debouncedQuery, { limit: 50 }),
    enabled: debouncedQuery.length >= 2,
    staleTime: 60000,
  });
  
  // Sort results based on selected option
  const sortedResults = useMemo(() => {
    const results = searchResults?.results ?? [];
    if (results.length === 0) return results;
    
    const sorted = [...results];
    switch (sortBy) {
      case 'popularity':
        // Sort by issue count (more issues = more popular/established)
        return sorted.sort((a, b) => (b.issueCount || 0) - (a.issueCount || 0));
      case 'year-desc':
        // Newest first
        return sorted.sort((a, b) => (b.startYear || 0) - (a.startYear || 0));
      case 'year-asc':
        // Oldest first
        return sorted.sort((a, b) => (a.startYear || 0) - (b.startYear || 0));
      case 'name':
        return sorted.sort((a, b) => a.title.localeCompare(b.title));
      case 'relevance':
      default:
        // Keep original order (by confidence score)
        return sorted;
    }
  }, [searchResults?.results, sortBy]);

  const addSeriesMutation = useMutation({
    mutationFn: (comicVineId: number) => api.addSeriesFromComicVine(comicVineId, {
      monitored: true,
      monitoringMode: 'AllIssues',
    }),
    onSuccess: async (result) => {
      if (result.success) {
        // Show refreshing state while waiting for list to update
        setIsRefreshing(true);
        try {
          // Parent handles refetch - await it to keep modal open until data refreshes
          await onAdded();
        } finally {
          setIsRefreshing(false);
        }
      } else if (result.alreadyExists) {
        setAddError(`This series already exists in your library (ID: ${result.existingSeriesId})`);
      } else {
        setAddError(result.error || 'Failed to add series');
      }
    },
    onError: (e) => {
      setAddError(e instanceof Error ? e.message : 'Failed to add series');
    },
  });

  const handleAdd = useCallback(() => {
    if (!selectedSeries) return;
    setAddError(null);
    addSeriesMutation.mutate(selectedSeries.comicVineId);
  }, [selectedSeries, addSeriesMutation]);

  const isAdding = addSeriesMutation.isPending || isRefreshing;

  // Close on escape key
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  const hasResults = sortedResults.length > 0;
  const showNoResults = debouncedQuery.length >= 2 && !isSearching && !hasResults && !searchError;
  const showApiKeyWarning = searchResults && !searchResults.success && searchResults.error?.toLowerCase().includes('api key');

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal modal-large" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2 className="modal-title">Add Series</h2>
          <button className="btn btn-icon" onClick={onClose}>
            <X size={20} />
          </button>
        </div>

        <div className="modal-body" style={{ display: 'flex', flexDirection: 'column', gap: '16px', minHeight: '500px' }}>
          {/* Search Input */}
          <div className="form-group" style={{ marginBottom: 0 }}>
            <label className="form-label">Search ComicVine</label>
            <div style={{ position: 'relative' }}>
              <Search size={18} style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }} />
              <input
                type="text"
                className="input"
                placeholder="Search for a series (e.g., Batman, Spider-Man, Saga)..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                autoFocus
                style={{ paddingLeft: '40px' }}
              />
              {isSearching && (
                <Loader2 size={18} style={{ position: 'absolute', right: '12px', top: '50%', transform: 'translateY(-50%)', color: 'var(--accent-primary)' }} className="spin" />
              )}
            </div>
            <div className="form-hint">
              Search ComicVine for series to add to your library. Enter at least 2 characters.
            </div>
          </div>

          {/* API Key Warning */}
          {showApiKeyWarning && (
            <div className="alert alert-warning" style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <AlertCircle size={20} />
              <div>
                <strong>ComicVine API Key Required</strong>
                <p style={{ margin: '4px 0 0', opacity: 0.9 }}>
                  Please configure your ComicVine API key in Settings &gt; ComicVine to search for series.
                </p>
              </div>
            </div>
          )}

          {/* Sort & View Controls */}
          {hasResults && (
            <div style={{ 
              display: 'flex', 
              alignItems: 'center', 
              justifyContent: 'space-between',
              padding: '8px 0',
              borderBottom: '1px solid var(--border-color)',
              fontSize: '13px'
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                <span style={{ color: 'var(--text-muted)' }}>{sortedResults.length} results</span>
                <select 
                  value={sortBy} 
                  onChange={(e) => setSortBy(e.target.value as SortOption)}
                  className="input"
                  style={{ padding: '4px 8px', fontSize: '12px', width: 'auto' }}
                >
                  <option value="popularity">Most Issues</option>
                  <option value="relevance">Best Match</option>
                  <option value="year-desc">Newest First</option>
                  <option value="year-asc">Oldest First</option>
                  <option value="name">Name A-Z</option>
                </select>
              </div>
              <button 
                className="btn btn-sm btn-icon"
                onClick={() => setCompactView(!compactView)}
                title={compactView ? 'Show larger covers' : 'Compact view'}
                style={{ padding: '4px 8px' }}
              >
                {compactView ? <Grid size={14} /> : <List size={14} />}
              </button>
            </div>
          )}

          {/* Search Results */}
          <div style={{ flex: 1, overflow: 'auto', minHeight: 0 }}>
            {showNoResults && (
              <div className="empty-state" style={{ padding: '40px 20px' }}>
                <Search size={48} style={{ opacity: 0.3 }} />
                <div className="empty-state-title">No results found</div>
                <div className="empty-state-text">
                  Try a different search term or check your spelling.
                </div>
              </div>
            )}

            {hasResults && (
              <div className={`series-search-results ${compactView ? 'compact' : ''}`}>
                {sortedResults.map((candidate) => (
                  <SeriesSearchResult
                    key={candidate.comicVineId}
                    candidate={candidate}
                    isSelected={selectedSeries?.comicVineId === candidate.comicVineId}
                    onSelect={() => setSelectedSeries(candidate)}
                    compact={compactView}
                  />
                ))}
              </div>
            )}

            {debouncedQuery.length < 2 && !showApiKeyWarning && (
              <div className="empty-state" style={{ padding: '60px 20px' }}>
                <BookOpen size={48} style={{ opacity: 0.3 }} />
                <div className="empty-state-title">Search for a series</div>
                <div className="empty-state-text">
                  Enter a series name above to search ComicVine.
                </div>
              </div>
            )}
          </div>

          {/* Error Message */}
          {addError && (
            <div className="alert alert-danger" style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <AlertCircle size={20} />
              <span>{addError}</span>
            </div>
          )}
        </div>

        <div className="modal-footer">
          <button className="btn" onClick={onClose}>
            Cancel
          </button>
          <button
            className="btn btn-primary"
            onClick={handleAdd}
            disabled={!selectedSeries || isAdding}
          >
            {isRefreshing ? (
              <>
                <Loader2 size={16} className="spin" />
                Refreshing list...
              </>
            ) : addSeriesMutation.isPending ? (
              <>
                <Loader2 size={16} className="spin" />
                Adding...
              </>
            ) : selectedSeries ? (
              <>
                <Plus size={16} />
                Add "{selectedSeries.title}"
              </>
            ) : (
              <>
                <Plus size={16} />
                Add Series
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
}

// Series Search Result Card
interface SeriesSearchResultProps {
  candidate: SeriesMatchCandidate;
  isSelected: boolean;
  onSelect: () => void;
  compact?: boolean;
}

function SeriesSearchResult({ candidate, isSelected, onSelect, compact = false }: SeriesSearchResultProps) {
  const placeholderCover = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="100" height="150" viewBox="0 0 100 150"%3E%3Crect fill="%232a2d35" width="100" height="150"/%3E%3Ctext fill="%236b7280" font-family="sans-serif" font-size="10" x="50" y="75" text-anchor="middle"%3ENo Cover%3C/text%3E%3C/svg%3E';
  
  return (
    <div
      className={`series-search-result ${isSelected ? 'selected' : ''} ${compact ? 'compact' : ''}`}
      onClick={onSelect}
    >
      <img
        src={candidate.coverImageUrl || placeholderCover}
        alt={candidate.title}
        className="series-search-result-cover"
        loading="lazy"
        decoding="async"
        onError={(e) => {
          (e.target as HTMLImageElement).src = placeholderCover;
        }}
      />
      <div className="series-search-result-info">
        <div className="series-search-result-title">
          {candidate.title}
          {candidate.startYear && <span className="series-search-result-year">({candidate.startYear})</span>}
        </div>
        <div className="series-search-result-meta">
          {candidate.publisher && (
            <span className="series-search-result-publisher">{candidate.publisher}</span>
          )}
          <span className="series-search-result-issues">{candidate.issueCount} issues</span>
          {candidate.siteDetailUrl && (
            <a
              href={candidate.siteDetailUrl}
              target="_blank"
              rel="noopener noreferrer"
              onClick={(e) => e.stopPropagation()}
              className="series-search-result-link"
            >
              <ExternalLink size={12} />
              CV
            </a>
          )}
        </div>
        {!compact && candidate.description && (
          <div className="series-search-result-description">
            {stripHtml(candidate.description).slice(0, 150)}
            {candidate.description.length > 150 && '...'}
          </div>
        )}
      </div>
    </div>
  );
}

function stripHtml(html: string): string {
  const doc = new DOMParser().parseFromString(html, 'text/html');
  return doc.body.textContent || '';
}

// Bulk Organize Modal Component
interface BulkOrganizeModalProps {
  seriesIds: number[];
  seriesList: Array<{ id: number; title: string }>;
  onClose: () => void;
  onOrganized: () => Promise<void>;
}

function BulkOrganizeModal({ seriesIds, seriesList: _seriesList, onClose, onOrganized }: BulkOrganizeModalProps) {
  void _seriesList; // Available for future use (e.g., fallback display)
  const [isExecuting, setIsExecuting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [executionResults, setExecutionResults] = useState<{
    successful: number;
    failed: number;
    totalFilesRenamed: number;
    errors: string[];
  } | null>(null);

  const { data: preview, isLoading } = useQuery({
    queryKey: ['series', 'organize', 'bulk-preview', seriesIds.join(',')],
    queryFn: () => api.getBulkOrganizePreview(seriesIds),
  });

  const executeMutation = useMutation({
    mutationFn: () => api.executeBulkOrganize(seriesIds),
    onSuccess: async (result) => {
      const errors = result.results
        .filter(r => !r.success && r.error)
        .map(r => `${r.seriesTitle}: ${r.error}`);
      
      setExecutionResults({
        successful: result.successful,
        failed: result.failed,
        totalFilesRenamed: result.totalFilesRenamed,
        errors,
      });

      if (result.successful > 0) {
        setIsExecuting(true);
        try {
          await onOrganized();
        } finally {
          setIsExecuting(false);
        }
      }
    },
    onError: (e) => {
      setError(e instanceof Error ? e.message : 'Failed to organize files');
    },
  });

  const handleExecute = () => {
    setError(null);
    setExecutionResults(null);
    executeMutation.mutate();
  };

  const formatBytes = (bytes: number): string => {
    if (bytes >= 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
    if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    if (bytes >= 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${bytes} B`;
  };

  const isPending = executeMutation.isPending || isExecuting;
  const hasChanges = preview && (preview.seriesWithChanges > 0 || preview.filesWithChanges > 0);
  const noChangesNeeded = preview && !hasChanges && !preview.hasErrors;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: '650px' }}>
        <div className="modal-header">
          <h2 className="modal-title">Organize Files</h2>
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
              {seriesIds.length} Series Selected
            </div>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
              Rename files and folders to match current naming format settings
            </div>
          </div>

          {isLoading ? (
            <div className="loading" style={{ padding: '40px 0' }}>
              <div className="spinner" />
              <div style={{ marginTop: '12px', color: 'var(--text-muted)' }}>Analyzing {seriesIds.length} series...</div>
            </div>
          ) : executionResults ? (
            // Show execution results
            <div>
              {executionResults.successful > 0 && (
                <div className="alert alert-success" style={{ marginBottom: '16px', padding: '12px 14px', display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <Check size={20} />
                  <div>
                    <strong>Organization Complete</strong>
                    <div style={{ fontSize: '13px', marginTop: '4px' }}>
                      {executionResults.successful} series organized, {executionResults.totalFilesRenamed} files renamed
                    </div>
                  </div>
                </div>
              )}
              
              {executionResults.failed > 0 && (
                <div className="alert alert-danger" style={{ padding: '12px 14px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: executionResults.errors.length > 0 ? '12px' : 0 }}>
                    <AlertCircle size={20} />
                    <strong>{executionResults.failed} series failed</strong>
                  </div>
                  {executionResults.errors.length > 0 && (
                    <ul style={{ margin: '0', paddingLeft: '20px', fontSize: '13px' }}>
                      {executionResults.errors.slice(0, 5).map((err, idx) => (
                        <li key={idx}>{err}</li>
                      ))}
                      {executionResults.errors.length > 5 && (
                        <li style={{ color: 'var(--text-muted)' }}>...and {executionResults.errors.length - 5} more</li>
                      )}
                    </ul>
                  )}
                </div>
              )}
            </div>
          ) : preview ? (
            <>
              {error && (
                <div className="alert alert-danger" style={{ marginBottom: '16px', padding: '10px 14px' }}>
                  {error}
                </div>
              )}

              {preview.hasErrors && (
                <div className="alert alert-warning" style={{ marginBottom: '16px', padding: '10px 14px', display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <AlertTriangle size={18} />
                  <span>Some series have errors and will be skipped</span>
                </div>
              )}

              {noChangesNeeded ? (
                <div style={{ 
                  textAlign: 'center', 
                  padding: '32px 20px',
                  background: 'var(--bg-tertiary)',
                  borderRadius: 'var(--radius-md)'
                }}>
                  <Check size={48} style={{ color: 'var(--accent-success)', marginBottom: '12px' }} />
                  <div style={{ fontSize: '16px', fontWeight: 500, color: 'var(--text-primary)' }}>
                    All files are already organized
                  </div>
                  <div style={{ fontSize: '13px', color: 'var(--text-muted)', marginTop: '8px' }}>
                    All {seriesIds.length} series match the current naming format
                  </div>
                </div>
              ) : (
                <>
                  {/* Summary Stats */}
                  <div style={{ 
                    display: 'grid', 
                    gridTemplateColumns: 'repeat(3, 1fr)', 
                    gap: '12px',
                    marginBottom: '16px'
                  }}>
                    <div style={{ 
                      background: 'var(--bg-tertiary)', 
                      padding: '16px', 
                      borderRadius: 'var(--radius-sm)',
                      textAlign: 'center'
                    }}>
                      <div style={{ fontSize: '24px', fontWeight: 600, color: 'var(--accent-primary)' }}>
                        {preview.seriesWithChanges}
                      </div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
                        Series to update
                      </div>
                    </div>
                    <div style={{ 
                      background: 'var(--bg-tertiary)', 
                      padding: '16px', 
                      borderRadius: 'var(--radius-sm)',
                      textAlign: 'center'
                    }}>
                      <div style={{ fontSize: '24px', fontWeight: 600, color: 'var(--accent-primary)' }}>
                        {preview.filesWithChanges}
                      </div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
                        Files to rename
                      </div>
                    </div>
                    <div style={{ 
                      background: 'var(--bg-tertiary)', 
                      padding: '16px', 
                      borderRadius: 'var(--radius-sm)',
                      textAlign: 'center'
                    }}>
                      <div style={{ fontSize: '24px', fontWeight: 600, color: 'var(--text-primary)' }}>
                        {formatBytes(preview.previews.reduce((sum, p) => sum + p.totalSize, 0))}
                      </div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
                        Total size
                      </div>
                    </div>
                  </div>

                  {/* Series List */}
                  <div style={{ 
                    fontSize: '12px', 
                    color: 'var(--text-muted)', 
                    marginBottom: '8px', 
                    textTransform: 'uppercase', 
                    letterSpacing: '0.5px'
                  }}>
                    Series Changes
                  </div>
                  <div style={{ 
                    maxHeight: '250px', 
                    overflow: 'auto',
                    background: 'var(--bg-tertiary)', 
                    borderRadius: 'var(--radius-sm)'
                  }}>
                    {preview.previews.map((p, idx) => (
                      <div 
                        key={p.seriesId}
                        style={{
                          padding: '12px',
                          borderBottom: idx < preview.previews.length - 1 ? '1px solid var(--border-color)' : 'none',
                        }}
                      >
                        <div style={{ 
                          display: 'flex', 
                          justifyContent: 'space-between', 
                          alignItems: 'center',
                          marginBottom: p.errors.length > 0 || (p.willMove || p.willCreate) ? '8px' : 0
                        }}>
                          <span style={{ fontWeight: 500, color: 'var(--text-primary)' }}>
                            {p.seriesTitle}
                          </span>
                          {p.canRename ? (
                            <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                              {p.files.filter(f => f.willRename || f.willMove).length} files
                            </span>
                          ) : (
                            <span className="badge badge-danger" style={{ fontSize: '10px' }}>Error</span>
                          )}
                        </div>
                        
                        {p.errors.length > 0 && (
                          <div style={{ fontSize: '12px', color: 'var(--accent-danger)' }}>
                            {p.errors[0]}
                          </div>
                        )}
                        
                        {p.willMove && (
                          <div style={{ fontSize: '11px', color: 'var(--accent-success)' }}>
                            → {p.newPath}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </>
              )}
            </>
          ) : (
            <div className="alert alert-danger" style={{ padding: '10px 14px' }}>
              Failed to load preview
            </div>
          )}
        </div>

        <div className="modal-footer">
          <button className="btn" onClick={onClose} disabled={isPending}>
            {executionResults ? 'Close' : 'Cancel'}
          </button>
          {!executionResults && (
            <button 
              className="btn btn-primary" 
              onClick={handleExecute} 
              disabled={!preview || preview.seriesWithChanges === 0 || isPending || !!noChangesNeeded}
            >
              {isPending ? (
                <>
                  <Loader2 size={16} className="spinning" />
                  Organizing...
                </>
              ) : noChangesNeeded ? (
                <>
                  <Check size={16} />
                  No Changes Needed
                </>
              ) : (
                <>
                  <FolderSync size={16} />
                  Organize {preview?.seriesWithChanges ?? 0} Series
                </>
              )}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
