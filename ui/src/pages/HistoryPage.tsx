import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { 
  History, RefreshCw, Download, FolderInput, Trash2, 
  XCircle, Clock 
} from 'lucide-react';
import { api } from '../api/client';

// HistoryEvent interface is used implicitly through the API response

export function HistoryPage() {
  const [filter, setFilter] = useState<string>('all');
  const [search, setSearch] = useState('');

  const { data: history, isLoading, refetch } = useQuery({
    queryKey: ['history', filter, search],
    queryFn: () => api.getHistory({ type: filter, search }),
  });

  const events = history?.items ?? [];

  const typeIcons = {
    grabbed: Download,
    imported: FolderInput,
    deleted: Trash2,
    failed: XCircle,
    renamed: History,
  };

  const typeColors = {
    grabbed: 'info',
    imported: 'success',
    deleted: 'warning',
    failed: 'danger',
    renamed: 'muted',
  };

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">History</h1>
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
            <option value="grabbed">Grabbed</option>
            <option value="imported">Imported</option>
            <option value="deleted">Deleted</option>
            <option value="failed">Failed</option>
            <option value="renamed">Renamed</option>
          </select>
          
          <input
            type="text"
            className="input search-input"
            placeholder="Search history..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          
          <div className="toolbar-spacer" />
          
          <button className="btn btn-icon" onClick={() => refetch()} title="Refresh">
            <RefreshCw size={18} />
          </button>
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
              
              return (
                <div key={event.id} className="activity-item">
                  <div className={`activity-icon ${color}`}>
                    <Icon size={20} />
                  </div>
                  <div className="activity-content">
                    <div className="activity-title">{event.title}</div>
                    <div className="activity-meta">
                      {event.series} • {event.details}
                      {event.source && ` • ${event.source}`}
                    </div>
                  </div>
                  <div style={{ color: 'var(--text-muted)', fontSize: '13px', whiteSpace: 'nowrap' }}>
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

