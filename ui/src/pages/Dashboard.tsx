import { useQuery } from '@tanstack/react-query';
import { Download, AlertCircle, CheckCircle, Clock, Calendar, ChevronRight } from 'lucide-react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { PullListIssue } from '../api/client';

export function Dashboard() {
  const { data: stats, isLoading } = useQuery({
    queryKey: ['dashboard-stats'],
    queryFn: api.getSystemStatus,
  });

  const { data: thisWeek } = useQuery({
    queryKey: ['pulllist', 'week', 'dashboard'],
    queryFn: () => api.getPullListThisWeek(),
    staleTime: 60000, // 1 minute
  });

  const { data: pullStats } = useQuery({
    queryKey: ['pulllist', 'stats', 'dashboard'],
    queryFn: () => api.getPullListStats(),
    staleTime: 60000,
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
            <div className="card-title">Issues</div>
            <div className="stat-value">{isLoading ? '-' : stats?.issuesCount ?? 0}</div>
            <div className="stat-label">Single issues</div>
          </div>
          
          <div className="card">
            <div className="card-title">Files</div>
            <div className="stat-value">{isLoading ? '-' : stats?.filesCount ?? 0}</div>
            <div className="stat-label">In library</div>
          </div>
          
          <Link to="/wanted" className="card card-clickable" style={{ textDecoration: 'none' }}>
            <div className="card-title">Wanted</div>
            <div className="stat-value" style={{ color: 'var(--accent-warning)' }}>
              {pullStats?.totalWantedIssues ?? 0}
            </div>
            <div className="stat-label">Issues to find</div>
          </Link>
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
        
        {/* Pull List Widgets */}
        <div className="dashboard-widgets" style={{ marginTop: '24px', display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(350px, 1fr))', gap: '24px' }}>
          <ThisWeekWidget issues={thisWeek?.issues ?? []} stats={pullStats} />
          <ComingSoonWidget stats={pullStats} />
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

interface PullListStats {
  totalMonitoredSeries: number;
  totalWantedIssues: number;
  totalOwnedIssues: number;
  totalSkippedIssues: number;
  releasingThisWeek: number;
  releasingNextWeek: number;
  missedIssues: number;
  wantedByPublisher: Record<string, number>;
}

function ThisWeekWidget({ issues, stats }: { issues: PullListIssue[]; stats?: PullListStats }) {
  const wantedIssues = issues.filter(i => i.status === 'Wanted').slice(0, 5);
  const totalThisWeek = stats?.releasingThisWeek ?? issues.length;
  const wantedCount = issues.filter(i => i.status === 'Wanted').length;
  
  return (
    <div className="card dashboard-widget">
      <div className="widget-header">
        <div className="widget-title">
          <Calendar size={18} className="widget-icon" />
          This Week
        </div>
        <Link to="/pulllist" className="widget-link">
          View All <ChevronRight size={14} />
        </Link>
      </div>
      
      <div className="widget-stats">
        <div className="widget-stat">
          <span className="widget-stat-value">{totalThisWeek}</span>
          <span className="widget-stat-label">releasing</span>
        </div>
        <div className="widget-stat wanted">
          <span className="widget-stat-value">{wantedCount}</span>
          <span className="widget-stat-label">wanted</span>
        </div>
      </div>
      
      {wantedIssues.length > 0 ? (
        <div className="widget-list">
          {wantedIssues.map((issue) => (
            <Link 
              key={issue.issueId} 
              to={`/series/${issue.seriesId}`}
              className="widget-list-item"
            >
              {issue.coverImageUrl ? (
                <img src={issue.coverImageUrl} alt="" className="widget-list-thumb" />
              ) : (
                <div className="widget-list-thumb-placeholder">
                  <Calendar size={14} />
                </div>
              )}
              <div className="widget-list-info">
                <div className="widget-list-title">{issue.seriesTitle}</div>
                <div className="widget-list-meta">#{issue.issueNumberText || issue.issueNumber}</div>
              </div>
              <span className="badge badge-warning">Wanted</span>
            </Link>
          ))}
        </div>
      ) : (
        <div className="widget-empty">
          <Calendar size={24} />
          <span>No wanted issues this week</span>
        </div>
      )}
    </div>
  );
}

function ComingSoonWidget({ stats }: { stats?: PullListStats }) {
  const topPublishers = stats?.wantedByPublisher 
    ? Object.entries(stats.wantedByPublisher)
        .sort(([, a], [, b]) => b - a)
        .slice(0, 4)
    : [];

  return (
    <div className="card dashboard-widget">
      <div className="widget-header">
        <div className="widget-title">
          <Clock size={18} className="widget-icon" />
          Coming Soon
        </div>
        <Link to="/pulllist?view=upcoming" className="widget-link">
          View All <ChevronRight size={14} />
        </Link>
      </div>
      
      <div className="widget-stats">
        <div className="widget-stat">
          <span className="widget-stat-value">{stats?.releasingNextWeek ?? 0}</span>
          <span className="widget-stat-label">next week</span>
        </div>
        {(stats?.missedIssues ?? 0) > 0 && (
          <div className="widget-stat missed">
            <span className="widget-stat-value">{stats?.missedIssues}</span>
            <span className="widget-stat-label">missed</span>
          </div>
        )}
      </div>
      
      {topPublishers.length > 0 ? (
        <div className="widget-breakdown">
          <div className="widget-breakdown-title">Wanted by Publisher</div>
          {topPublishers.map(([publisher, count]) => (
            <div key={publisher} className="widget-breakdown-row">
              <span className="widget-breakdown-label">{publisher}</span>
              <span className="widget-breakdown-value">{count}</span>
            </div>
          ))}
        </div>
      ) : (
        <div className="widget-empty">
          <Clock size={24} />
          <span>No upcoming releases</span>
        </div>
      )}
    </div>
  );
}
