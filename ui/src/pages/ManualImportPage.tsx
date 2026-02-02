import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  FolderInput, RefreshCw, Check, X, Edit, ChevronRight, 
  FileArchive, AlertCircle, CheckCircle 
} from 'lucide-react';
import { api } from '../api/client';

// StagedFile interface is used implicitly through the API response

export function ManualImportPage() {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const queryClient = useQueryClient();

  const { data: staged, isLoading, refetch } = useQuery({
    queryKey: ['staged-files'],
    queryFn: api.getStagedFiles,
  });

  const importMutation = useMutation({
    mutationFn: (ids: string[]) => api.importFiles(ids),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['staged-files'] });
      setSelectedIds(new Set());
    },
  });

  const files = staged?.items ?? [];
  const allSelected = files.length > 0 && selectedIds.size === files.length;
  const matchedCount = files.filter(f => f.status === 'matched').length;
  const unmatchedCount = files.filter(f => f.status === 'unmatched').length;

  const toggleSelectAll = () => {
    if (allSelected) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(files.map(f => f.id)));
    }
  };

  const toggleSelect = (id: string) => {
    const newSelected = new Set(selectedIds);
    if (newSelected.has(id)) {
      newSelected.delete(id);
    } else {
      newSelected.add(id);
    }
    setSelectedIds(newSelected);
  };

  const handleImportSelected = () => {
    const selectedMatched = files
      .filter(f => selectedIds.has(f.id) && f.status === 'matched')
      .map(f => f.id);
    
    if (selectedMatched.length === 0) {
      alert('No matched files selected. Please select files with valid matches.');
      return;
    }
    
    importMutation.mutate(selectedMatched);
  };

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">Manual Import</h1>
        <div className="toolbar-group">
          <button 
            className="btn btn-primary"
            onClick={handleImportSelected}
            disabled={selectedIds.size === 0 || importMutation.isPending}
          >
            <Check size={16} />
            Import Selected
          </button>
        </div>
      </header>
      
      <div className="page-content">
        <div className="card-grid" style={{ marginBottom: '24px' }}>
          <div className="card" style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <div style={{
              width: '48px',
              height: '48px',
              borderRadius: 'var(--radius-md)',
              background: 'rgba(93, 156, 236, 0.15)',
              color: 'var(--accent-primary)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}>
              <FileArchive size={24} />
            </div>
            <div>
              <div className="stat-value">{files.length}</div>
              <div className="stat-label">Files in staging</div>
            </div>
          </div>
          
          <div className="card" style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <div style={{
              width: '48px',
              height: '48px',
              borderRadius: 'var(--radius-md)',
              background: 'rgba(92, 184, 92, 0.15)',
              color: 'var(--accent-success)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}>
              <CheckCircle size={24} />
            </div>
            <div>
              <div className="stat-value">{matchedCount}</div>
              <div className="stat-label">Auto-matched</div>
            </div>
          </div>
          
          <div className="card" style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <div style={{
              width: '48px',
              height: '48px',
              borderRadius: 'var(--radius-md)',
              background: 'rgba(240, 173, 78, 0.15)',
              color: 'var(--accent-warning)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}>
              <AlertCircle size={24} />
            </div>
            <div>
              <div className="stat-value">{unmatchedCount}</div>
              <div className="stat-label">Need review</div>
            </div>
          </div>
        </div>
        
        <div className="toolbar">
          <div className="toolbar-spacer" />
          <button className="btn btn-icon" onClick={() => refetch()} title="Refresh">
            <RefreshCw size={18} />
          </button>
        </div>
        
        <div className="table-container">
          {isLoading ? (
            <div className="loading"><div className="spinner" /></div>
          ) : files.length === 0 ? (
            <div className="empty-state">
              <FolderInput size={48} />
              <div className="empty-state-title">Staging folder is empty</div>
              <div className="empty-state-text">
                Drop comic files into the staging folder to import them.
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
                  <th>File</th>
                  <th>Parsed Info</th>
                  <th style={{ width: '40px' }}></th>
                  <th>Match</th>
                  <th>Status</th>
                  <th className="table-actions"></th>
                </tr>
              </thead>
              <tbody>
                {files.map((file) => (
                  <tr key={file.id}>
                    <td className="table-checkbox">
                      <input
                        type="checkbox"
                        checked={selectedIds.has(file.id)}
                        onChange={() => toggleSelect(file.id)}
                      />
                    </td>
                    <td>
                      <div style={{ color: 'var(--text-primary)', fontWeight: 500 }}>
                        {file.filename}
                      </div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                        {file.size}
                      </div>
                    </td>
                    <td>
                      <div style={{ fontSize: '13px' }}>
                        {file.parsed.series ?? 'Unknown'}
                        {file.parsed.issue && ` #${file.parsed.issue}`}
                        {file.parsed.year && ` (${file.parsed.year})`}
                      </div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                        {file.parsed.format?.toUpperCase()} • {file.parsed.confidence}% confidence
                      </div>
                    </td>
                    <td style={{ textAlign: 'center' }}>
                      <ChevronRight size={16} style={{ color: 'var(--text-muted)' }} />
                    </td>
                    <td>
                      {file.match ? (
                        <div style={{ fontSize: '13px' }}>
                          {file.match.seriesTitle}
                          {file.match.confidence && (
                            <span style={{ marginLeft: '8px', color: 'var(--text-muted)' }}>
                              ({file.match.confidence}%)
                            </span>
                          )}
                        </div>
                      ) : (
                        <span style={{ color: 'var(--text-muted)', fontStyle: 'italic' }}>
                          No match found
                        </span>
                      )}
                    </td>
                    <td>
                      <span className={`badge badge-${getStatusBadge(file.status)}`}>
                        {file.status}
                      </span>
                    </td>
                    <td className="table-actions">
                      <button className="btn btn-icon" title="Edit match">
                        <Edit size={16} />
                      </button>
                      <button className="btn btn-icon" title="Reject">
                        <X size={16} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </>
  );
}

function getStatusBadge(status: string): string {
  switch (status) {
    case 'matched':
      return 'success';
    case 'unmatched':
      return 'warning';
    case 'error':
      return 'danger';
    default:
      return 'muted';
  }
}

