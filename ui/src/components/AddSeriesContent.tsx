import { useState, useEffect, useCallback, useMemo, memo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { BookOpen, Search, Loader2, AlertCircle, ExternalLink, Plus, Grid, List } from 'lucide-react';
import { api } from '../api/client';
import type { SeriesMatchCandidate } from '../api/client';

export interface AddSeriesContentProps {
  onClose: () => void;
  onAdded: () => Promise<void>;
}

type SortOption = 'relevance' | 'popularity' | 'year-desc' | 'year-asc' | 'name';
type ViewMode = 'list' | 'grid';

function stripHtml(html: string): string {
  const doc = new DOMParser().parseFromString(html, 'text/html');
  return doc.body.textContent || '';
}

const PLACEHOLDER_COVER = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="100" height="150" viewBox="0 0 100 150"%3E%3Crect fill="%232a2d35" width="100" height="150"/%3E%3Ctext fill="%236b7280" font-family="sans-serif" font-size="10" x="50" y="75" text-anchor="middle"%3ENo Cover%3C/text%3E%3C/svg%3E';

const SeriesSearchResult = memo(function SeriesSearchResult({
  candidate,
  isSelected,
  onSelect,
  compact = false,
}: {
  candidate: SeriesMatchCandidate;
  isSelected: boolean;
  onSelect: () => void;
  compact?: boolean;
}) {
  const handleImageError = useCallback((e: React.SyntheticEvent<HTMLImageElement>) => {
    e.currentTarget.src = PLACEHOLDER_COVER;
  }, []);
  const handleLinkClick = useCallback((e: React.MouseEvent) => {
    e.stopPropagation();
  }, []);

  return (
    <div
      className={`series-search-result ${isSelected ? 'selected' : ''} ${compact ? 'compact' : ''}`}
      onClick={onSelect}
    >
      <img
        src={candidate.coverImageUrl || PLACEHOLDER_COVER}
        alt={candidate.title}
        className="series-search-result-cover"
        loading="lazy"
        decoding="async"
        onError={handleImageError}
      />
      <div className="series-search-result-info">
        <div className="series-search-result-title">
          {candidate.title}
          {candidate.startYear && <span className="series-search-result-year">({candidate.startYear})</span>}
        </div>
        <div className="series-search-result-meta">
          {candidate.publisher && <span className="series-search-result-publisher">{candidate.publisher}</span>}
          <span className="series-search-result-issues">{candidate.issueCount} issues</span>
          {candidate.siteDetailUrl && (
            <a href={candidate.siteDetailUrl} target="_blank" rel="noopener noreferrer" onClick={handleLinkClick} className="series-search-result-link">
              <ExternalLink size={12} /> CV
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
});

const SeriesSearchResultRow = memo(function SeriesSearchResultRow({
  candidate,
  isSelected,
  onToggle,
}: {
  candidate: SeriesMatchCandidate;
  isSelected: boolean;
  onToggle: () => void;
}) {
  const handleLinkClick = useCallback((e: React.MouseEvent) => {
    e.stopPropagation();
  }, []);

  return (
    <tr className={`add-series-row ${isSelected ? 'selected' : ''}`} onClick={onToggle}>
      <td className="col-checkbox" onClick={(e) => e.stopPropagation()}>
        <input type="checkbox" checked={isSelected} onChange={onToggle} />
      </td>
      <td className="col-title"><span className="series-title">{candidate.title}</span></td>
      <td className="col-year">{candidate.startYear || '—'}</td>
      <td className="col-publisher">{candidate.publisher || '—'}</td>
      <td className="col-issues">{candidate.issueCount || 0}</td>
      <td className="col-link">
        {candidate.siteDetailUrl && (
          <a href={candidate.siteDetailUrl} target="_blank" rel="noopener noreferrer" onClick={handleLinkClick} className="btn btn-icon btn-sm" title="View on ComicVine">
            <ExternalLink size={14} />
          </a>
        )}
      </td>
    </tr>
  );
});

export function AddSeriesContent({ onClose, onAdded }: AddSeriesContentProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [publisherFilter, setPublisherFilter] = useState('');
  const [yearStartFilter, setYearStartFilter] = useState<number | ''>('');
  const [yearEndFilter, setYearEndFilter] = useState<number | ''>('');
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [addError, setAddError] = useState<string | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [sortBy, setSortBy] = useState<SortOption>('year-desc');
  const [viewMode, setViewMode] = useState<ViewMode>('list');
  const [addingProgress, setAddingProgress] = useState<{ current: number; total: number } | null>(null);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedQuery(searchQuery), 400);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  const { data: searchResults, isLoading: isSearching, error: searchError } = useQuery({
    queryKey: ['comicvine-search', debouncedQuery, publisherFilter || null, yearStartFilter === '' ? null : yearStartFilter, yearEndFilter === '' ? null : yearEndFilter],
    queryFn: () =>
      api.searchSeriesFromComicVine(debouncedQuery, {
        limit: 50,
        publisher: publisherFilter.trim() || undefined,
        yearStart: yearStartFilter === '' ? undefined : Number(yearStartFilter),
        yearEnd: yearEndFilter === '' ? undefined : Number(yearEndFilter),
      }),
    enabled: debouncedQuery.length >= 2,
    staleTime: 60000,
  });

  const sortedResults = useMemo(() => {
    const results = searchResults?.results ?? [];
    if (results.length === 0) return results;
    const sorted = [...results];
    switch (sortBy) {
      case 'popularity': return sorted.sort((a, b) => (b.issueCount || 0) - (a.issueCount || 0));
      case 'year-desc': return sorted.sort((a, b) => (b.startYear || 0) - (a.startYear || 0));
      case 'year-asc': return sorted.sort((a, b) => (a.startYear || 0) - (b.startYear || 0));
      case 'name': return sorted.sort((a, b) => a.title.localeCompare(b.title));
      default: return sorted;
    }
  }, [searchResults?.results, sortBy]);

  const toggleSelection = useCallback((comicVineId: number) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(comicVineId)) next.delete(comicVineId);
      else next.add(comicVineId);
      return next;
    });
  }, []);

  const toggleSelectAll = useCallback(() => {
    if (selectedIds.size === sortedResults.length) setSelectedIds(new Set());
    else setSelectedIds(new Set(sortedResults.map(s => s.comicVineId)));
  }, [selectedIds.size, sortedResults]);

  const handleAddSelected = useCallback(async () => {
    if (selectedIds.size === 0) return;
    setAddError(null);
    const idsToAdd = Array.from(selectedIds);
    setAddingProgress({ current: 0, total: idsToAdd.length });
    const errors: string[] = [];
    let successCount = 0;
    for (let i = 0; i < idsToAdd.length; i++) {
      setAddingProgress({ current: i + 1, total: idsToAdd.length });
      try {
        const result = await api.addSeriesFromComicVine(idsToAdd[i], { monitored: true, monitoringMode: 'AllIssues' });
        if (result.success || result.alreadyExists) {
          successCount++;
          setSelectedIds(prev => { const n = new Set(prev); n.delete(idsToAdd[i]); return n; });
        } else {
          errors.push(result.error || `Failed to add series ${idsToAdd[i]}`);
        }
      } catch (e) {
        errors.push(e instanceof Error ? e.message : `Failed to add series ${idsToAdd[i]}`);
      }
    }
    setAddingProgress(null);
    if (errors.length > 0) setAddError(`Added ${successCount} series. Errors: ${errors.join('; ')}`);
    if (successCount > 0) {
      setIsRefreshing(true);
      try { await onAdded(); } finally { setIsRefreshing(false); }
    }
  }, [selectedIds, onAdded]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  const isAdding = addingProgress !== null || isRefreshing;
  const hasResults = sortedResults.length > 0;
  const showNoResults = debouncedQuery.length >= 2 && !isSearching && !hasResults && !searchError;
  const showApiKeyWarning = searchResults && !searchResults.success && searchResults.error?.toLowerCase().includes('api key');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', minHeight: 0, flex: 1 }}>
      <div className="form-group" style={{ marginBottom: 0 }}>
        <label className="form-label">Search ComicVine</label>
        <div style={{ position: 'relative' }}>
          <Search size={18} style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }} />
          <input
            type="text"
            className="input"
            placeholder="Search by title or ComicVine ID (e.g. Batman, 4050-12345)..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            autoFocus
            style={{ paddingLeft: '40px' }}
          />
          {isSearching && <Loader2 size={18} style={{ position: 'absolute', right: '12px', top: '50%', transform: 'translateY(-50%)', color: 'var(--accent-primary)' }} className="spin" />}
        </div>
        <div className="form-hint">Search ComicVine for series to add to your library. Enter at least 2 characters.</div>
      </div>

      <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '12px', fontSize: '13px' }}>
        <label style={{ display: 'flex', alignItems: 'center', gap: '6px', color: 'var(--text-muted)' }}>
          Publisher:
          <input type="text" className="input" placeholder="e.g. DC Comics" value={publisherFilter} onChange={(e) => setPublisherFilter(e.target.value)} style={{ width: '140px', padding: '6px 8px' }} />
        </label>
        <label style={{ display: 'flex', alignItems: 'center', gap: '6px', color: 'var(--text-muted)' }}>
          Year:
          <input type="number" className="input" placeholder="From" min={1900} max={2100} value={yearStartFilter === '' ? '' : yearStartFilter} onChange={(e) => setYearStartFilter(e.target.value === '' ? '' : parseInt(e.target.value, 10) || '')} style={{ width: '72px', padding: '6px 8px' }} />
          <span style={{ color: 'var(--text-muted)' }}>–</span>
          <input type="number" className="input" placeholder="To" min={1900} max={2100} value={yearEndFilter === '' ? '' : yearEndFilter} onChange={(e) => setYearEndFilter(e.target.value === '' ? '' : parseInt(e.target.value, 10) || '')} style={{ width: '72px', padding: '6px 8px' }} />
        </label>
      </div>

      {showApiKeyWarning && (
        <div className="alert alert-warning" style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <AlertCircle size={20} />
          <div>
            <strong>ComicVine API Key Required</strong>
            <p style={{ margin: '4px 0 0', opacity: 0.9 }}>Please configure your ComicVine API key in Settings &gt; ComicVine to search for series.</p>
          </div>
        </div>
      )}

      {hasResults && (
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid var(--border-color)', fontSize: '13px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <span style={{ color: 'var(--text-muted)' }}>{selectedIds.size > 0 ? `${selectedIds.size} selected of ` : ''}{sortedResults.length} results</span>
            <select value={sortBy} onChange={(e) => setSortBy(e.target.value as SortOption)} className="input" style={{ padding: '4px 8px', fontSize: '12px', width: 'auto' }}>
              <option value="year-desc">Newest First</option>
              <option value="year-asc">Oldest First</option>
              <option value="popularity">Most Issues</option>
              <option value="relevance">Best Match</option>
              <option value="name">Name A-Z</option>
            </select>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <button className="btn btn-sm" onClick={toggleSelectAll} style={{ padding: '4px 8px', fontSize: '12px' }}>{selectedIds.size === sortedResults.length ? 'Deselect All' : 'Select All'}</button>
            <button className={`btn btn-sm btn-icon ${viewMode === 'list' ? 'btn-active' : ''}`} onClick={() => setViewMode('list')} title="List view" style={{ padding: '4px 8px' }}><List size={14} /></button>
            <button className={`btn btn-sm btn-icon ${viewMode === 'grid' ? 'btn-active' : ''}`} onClick={() => setViewMode('grid')} title="Grid view" style={{ padding: '4px 8px' }}><Grid size={14} /></button>
          </div>
        </div>
      )}

      <div style={{ flex: 1, overflow: 'auto', minHeight: 0 }}>
        {showNoResults && (
          <div className="empty-state" style={{ padding: '40px 20px' }}>
            <Search size={48} style={{ opacity: 0.3 }} />
            <div className="empty-state-title">No results found</div>
            <div className="empty-state-text">Try a different search term or check your spelling.</div>
          </div>
        )}
        {hasResults && viewMode === 'list' && (
          <table className="add-series-table">
            <thead>
              <tr>
                <th style={{ width: '40px' }}></th>
                <th>Title</th>
                <th style={{ width: '80px' }}>Year</th>
                <th style={{ width: '150px' }}>Publisher</th>
                <th style={{ width: '80px' }}>Issues</th>
                <th style={{ width: '40px' }}></th>
              </tr>
            </thead>
            <tbody>
              {sortedResults.map((candidate) => (
                <SeriesSearchResultRow key={candidate.comicVineId} candidate={candidate} isSelected={selectedIds.has(candidate.comicVineId)} onToggle={() => toggleSelection(candidate.comicVineId)} />
              ))}
            </tbody>
          </table>
        )}
        {hasResults && viewMode === 'grid' && (
          <div className="series-search-results compact">
            {sortedResults.map((candidate) => (
              <SeriesSearchResult key={candidate.comicVineId} candidate={candidate} isSelected={selectedIds.has(candidate.comicVineId)} onSelect={() => toggleSelection(candidate.comicVineId)} compact={true} />
            ))}
          </div>
        )}
        {debouncedQuery.length < 2 && !showApiKeyWarning && (
          <div className="empty-state" style={{ padding: '60px 20px' }}>
            <BookOpen size={48} style={{ opacity: 0.3 }} />
            <div className="empty-state-title">Search for a series</div>
            <div className="empty-state-text">Enter a series name above to search ComicVine.</div>
          </div>
        )}
      </div>

      {addError && (
        <div className="alert alert-danger" style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <AlertCircle size={20} />
          <span>{addError}</span>
        </div>
      )}

      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', paddingTop: '8px', borderTop: '1px solid var(--border-color)' }}>
        <button className="btn" onClick={onClose} disabled={isAdding}>Back</button>
        <button className="btn btn-primary" onClick={handleAddSelected} disabled={selectedIds.size === 0 || isAdding}>
          {isRefreshing ? <><Loader2 size={16} className="spin" /> Refreshing list...</> : addingProgress ? <><Loader2 size={16} className="spin" /> Adding {addingProgress.current} of {addingProgress.total}...</> : selectedIds.size > 0 ? <><Plus size={16} /> Add {selectedIds.size} Series</> : <><Plus size={16} /> Select Series to Add</>}
        </button>
      </div>
    </div>
  );
}
