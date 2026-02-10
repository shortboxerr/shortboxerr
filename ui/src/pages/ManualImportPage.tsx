import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  FolderInput, RefreshCw, Check, X, Edit, ChevronRight, 
  FileArchive, AlertCircle, CheckCircle, Search, Loader2
} from 'lucide-react';
import { api } from '../api/client';
import type { Series } from '../api/client';

// Interface for staged files
interface StagedFile {
  id: string;
  filename: string;
  path: string;
  size: string;
  parsed: {
    series: string | null;
    issue: number | null;
    year: number | null;
    format: string | null;
    isCollection: boolean;
    editionType: string | null;
    confidence: number;
  };
  match: {
    seriesId: number | null;
    seriesTitle: string | null;
    issueId: number | null;
    editionId: number | null;
    confidence: number;
  } | null;
  status: 'pending' | 'matched' | 'unmatched' | 'error';
}

export function ManualImportPage() {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [editingFile, setEditingFile] = useState<StagedFile | null>(null);
  const [rejectingFile, setRejectingFile] = useState<StagedFile | null>(null);
  const [rejectReason, setRejectReason] = useState('');
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

  const rejectMutation = useMutation({
    mutationFn: ({ path, reason }: { path: string; reason?: string }) => 
      api.rejectStagedFile(path, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['staged-files'] });
      setRejectingFile(null);
      setRejectReason('');
    },
  });

  const updateMatchMutation = useMutation({
    mutationFn: ({ path, seriesId }: { path: string; seriesId: number | null }) => 
      api.updateStagedMatch(path, seriesId, null, null),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['staged-files'] });
      setEditingFile(null);
    },
  });

  const files = (staged?.items ?? []) as StagedFile[];
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

  const handleReject = (file: StagedFile) => {
    setRejectingFile(file);
    setRejectReason('');
  };

  const confirmReject = () => {
    if (rejectingFile) {
      rejectMutation.mutate({ path: rejectingFile.path, reason: rejectReason || undefined });
    }
  };

  const handleEditMatch = (file: StagedFile) => {
    setEditingFile(file);
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
                          {file.match.confidence > 0 && (
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
                      <button 
                        className="btn btn-icon" 
                        title="Edit match"
                        onClick={() => handleEditMatch(file)}
                      >
                        <Edit size={16} />
                      </button>
                      <button 
                        className="btn btn-icon" 
                        title="Reject"
                        onClick={() => handleReject(file)}
                      >
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

      {/* Reject Confirmation Modal */}
      {rejectingFile && (
        <div className="modal-overlay" onClick={() => setRejectingFile(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '400px' }}>
            <div className="modal-header">
              <h2 className="modal-title">Reject File</h2>
              <button className="btn btn-icon" onClick={() => setRejectingFile(null)}>
                <X size={18} />
              </button>
            </div>
            <div className="modal-body">
              <p style={{ marginBottom: '16px' }}>
                Are you sure you want to reject <strong>{rejectingFile.filename}</strong>?
              </p>
              <p style={{ marginBottom: '16px', fontSize: '13px', color: 'var(--text-secondary)' }}>
                The file will be moved to the failed folder and won't appear in the import list.
              </p>
              <label className="form-label">Reason (optional)</label>
              <input
                type="text"
                className="form-input"
                value={rejectReason}
                onChange={e => setRejectReason(e.target.value)}
                placeholder="e.g., Duplicate, wrong format, etc."
              />
            </div>
            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setRejectingFile(null)}>
                Cancel
              </button>
              <button 
                className="btn btn-danger" 
                onClick={confirmReject}
                disabled={rejectMutation.isPending}
              >
                {rejectMutation.isPending ? <Loader2 size={16} className="spin" /> : <X size={16} />}
                Reject
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Edit Match Modal */}
      {editingFile && (
        <EditMatchModal
          file={editingFile}
          onClose={() => setEditingFile(null)}
          onSelect={(seriesId) => {
            updateMatchMutation.mutate({ path: editingFile.path, seriesId });
          }}
          isPending={updateMatchMutation.isPending}
        />
      )}
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

// Edit Match Modal Component
function EditMatchModal({ 
  file, 
  onClose, 
  onSelect,
  isPending 
}: { 
  file: StagedFile; 
  onClose: () => void; 
  onSelect: (seriesId: number | null) => void;
  isPending: boolean;
}) {
  const [searchTerm, setSearchTerm] = useState(file.parsed.series ?? '');
  const [selectedSeries, setSelectedSeries] = useState<Series | null>(null);

  const { data: searchResults, isLoading: isSearching } = useQuery({
    queryKey: ['series-search', searchTerm],
    queryFn: () => api.getSeries({ search: searchTerm, pageSize: 10 }),
    enabled: searchTerm.length >= 2,
    staleTime: 30000,
  });

  const series = searchResults?.items ?? [];

  useEffect(() => {
    // Pre-select if there's a current match
    if (file.match?.seriesId) {
      const match = series.find(s => s.id === file.match?.seriesId);
      if (match) {
        setSelectedSeries(match);
      }
    }
  }, [file.match?.seriesId, series]);

  const handleConfirm = () => {
    onSelect(selectedSeries?.id ?? null);
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '500px' }}>
        <div className="modal-header">
          <h2 className="modal-title">Edit Match</h2>
          <button className="btn btn-icon" onClick={onClose}>
            <X size={18} />
          </button>
        </div>
        <div className="modal-body">
          <div style={{ marginBottom: '16px' }}>
            <div style={{ fontWeight: 500, marginBottom: '4px' }}>{file.filename}</div>
            <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
              Parsed: {file.parsed.series ?? 'Unknown'}
              {file.parsed.issue && ` #${file.parsed.issue}`}
            </div>
          </div>

          <label className="form-label">Search Series</label>
          <div style={{ position: 'relative', marginBottom: '16px' }}>
            <Search 
              size={16} 
              style={{ 
                position: 'absolute', 
                left: '12px', 
                top: '50%', 
                transform: 'translateY(-50%)',
                color: 'var(--text-muted)' 
              }} 
            />
            <input
              type="text"
              className="form-input"
              value={searchTerm}
              onChange={e => setSearchTerm(e.target.value)}
              placeholder="Type to search..."
              style={{ paddingLeft: '36px' }}
              autoFocus
            />
          </div>

          <div style={{ 
            maxHeight: '250px', 
            overflowY: 'auto',
            border: '1px solid var(--border-color)',
            borderRadius: 'var(--radius-sm)',
          }}>
            {isSearching ? (
              <div style={{ padding: '24px', textAlign: 'center' }}>
                <Loader2 size={24} className="spin" style={{ color: 'var(--text-muted)' }} />
              </div>
            ) : searchTerm.length < 2 ? (
              <div style={{ padding: '24px', textAlign: 'center', color: 'var(--text-muted)' }}>
                Enter at least 2 characters to search
              </div>
            ) : series.length === 0 ? (
              <div style={{ padding: '24px', textAlign: 'center', color: 'var(--text-muted)' }}>
                No series found matching "{searchTerm}"
              </div>
            ) : (
              series.map(s => (
                <div
                  key={s.id}
                  onClick={() => setSelectedSeries(s)}
                  style={{
                    padding: '12px 16px',
                    cursor: 'pointer',
                    borderBottom: '1px solid var(--border-color)',
                    background: selectedSeries?.id === s.id ? 'var(--bg-selected)' : 'transparent',
                  }}
                >
                  <div style={{ fontWeight: 500, marginBottom: '2px' }}>{s.title}</div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                    {s.publisher}{s.year && ` • ${s.year}`}
                    {s.issueCount && ` • ${s.issueCount} issues`}
                  </div>
                </div>
              ))
            )}
          </div>

          {selectedSeries && (
            <div style={{ 
              marginTop: '16px', 
              padding: '12px', 
              background: 'var(--bg-secondary)',
              borderRadius: 'var(--radius-sm)',
            }}>
              <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '4px' }}>
                Selected:
              </div>
              <div style={{ fontWeight: 500 }}>{selectedSeries.title}</div>
            </div>
          )}
        </div>
        <div className="modal-footer">
          <button className="btn btn-secondary" onClick={onClose}>
            Cancel
          </button>
          <button 
            className="btn btn-primary" 
            onClick={handleConfirm}
            disabled={isPending}
          >
            {isPending ? <Loader2 size={16} className="spin" /> : <Check size={16} />}
            Confirm Match
          </button>
        </div>
      </div>
    </div>
  );
}