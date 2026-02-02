import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { 
  Download, AlertCircle, Clock, XCircle, RefreshCw, Pause, Play, Trash2,
  Search, CheckCircle, Filter, Plug
} from 'lucide-react';
import { api } from '../api/client';

// Queue item interface for downloads
interface QueueItem {
  id: string;
  title: string;
  series: string;
  status: 'downloading' | 'queued' | 'paused' | 'failed';
  progress: number;
  size: string;
  timeRemaining: string | null;
  provider: string;
}

// DDL activity event
interface DdlActivityEvent {
  id: string;
  type: 'search' | 'download_started' | 'download_complete' | 'download_failed' | 'candidate_found';
  title: string;
  provider: string;
  timestamp: string;
  details: string | null;
  status: 'success' | 'warning' | 'error' | 'info';
}

type ActivityTab = 'queue' | 'ddl';
type DdlFilterType = 'all' | 'search' | 'download' | 'failed';

export function ActivityPage() {
  const [activeTab, setActiveTab] = useState<ActivityTab>('queue');
  const [ddlFilter, setDdlFilter] = useState<DdlFilterType>('all');

  const { data: queue, isLoading: queueLoading, refetch: refetchQueue } = useQuery({
    queryKey: ['activity-queue'],
    queryFn: api.getActivityQueue,
    refetchInterval: 5000,
  });

  const { data: ddlActivity, isLoading: ddlLoading, refetch: refetchDdl } = useQuery({
    queryKey: ['ddl-activity', ddlFilter],
    queryFn: () => getDdlActivity(ddlFilter),
    refetchInterval: 10000,
  });

  const handleRefresh = () => {
    if (activeTab === 'queue') {
      refetchQueue();
    } else {
      refetchDdl();
    }
  };

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">Activity</h1>
        <div className="toolbar-group">
          <button className="btn btn-icon" onClick={handleRefresh} title="Refresh">
            <RefreshCw size={18} />
          </button>
        </div>
      </header>
      
      <div className="page-content">
        <div className="toolbar">
          <div className="toolbar-group" style={{ borderRadius: 'var(--radius-md)', overflow: 'hidden' }}>
            <button 
              className={`btn ${activeTab === 'queue' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setActiveTab('queue')}
              style={{ borderRadius: 0, borderRight: 'none' }}
            >
              <Download size={16} />
              Queue ({queue?.length ?? 0})
            </button>
            <button 
              className={`btn ${activeTab === 'ddl' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setActiveTab('ddl')}
              style={{ borderRadius: 0 }}
            >
              <Plug size={16} />
              DDL Activity
            </button>
          </div>

          {activeTab === 'ddl' && (
            <>
              <div className="toolbar-spacer" />
              <select 
                className="input" 
                value={ddlFilter} 
                onChange={(e) => setDdlFilter(e.target.value as DdlFilterType)}
                style={{ minWidth: '150px' }}
              >
                <option value="all">All Events</option>
                <option value="search">Searches</option>
                <option value="download">Downloads</option>
                <option value="failed">Failed</option>
              </select>
            </>
          )}
        </div>
        
        {activeTab === 'queue' && (
          <QueueView queue={queue ?? []} isLoading={queueLoading} />
        )}

        {activeTab === 'ddl' && (
          <DdlActivityView events={ddlActivity ?? []} isLoading={ddlLoading} />
        )}
      </div>
    </>
  );
}

// Queue view component
function QueueView({ queue, isLoading }: { queue: QueueItem[]; isLoading: boolean }) {
  if (isLoading) {
    return <div className="loading"><div className="spinner" /></div>;
  }

  if (!queue.length) {
    return (
      <div className="empty-state">
        <Download size={48} />
        <div className="empty-state-title">Queue is empty</div>
        <div className="empty-state-text">
          Downloads will appear here when comics are grabbed from indexers.
        </div>
      </div>
    );
  }

  return (
    <div className="activity-list">
      {queue.map((item) => (
        <QueueItemCard key={item.id} item={item} />
      ))}
    </div>
  );
}

