import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, RefreshCw, Trash2, Edit, MoreVertical, Library } from 'lucide-react';
import { api } from '../api/client';

// Edition interface is used implicitly through the API response

export function CollectionsPage() {
  const [search, setSearch] = useState('');
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const { data: editionsData, isLoading, refetch } = useQuery({
    queryKey: ['editions', search],
    queryFn: () => api.getEditions({ search, page: 1, pageSize: 50 }),
  });

  const deleteMutation = useMutation({
    mutationFn: api.deleteEdition,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['editions'] });
      setSelectedIds(new Set());
    },
  });

  const editions = editionsData?.items ?? [];
  const allSelected = editions.length > 0 && selectedIds.size === editions.length;

  const toggleSelectAll = () => {
    if (allSelected) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(editions.map((e) => e.id)));
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
    if (!confirm(`Delete ${selectedIds.size} collections? This cannot be undone.`)) return;
    
    selectedIds.forEach((id) => deleteMutation.mutate(id));
  };

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">Collections</h1>
        <div className="toolbar-group">
          <button className="btn btn-primary">
            <Plus size={16} />
            Add Collection
          </button>
        </div>
      </header>
      
      <div className="page-content">
        <div className="toolbar">
          <input
            type="text"
            className="input search-input"
            placeholder="Search collections..."
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
          ) : editions.length === 0 ? (
            <div className="empty-state">
              <Library size={48} />
              <div className="empty-state-title">No collections found</div>
              <div className="empty-state-text">
                {search ? 'Try a different search term.' : 'Add a TPB, hardcover, or omnibus to get started.'}
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
                  <th>Series</th>
                  <th>Type</th>
                  <th>Volume</th>
                  <th>Year</th>
                  <th>Status</th>
                  <th className="table-actions"></th>
                </tr>
              </thead>
              <tbody>
                {editions.map((item) => (
                  <tr 
                    key={item.id} 
                    className="table-row-clickable"
                    onClick={() => navigate(`/collections/${item.id}`)}
                  >
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
                    <td>{item.seriesTitle}</td>
                    <td>
                      <span className={`badge badge-${getTypeBadge(item.editionType)}`}>
                        {formatEditionType(item.editionType)}
                      </span>
                    </td>
                    <td>{item.volumeNumber ?? '-'}</td>
                    <td>{formatReleaseYear(item.releaseDate)}</td>
                    <td>
                      <span className={`badge badge-${item.hasFile ? 'success' : 'warning'}`}>
                        {item.hasFile ? 'Have' : 'Missing'}
                      </span>
                    </td>
                    <td className="table-actions" onClick={(e) => e.stopPropagation()}>
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
        
        {editionsData && editionsData.totalCount > editionsData.items.length && (
          <div style={{ marginTop: '16px', textAlign: 'center', color: 'var(--text-muted)' }}>
            Showing {editions.length} of {editionsData.totalCount} collections
          </div>
        )}
      </div>
    </>
  );
}

function getTypeBadge(type: string): string {
  const typeStr = String(type);
  switch (typeStr.toLowerCase()) {
    case '0':
    case 'tradespaperback':
    case 'tpb':
      return 'info';
    case '1':
    case 'hardcover':
    case 'hc':
      return 'success';
    case '2':
    case 'omnibus':
      return 'warning';
    case '3':
    case 'compendium':
      return 'warning';
    case '4':
    case 'absoluteedition':
      return 'danger';
    case '5':
    case 'deluxeedition':
    case 'deluxe':
      return 'info';
    default:
      return 'muted';
  }
}

function formatEditionType(type: string): string {
  const typeStr = String(type);
  const editionTypes: Record<string, string> = {
    '0': 'TPB',
    '1': 'Hardcover',
    '2': 'Omnibus',
    '3': 'Compendium',
    '4': 'Absolute',
    '5': 'Deluxe',
    '99': 'Other',
    'TradesPaperback': 'TPB',
    'Hardcover': 'Hardcover',
    'Omnibus': 'Omnibus',
    'Compendium': 'Compendium',
    'AbsoluteEdition': 'Absolute',
    'DeluxeEdition': 'Deluxe',
    'Other': 'Other'
  };
  return editionTypes[typeStr] ?? typeStr;
}

function formatReleaseYear(dateStr: string | null): string {
  if (!dateStr) return '-';
  const date = new Date(dateStr);
  return isNaN(date.getTime()) ? '-' : String(date.getFullYear());
}

