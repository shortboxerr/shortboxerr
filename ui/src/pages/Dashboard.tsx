import { useQuery } from '@tanstack/react-query';
import { Download, AlertCircle, CheckCircle, Clock } from 'lucide-react';
import { api } from '../api/client';

export function Dashboard() {
  const { data: stats, isLoading } = useQuery({
    queryKey: ['dashboard-stats'],
    queryFn: api.getSystemStatus,
  });

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">Dashboard</h1>
      </header>
      
      <div className="page-content">
        <div className="card-grid">
          <div className="card">
            <div className="card-title">Series</div>
            <div className="stat-value">{isLoading ? '-' : stats?.seriesCount ?? 0}</div>
            <div className="stat-label">Tracked series</div>
          </div>
          
          <div className="card">
            <div className="card-title">Collections</div>
            <div className="stat-value">{isLoading ? '-' : stats?.collectionsCount ?? 0}</div>
            <div className="stat-label">TPBs & Hardcovers</div>
          </div>
          
          <div className="card">
            <div className="card-title">Issues</div>
            <div className="stat-value">{isLoading ? '-' : stats?.issuesCount ?? 0}</div>
            <div className="stat-label">Single issues</div>
          </div>
          
          <div className="card">
            <div className="card-title">Files</div>
            <div className="stat-value">{isLoading ? '-' : stats?.filesCount ?? 0}</div>
            <div className="stat-label">In library</div>
          </div>
        </div>
        
        <div style={{ marginTop: '24px' }}>
          <h2 style={{ fontSize: '16px', fontWeight: 600, marginBottom: '16px', color: 'var(--text-primary)' }}>
            System Status
          </h2>
          <div className="card-grid">
            <StatusCard
              icon={CheckCircle}
              label="Database"
              status="healthy"
              message={stats?.databaseStatus ?? 'Connected'}
            />
            <StatusCard
              icon={Download}
              label="Indexers"
              status={stats?.indexerStatus === 'healthy' ? 'healthy' : 'warning'}
              message={`${stats?.enabledIndexers ?? 0} enabled`}
            />
            <StatusCard
              icon={Clock}
              label="Queue"
              status="healthy"
              message={`${stats?.queuedDownloads ?? 0} pending`}
            />
          </div>
        </div>
        
        <div style={{ marginTop: '24px' }}>
          <h2 style={{ fontSize: '16px', fontWeight: 600, marginBottom: '16px', color: 'var(--text-primary)' }}>
            Recent Activity
          </h2>
          <RecentActivityList />
        </div>
      </div>
    </>
  );
}

function StatusCard({ 
  icon: Icon, 
  label, 
  status, 
  message 
}: { 
  icon: React.ElementType; 
  label: string; 
  status: 'healthy' | 'warning' | 'error';
  message: string;
}) {
  const statusColors = {
    healthy: 'var(--accent-success)',
    warning: 'var(--accent-warning)',
    error: 'var(--accent-danger)',
  };

  return (
    <div className="card" style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
      <div
        style={{
          width: '48px',
          height: '48px',
          borderRadius: 'var(--radius-md)',
          background: `${statusColors[status]}15`,
          color: statusColors[status],
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Icon size={24} />
      </div>
      <div>
        <div style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{label}</div>
        <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>{message}</div>
      </div>
    </div>
  );
}

function RecentActivityList() {
  const { data: activities, isLoading } = useQuery({
    queryKey: ['recent-activity'],
    queryFn: () => api.getRecentActivity(5),
  });

  if (isLoading) {
    return <div className="loading"><div className="spinner" /></div>;
  }

  if (!activities?.length) {
    return (
      <div className="empty-state">
        <Clock size={48} />
        <div className="empty-state-title">No recent activity</div>
        <div className="empty-state-text">Activity will appear here as you add series and import comics.</div>
      </div>
    );
  }

  return (
    <div className="activity-list">
      {activities.map((activity) => (
        <div key={activity.id} className="activity-item">
          <div className={`activity-icon ${activity.type}`}>
            {activity.type === 'success' && <CheckCircle size={20} />}
            {activity.type === 'warning' && <AlertCircle size={20} />}
            {activity.type === 'info' && <Download size={20} />}
          </div>
          <div className="activity-content">
            <div className="activity-title">{activity.title}</div>
            <div className="activity-meta">{activity.timestamp}</div>
          </div>
        </div>
      ))}
    </div>
  );
}

