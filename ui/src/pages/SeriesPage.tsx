import { useState, useEffect, useCallback } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { Plus, RefreshCw, Trash2, Edit, MoreVertical, BookOpen, Search, X, Loader2, AlertCircle, ExternalLink } from 'lucide-react';
import { api } from '../api/client';
import type { SeriesMatchCandidate } from '../api/client';

export function SeriesPage() {
  const [search, setSearch] = useState('');
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [showAddModal, setShowAddModal] = useState(false);
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const { data: seriesData, isLoading } = useQuery({
    queryKey: ['series', search],
    queryFn: () => api.getSeries({ search, page: 1, pageSize: 50 }),
  });

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

  const toggleSelectAll = () => {
    if (allSelected) {
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

  const handleSeriesAdded = () => {
    setShowAddModal(false);
    queryClient.invalidateQueries({ queryKey: ['series'] });
  };

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
          
          <div className="toolbar-spacer" />
          
          {selectedIds.size > 0 && (
            <div className="toolbar-group">
              <span style={{ color: 'var(--text-muted)', fontSize: '13px' }}>
                {selectedIds.size} selected
              </span>
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
                    <td>{item.issueCount}</td>
                    <td>{item.filesCount}</td>
                    <td className="table-actions">
                      <button className="btn btn-icon" title="Edit">
                        <Edit size={16} />
                      </button>
                      <button className="btn btn-icon" title="More">
                        <MoreVertical size={16} />
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
  onAdded: () => void;
}

function AddSeriesModal({ onClose, onAdded }: AddSeriesModalProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [selectedSeries, setSelectedSeries] = useState<SeriesMatchCandidate | null>(null);
  const [isAdding, setIsAdding] = useState(false);
  const [addError, setAddError] = useState<string | null>(null);

  // Debounce search query
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedQuery(searchQuery);
    }, 400);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  const { data: searchResults, isLoading: isSearching, error: searchError } = useQuery({
    queryKey: ['comicvine-search', debouncedQuery],
    queryFn: () => api.searchSeriesFromComicVine(debouncedQuery, { limit: 20 }),
    enabled: debouncedQuery.length >= 2,
    staleTime: 60000,
  });

  const handleAdd = useCallback(async () => {
    if (!selectedSeries) return;
    
    setIsAdding(true);
    setAddError(null);

    try {
      const result = await api.addSeriesFromComicVine(selectedSeries.comicVineId, {
        monitored: true,
        monitoringMode: 'AllIssues',
      });

      if (result.success) {
        onAdded();
      } else if (result.alreadyExists) {
        setAddError(`This series already exists in your library (ID: ${result.existingSeriesId})`);
      } else {
        setAddError(result.error || 'Failed to add series');
      }
    } catch (e) {
      setAddError(e instanceof Error ? e.message : 'Failed to add series');
    } finally {
      setIsAdding(false);
    }
  }, [selectedSeries, onAdded]);

  // Close on escape key
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  const results = searchResults?.results ?? [];
  const hasResults = results.length > 0;
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
              <div className="series-search-results">
                {results.map((candidate) => (
                  <SeriesSearchResult
                    key={candidate.comicVineId}
                    candidate={candidate}
                    isSelected={selectedSeries?.comicVineId === candidate.comicVineId}
                    onSelect={() => setSelectedSeries(candidate)}
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
            {isAdding ? (
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
}

function SeriesSearchResult({ candidate, isSelected, onSelect }: SeriesSearchResultProps) {
  const placeholderCover = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="100" height="150" viewBox="0 0 100 150"%3E%3Crect fill="%232a2d35" width="100" height="150"/%3E%3Ctext fill="%236b7280" font-family="sans-serif" font-size="10" x="50" y="75" text-anchor="middle"%3ENo Cover%3C/text%3E%3C/svg%3E';
  
  return (
    <div
      className={`series-search-result ${isSelected ? 'selected' : ''}`}
      onClick={onSelect}
    >
      <img
        src={candidate.coverImageUrl || placeholderCover}
        alt={candidate.title}
        className="series-search-result-cover"
        onError={(e) => {
          (e.target as HTMLImageElement).src = placeholderCover;
        }}
      />
      <div className="series-search-result-info">
        <div className="series-search-result-title">
          {candidate.title}
          {candidate.startYear && <span className="series-search-result-year">({candidate.startYear})</span>}
        </div>
        {candidate.publisher && (
          <div className="series-search-result-publisher">{candidate.publisher}</div>
        )}
        <div className="series-search-result-meta">
          <span>{candidate.issueCount} issues</span>
          {candidate.siteDetailUrl && (
            <a
              href={candidate.siteDetailUrl}
              target="_blank"
              rel="noopener noreferrer"
              onClick={(e) => e.stopPropagation()}
              className="series-search-result-link"
            >
              <ExternalLink size={12} />
              ComicVine
            </a>
          )}
        </div>
        {candidate.description && (
          <div className="series-search-result-description">
            {stripHtml(candidate.description).slice(0, 200)}
            {candidate.description.length > 200 && '...'}
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
