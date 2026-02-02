import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, RefreshCw, Trash2, Edit, MoreVertical, BookOpen } from 'lucide-react';
import { api } from '../api/client';

// Series interface is used implicitly through the API response

export function SeriesPage() {
  const [search, setSearch] = useState('');
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const queryClient = useQueryClient();

  const { data: seriesData, isLoading, refetch } = useQuery({
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

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">Series</h1>
        <div className="toolbar-group">
          <button className="btn btn-primary">
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
          
          <button className="btn btn-icon" onClick={() => refetch()} title="Refresh">
            <RefreshCw size={18} />
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
                  <tr key={item.id}>
                    <td className="table-checkbox">
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