function QueueItemCard({ item }: { item: QueueItem }) {
  const statusIcons = {
    downloading: Download,
    queued: Clock,
    paused: Pause,
    failed: XCircle,
  };
  
  const statusColors = {
    downloading: 'info',
    queued: 'muted',
    paused: 'warning',
    failed: 'danger',
  };
  
  const Icon = statusIcons[item.status];
  const colorClass = statusColors[item.status];

  return (
    <div className="card" style={{ padding: '16px' }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: '16px' }}>
        <div className={`activity-icon ${colorClass}`}>
          <Icon size={20} />
        </div>
        
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '8px' }}>
            <div>
              <div style={{ fontWeight: 500, color: 'var(--text-primary)' }}>{item.title}</div>
              <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                {item.series} • {item.provider}
              </div>
            </div>
            <div style={{ display: 'flex', gap: '4px' }}>
              {item.status === 'downloading' && (
                <button className="btn btn-icon" title="Pause">
                  <Pause size={16} />
                </button>
              )}
              {item.status === 'paused' && (
                <button className="btn btn-icon" title="Resume">
                  <Play size={16} />
                </button>
              )}
              <button className="btn btn-icon" title="Remove">
                <Trash2 size={16} />
              </button>
            </div>
          </div>
          
          {item.status === 'downloading' && (
            <div>
              <div style={{ 
                height: '4px', 
                background: 'var(--bg-tertiary)', 
                borderRadius: '2px',
                overflow: 'hidden',
                marginBottom: '8px'
              }}>
                <div style={{ 
                  width: `${item.progress}%`, 
                  height: '100%', 
                  background: 'var(--accent-primary)',
                  transition: 'width 0.3s ease'
                }} />
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', color: 'var(--text-muted)' }}>
                <span>{item.progress}% • {item.size}</span>
                <span>{item.timeRemaining ?? 'Calculating...'}</span>
              </div>
            </div>
          )}
          
          {item.status === 'failed' && (
            <div style={{ 
              display: 'flex', 
              alignItems: 'center', 
              gap: '8px',
              padding: '8px 12px',
              background: 'rgba(217, 83, 79, 0.1)',
              borderRadius: 'var(--radius-sm)',
              marginTop: '8px'
            }}>
              <AlertCircle size={14} style={{ color: 'var(--accent-danger)' }} />
              <span style={{ fontSize: '13px', color: 'var(--accent-danger)' }}>
                Download failed - click to retry
              </span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// DDL Activity view component
function DdlActivityView({ events, isLoading }: { events: DdlActivityEvent[]; isLoading: boolean }) {
  if (isLoading) {
    return <div className="loading"><div className="spinner" /></div>;
  }

  if (!events.length) {
    return (
      <div className="empty-state">
        <Plug size={48} />
        <div className="empty-state-title">No DDL activity</div>
        <div className="empty-state-text">
          DDL searches and downloads will appear here when indexers are active.
        </div>
      </div>
    );
  }

  return (
    <div className="activity-list">
      {events.map((event) => (
        <DdlActivityCard key={event.id} event={event} />
      ))}
    </div>
  );
}

function DdlActivityCard({ event }: { event: DdlActivityEvent }) {
  const typeIcons = {
    search: Search,
    download_started: Download,
    download_complete: CheckCircle,
    download_failed: XCircle,
    candidate_found: Filter,
  };
  
  const statusColors = {
    success: 'success',
    warning: 'warning',
    error: 'danger',
    info: 'info',
  };

  const typeLabels = {
    search: 'Search',
    download_started: 'Download Started',
    download_complete: 'Download Complete',
    download_failed: 'Download Failed',
    candidate_found: 'Candidate Found',
  };
  
  const Icon = typeIcons[event.type];
  const colorClass = statusColors[event.status];

  return (
    <div className="activity-item">
      <div className={`activity-icon ${colorClass}`}>
        <Icon size={20} />
      </div>
      <div className="activity-content">
        <div className="activity-title">
          {event.title}
          <span className={`badge badge-${colorClass}`} style={{ marginLeft: '8px' }}>
            {typeLabels[event.type]}
          </span>
        </div>
        <div className="activity-meta">
          {event.provider}
          {event.details && ` • ${event.details}`}
        </div>
      </div>
      <div style={{ color: 'var(--text-muted)', fontSize: '13px', whiteSpace: 'nowrap' }}>
        {event.timestamp}
      </div>
    </div>
  );
}

// Mock function to get DDL activity (would connect to real API)
async function getDdlActivity(_filter: DdlFilterType): Promise<DdlActivityEvent[]> {
  // In a real implementation, this would call an API endpoint like:
  // return await api.getDdlActivity({ filter: _filter });
  
  // For now, return empty array since there's no DDL activity endpoint yet
  return [];
}
