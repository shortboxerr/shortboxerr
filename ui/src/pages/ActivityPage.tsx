import { memo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { 
  Download, AlertCircle, Clock, XCircle, RefreshCw, Pause, Play, Trash2
} from 'lucide-react';
import { api } from '../api/client';

interface QueueItem {
  id: string;
  title: string;
  series: string;
  status: 'downloading' | 'queued' | 'paused' | 'failed' | 'completed';
  progress: number;
  size: string;
  timeRemaining: string | null;
  provider: string;
}

export function ActivityPage() {
  const { data: queue, isLoading, refetch } = useQuery({
    queryKey: ['activity-queue'],
    queryFn: api.getActivityQueue,
    refetchInterval: 3000,
    refetchIntervalInBackground: false, // Pause polling when tab not visible
  });

  // Filter to only show active downloads (not completed)
  const activeQueue = (queue ?? []).filter(item => item.status !== 'completed');

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">Activity</h1>
        <div className="toolbar-group">
          <button className="btn btn-icon" onClick={() => refetch()} title="Refresh">
            <RefreshCw size={18} />
          </button>
        </div>
      </header>
      
      <div className="page-content">
        <div className="toolbar">
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Download size={18} style={{ color: 'var(--text-muted)' }} />
            <span style={{ color: 'var(--text-secondary)' }}>
              {activeQueue.length === 0 
                ? 'No active downloads' 
                : `${activeQueue.length} active download${activeQueue.length !== 1 ? 's' : ''}`}
            </span>
          </div>
        </div>
        
        <QueueView queue={activeQueue} isLoading={isLoading} />
      </div>
    </>
  );
}

function QueueView({ queue, isLoading }: { queue: QueueItem[]; isLoading: boolean }) {
  if (isLoading) {
    return <div className="loading"><div className="spinner" /></div>;
  }

  if (!queue.length) {
    return (
      <div className="empty-state">
        <Download size={48} />
        <div className="empty-state-title">No active downloads</div>
        <div className="empty-state-text">
          Downloads will appear here when comics are grabbed from indexers or DDL sites.
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

// Memoized to prevent re-renders when queue refreshes
const STATUS_ICONS = {
  downloading: Download,
  queued: Clock,
  paused: Pause,
  failed: XCircle,
  completed: Download,
} as const;

const STATUS_COLORS = {
  downloading: 'info',
  queued: 'muted',
  paused: 'warning',
  failed: 'danger',
  completed: 'success',
} as const;

const QueueItemCard = memo(function QueueItemCard({ item }: { item: QueueItem }) {
  const Icon = STATUS_ICONS[item.status];
  const colorClass = STATUS_COLORS[item.status];

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
              <button className="btn btn-icon" title="Cancel">
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

          {item.status === 'queued' && (
            <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
              Waiting to start • {item.size}
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
});
