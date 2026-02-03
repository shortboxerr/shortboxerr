import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, ExternalLink, RefreshCw, Calendar, BookOpen, HardDrive, Check, X, Clock } from 'lucide-react';
import { api } from '../api/client';
import type { Issue } from '../api/client';

export function SeriesDetailPage() {
  const { id } = useParams<{ id: string }>();
  const seriesId = parseInt(id ?? '0', 10);

  const { data: series, isLoading: isLoadingSeries } = useQuery({
    queryKey: ['series', seriesId],
    queryFn: () => api.getSeriesById(seriesId),
    enabled: seriesId > 0,
  });

  const { data: issuesData, isLoading: isLoadingIssues } = useQuery({
    queryKey: ['series', seriesId, 'issues'],
    queryFn: () => api.getSeriesIssues(seriesId, { pageSize: 500 }),
    enabled: seriesId > 0,
  });

  const issues = issuesData?.items ?? [];

  if (isLoadingSeries) {
    return (
      <div className="page-content">
        <div className="loading"><div className="spinner" /></div>
      </div>
    );
  }

  if (!series) {
    return (
      <div className="page-content">
        <div className="empty-state">
          <BookOpen size={48} />
          <div className="empty-state-title">Series not found</div>
          <div className="empty-state-text">
            The series you're looking for doesn't exist or has been removed.
          </div>
          <Link to="/series" className="btn btn-primary" style={{ marginTop: '16px' }}>
            <ArrowLeft size={16} />
            Back to Series
          </Link>
        </div>
      </div>
    );
  }

  const placeholderCover = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="200" height="300" viewBox="0 0 200 300"%3E%3Crect fill="%232a2d35" width="200" height="300"/%3E%3Ctext fill="%236b7280" font-family="sans-serif" font-size="14" x="100" y="150" text-anchor="middle"%3ENo Cover%3C/text%3E%3C/svg%3E';
  const ownedCount = issues.filter(i => i.hasFile).length;
  const wantedCount = issues.filter(i => i.monitored && !i.hasFile && !i.satisfiedByEdition).length;

  return (
    <>
      <header className="page-header">
        <Link to="/series" className="btn btn-icon" title="Back to Series">
          <ArrowLeft size={20} />
        </Link>
        <h1 className="page-title">{series.title}</h1>
        <div className="toolbar-group">
          <button className="btn btn-icon" title="Refresh Metadata">
            <RefreshCw size={18} />
          </button>
        </div>
      </header>

      <div className="page-content">
        {/* Series Header */}
        <div className="series-detail-header">
          <img
            src={series.coverImageUrl || placeholderCover}
            alt={series.title}
            className="series-detail-cover"
            onError={(e) => {
              (e.target as HTMLImageElement).src = placeholderCover;
            }}
          />
          <div className="series-detail-info">
            <div className="series-detail-meta">
              {series.publisher && (
                <span className="series-detail-publisher">{series.publisher}</span>
              )}
              {series.startYear && (
                <span className="series-detail-year">
                  {series.startYear}{series.endYear && series.endYear !== series.startYear ? ` - ${series.endYear}` : ''}
                </span>
              )}
              <span className={`badge badge-${getStatusBadge(series.status)}`}>
                {series.status}
              </span>
              {series.monitored ? (
                <span className="badge badge-success">Monitored</span>
              ) : (
                <span className="badge badge-muted">Not Monitored</span>
              )}
            </div>

            {series.overview && (
              <p className="series-detail-overview">{stripHtml(series.overview)}</p>
            )}

            <div className="series-detail-stats">
              <div className="series-detail-stat">
                <BookOpen size={16} />
                <span>{series.issueCount} issues</span>
              </div>
              <div className="series-detail-stat">
                <HardDrive size={16} />
                <span>{series.issueFileCount} files</span>
              </div>
              {series.totalIssueCount && series.totalIssueCount !== series.issueCount && (
                <div className="series-detail-stat">
                  <Clock size={16} />
                  <span>{series.totalIssueCount} on ComicVine</span>
                </div>
              )}
            </div>

            {series.comicVineUrl && (
              <a
                href={series.comicVineUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="series-detail-link"
              >
                <ExternalLink size={14} />
                View on ComicVine
              </a>
            )}

            {series.metadataLastRefreshed && (
              <div className="series-detail-refreshed">
                Last updated: {new Date(series.metadataLastRefreshed).toLocaleDateString()}
              </div>
            )}
          </div>
        </div>

        {/* Issues Section */}
        <div className="series-detail-section">
          <div className="series-detail-section-header">
            <h2>Issues</h2>
            <div className="series-detail-section-stats">
              <span className="badge badge-success">{ownedCount} owned</span>
              {wantedCount > 0 && <span className="badge badge-warning">{wantedCount} wanted</span>}
            </div>
          </div>

          {isLoadingIssues ? (
            <div className="loading"><div className="spinner" /></div>
          ) : issues.length === 0 ? (
            <div className="empty-state" style={{ padding: '40px 20px' }}>
              <BookOpen size={48} style={{ opacity: 0.3 }} />
              <div className="empty-state-title">No issues found</div>
              <div className="empty-state-text">
                This series doesn't have any issues yet.
              </div>
            </div>
          ) : (
            <div className="issues-grid">
              {issues.map((issue) => (
                <IssueCard key={issue.id} issue={issue} />
              ))}
            </div>
          )}
        </div>
      </div>
    </>
  );
}

interface IssueCardProps {
  issue: Issue;
}

function IssueCard({ issue }: IssueCardProps) {
  const placeholderCover = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="100" height="150" viewBox="0 0 100 150"%3E%3Crect fill="%232a2d35" width="100" height="150"/%3E%3Ctext fill="%236b7280" font-family="sans-serif" font-size="10" x="50" y="75" text-anchor="middle"%3ENo Cover%3C/text%3E%3C/svg%3E';
  
  const status = issue.hasFile ? 'owned' : issue.satisfiedByEdition ? 'edition' : issue.monitored ? 'wanted' : 'skipped';
  
  return (
    <div className={`issue-card issue-card-${status}`}>
      <div className="issue-card-cover-wrapper">
        <img
          src={issue.coverImageUrl || placeholderCover}
          alt={`Issue ${issue.displayNumber}`}
          className="issue-card-cover"
          onError={(e) => {
            (e.target as HTMLImageElement).src = placeholderCover;
          }}
        />
        <div className="issue-card-status">
          {status === 'owned' && <Check size={14} />}
          {status === 'edition' && <BookOpen size={14} />}
          {status === 'wanted' && <Clock size={14} />}
          {status === 'skipped' && <X size={14} />}
        </div>
      </div>
      <div className="issue-card-info">
        <div className="issue-card-number">{issue.displayNumber}</div>
        {issue.title && <div className="issue-card-title" title={issue.title}>{issue.title}</div>}
        {(issue.releaseDate || issue.storeDate) && (
          <div className="issue-card-date">
            <Calendar size={10} />
            {formatDate(issue.storeDate || issue.releaseDate)}
          </div>
        )}
      </div>
    </div>
  );
}

function getStatusBadge(status: string): string {
  switch (status?.toLowerCase()) {
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

function stripHtml(html: string): string {
  const doc = new DOMParser().parseFromString(html, 'text/html');
  return doc.body.textContent || '';
}

function formatDate(dateStr: string | null): string {
  if (!dateStr) return '';
  const date = new Date(dateStr);
  return date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
}

