import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  History, RefreshCw, Download, FolderInput, Trash2, 
  XCircle, Clock, CheckCircle, Search, PlusCircle
} from 'lucide-react';
import { api } from '../api/client';

export function HistoryPage() {
  const [filter, setFilter] = useState<string>('all');
  const [search, setSearch] = useState('');
  const queryClient = useQueryClient();

  const { data: history, isLoading, refetch } = useQuery({
    queryKey: ['history', filter, search],
    queryFn: () => api.getHistory({ type: filter, search }),
  });

  const clearHistoryMutation = useMutation({
    mutationFn: api.clearHistory,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['history'] });
    },
  });

  const handleClearHistory = () => {
    if (confirm('Are you sure you want to clear all history? This cannot be undone.')) {
      clearHistoryMutation.mutate();
    }
  };

  const events = history?.items ?? [];

  const typeIcons: Record<string, typeof Download> = {
    grabbed: Download,
    downloaded: CheckCircle,
    imported: FolderInput,
    deleted: Trash2,
    failed: XCircle,
    renamed: History,
    added: PlusCircle,
  };

  const typeColors: Record<string, string> = {
    grabbed: 'info',
    downloaded: 'success',
    imported: 'success',
    deleted: 'warning',
    failed: 'danger',
    renamed: 'muted',
    added: 'success',
  };

  const typeLabels: Record<string, string> = {
    grabbed: 'Grabbed',
    downloaded: 'Downloaded',
    imported: 'Imported',
    deleted: 'Deleted',
    failed: 'Failed',
    renamed: 'Renamed',
    added: 'Added',
  };

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">History</h1>
        <div className="toolbar-group">
          {events.length > 0 && (
            <button 
              className="btn btn-secondary"
              onClick={handleClearHistory}
              disabled={clearHistoryMutation.isPending}
              title="Clear all history"
            >
              <Trash2 size={16} />
              {clearHistoryMutation.isPending ? 'Clearing...' : 'Clear'}
            </button>
          )}
          <button className="btn btn-icon" onClick={() => refetch()} title="Refresh">
            <RefreshCw size={18} />
          </button>
        </div>
      </header>
      
      <div className="page-content">
        <div className="toolbar">
          <select 
            className="input" 
            value={filter} 
            onChange={(e) => setFilter(e.target.value)}
            style={{ minWidth: '150px' }}
          >
            <option value="all">All Events</option>
            <option value="added">Added</option>
            <option value="grabbed">Grabbed</option>
            <option value="downloaded">Downloaded</option>
            <option value="imported">Imported</option>
            <option value="deleted">Deleted</option>
            <option value="failed">Failed</option>
            <option value="renamed">Renamed</option>
          </select>
          
          <div style={{ position: 'relative', flex: 1, maxWidth: '300px' }}>
            <Search size={16} style={{ 
              position: 'absolute', 
              left: '12px', 
              top: '50%', 
              transform: 'translateY(-50%)',
              color: 'var(--text-muted)'
            }} />
            <input
              type="text"
              className="input"
              placeholder="Search history..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{ paddingLeft: '36px', width: '100%' }}
            />
          </div>
          
          <div className="toolbar-spacer" />
          
          <span style={{ color: 'var(--text-muted)', fontSize: '13px' }}>
            {events.length} event{events.length !== 1 ? 's' : ''}
          </span>
        </div>
        
        {isLoading ? (
          <div className="loading"><div className="spinner" /></div>
        ) : events.length === 0 ? (
          <div className="empty-state">
            <History size={48} />
            <div className="empty-state-title">No history</div>
            <div className="empty-state-text">
              Events will appear here as you download and manage your library.
            </div>
          </div>
        ) : (
          <div className="activity-list">
            {events.map((event) => {
              const Icon = typeIcons[event.type] || Clock;
              const color = typeColors[event.type] || 'muted';
              const label = typeLabels[event.type] || event.type;
              
              return (
                <div key={event.id} className="activity-item">
                  <div className={`activity-icon ${color}`}>
                    <Icon size={20} />
                  </div>
                  <div className="activity-content">
                    <div className="activity-title">
                      {event.title}
                      <span className={`badge badge-${color}`} style={{ marginLeft: '8px' }}>
                        {label}
                      </span>
                    </div>
                    <div className="activity-meta">
                      {event.series}
                      {event.details && ` • ${event.details}`}
                      {event.source && ` • ${event.source}`}
                    </div>
                  </div>
                  <div 
                    style={{ color: 'var(--text-muted)', fontSize: '13px', whiteSpace: 'nowrap' }}
                    title={event.fullTimestamp}
                  >
                    {event.timestamp}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </>
  );
}
