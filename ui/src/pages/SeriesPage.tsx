import { useState, useEffect, useRef } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Plus, RefreshCw, Trash2, Edit, BookOpen, Filter, ArrowUpDown, ArrowUp, ArrowDown, FolderSync, Check, AlertTriangle, FolderX, X, AlertCircle, Loader2 } from 'lucide-react';
import { api } from '../api/client';

export function SeriesPage() {
  const [search, setSearch] = useState('');
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
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


  return (
    <>
      <header className="page-header">
        <h1 className="page-title">Series</h1>
        <div className="toolbar-group">
          <button className="btn btn-primary" onClick={() => navigate('/series/add')}>
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
                <button className="btn btn-primary" style={{ marginTop: '16px' }} onClick={() => navigate('/series/add')}>
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
