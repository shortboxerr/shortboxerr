import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { 
  ArrowLeft, ExternalLink, Calendar, Hash, 
  FileText, Check, X, Clock, Layers
} from 'lucide-react';
import { api } from '../api/client';
import type { EditionContent } from '../api/client';

export function EditionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const editionId = parseInt(id ?? '0', 10);

  const { data: edition, isLoading } = useQuery({
    queryKey: ['edition', editionId],
    queryFn: () => api.getEditionDetail(editionId),
    enabled: editionId > 0,
  });

  if (isLoading) {
    return (
      <div className="page-content">
        <div className="loading"><div className="spinner" /></div>
      </div>
    );
  }

  if (!edition) {
    return (
      <div className="page-content">
        <div className="empty-state">
          <Layers size={48} />
          <div className="empty-state-title">Edition not found</div>
          <div className="empty-state-text">
            The edition you're looking for doesn't exist or has been removed.
          </div>
          <Link to="/collections" className="btn btn-primary" style={{ marginTop: '16px' }}>
            <ArrowLeft size={16} />
            Back to Collections
          </Link>
        </div>
      </div>
    );
  }

  const placeholderCover = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="200" height="300" viewBox="0 0 200 300"%3E%3Crect fill="%232a2d35" width="200" height="300"/%3E%3Ctext fill="%236b7280" font-family="sans-serif" font-size="14" x="100" y="150" text-anchor="middle"%3ENo Cover%3C/text%3E%3C/svg%3E';
  
  const editionTypeLabels: Record<string, string> = {
    '0': 'Trade Paperback',
    '1': 'Hardcover',
    '2': 'Omnibus',
    '3': 'Compendium',
    '4': 'Absolute Edition',
    '5': 'Deluxe Edition',
    '99': 'Other',
    'TradesPaperback': 'Trade Paperback',
    'Hardcover': 'Hardcover',
    'Omnibus': 'Omnibus',
    'Compendium': 'Compendium',
    'AbsoluteEdition': 'Absolute Edition',
    'DeluxeEdition': 'Deluxe Edition',
    'Other': 'Other'
  };

  const editionTypeLabel = editionTypeLabels[String(edition.editionType)] ?? 'Collection';

  // Group contents by series
  const contentsBySeries = edition.contents.reduce((acc, content) => {
    const seriesTitle = content.seriesTitle ?? 'Unknown Series';
    if (!acc[seriesTitle]) {
      acc[seriesTitle] = { seriesId: content.seriesId, contents: [] };
    }
    acc[seriesTitle].contents.push(content);
    return acc;
  }, {} as Record<string, { seriesId: number | null; contents: EditionContent[] }>);

  const ownedIssuesCount = edition.contents.filter(c => c.issueHasFile).length;

  return (
    <>
      <header className="page-header">
        <Link to="/collections" className="btn btn-icon" title="Back to Collections">
          <ArrowLeft size={20} />
        </Link>
        <h1 className="page-title">{edition.title}</h1>
        <div className="toolbar-group">
          {/* Future: refresh, edit buttons */}
        </div>
      </header>

      <div className="page-content">
        {/* Edition Header */}
        <div className="edition-detail-header">
          <img
            src={edition.coverImageUrl || placeholderCover}
            alt={edition.title}
            className="edition-detail-cover"
            onError={(e) => {
              (e.target as HTMLImageElement).src = placeholderCover;
            }}
          />
          <div className="edition-detail-info">
            <div className="edition-detail-meta">
              <span className={`badge badge-${getEditionTypeBadge(edition.editionType)}`}>
                {editionTypeLabel}
              </span>
              {edition.volumeNumber && (
                <span className="edition-detail-volume">Vol. {edition.volumeNumber}</span>
              )}
              {edition.hasFile ? (
                <span className="badge badge-success">
                  <Check size={12} /> Owned
                </span>
              ) : (
                <span className="badge badge-warning">
                  <Clock size={12} /> Wanted
                </span>
              )}
              {edition.monitored && !edition.hasFile && (
                <span className="badge badge-info">Monitored</span>
              )}
            </div>

            {edition.seriesTitle && (
              <div className="edition-detail-series">
                <span className="edition-detail-label">Series:</span>
                {edition.seriesId ? (
                  <Link to={`/series/${edition.seriesId}`} className="edition-detail-series-link">
                    {edition.seriesTitle}
                  </Link>
                ) : (
                  <span>{edition.seriesTitle}</span>
                )}
              </div>
            )}

            {edition.publisher && (
              <div className="edition-detail-publisher">
                <span className="edition-detail-label">Publisher:</span> {edition.publisher}
              </div>
            )}

            <div className="edition-detail-stats">
              {edition.releaseDate && (
                <div className="edition-detail-stat">
                  <Calendar size={16} />
                  <span>{formatDate(edition.releaseDate)}</span>
                </div>
              )}
              {edition.pageCount && (
                <div className="edition-detail-stat">
                  <FileText size={16} />
                  <span>{edition.pageCount} pages</span>
                </div>
              )}
              <div className="edition-detail-stat">
                <Layers size={16} />
                <span>{edition.contentCount} issues</span>
              </div>
              {edition.isbn && (
                <div className="edition-detail-stat">
                  <Hash size={16} />
                  <span>ISBN: {edition.isbn}</span>
                </div>
              )}
            </div>

            {edition.overview && (
              <p className="edition-detail-overview">{stripHtml(edition.overview)}</p>
            )}

            {edition.comicVineUrl && (
              <a
                href={edition.comicVineUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="edition-detail-link"
              >
                <ExternalLink size={14} />
                View on ComicVine
              </a>
            )}
          </div>
        </div>

        {/* Contents Section */}
        <div className="edition-detail-section">
          <div className="edition-detail-section-header">
            <h2>Contained Issues</h2>
            <div className="edition-detail-section-stats">
              <span className="badge badge-success">{ownedIssuesCount} owned</span>
              <span className="badge badge-muted">{edition.contentCount - ownedIssuesCount} missing</span>
            </div>
          </div>

          {edition.contents.length === 0 ? (
            <div className="empty-state" style={{ padding: '40px 20px' }}>
              <Layers size={48} style={{ opacity: 0.3 }} />
              <div className="empty-state-title">No issues mapped</div>
              <div className="empty-state-text">
                This edition doesn't have any issues mapped to it yet.
              </div>
            </div>
          ) : (
            <div className="edition-contents">
              {Object.entries(contentsBySeries).map(([seriesTitle, { seriesId, contents }]) => (
                <div key={seriesTitle} className="edition-contents-series">
                  <div className="edition-contents-series-header">
                    {seriesId ? (
                      <Link to={`/series/${seriesId}`} className="edition-contents-series-title">
                        {seriesTitle}
                      </Link>
                    ) : (
                      <span className="edition-contents-series-title">{seriesTitle}</span>
                    )}
                    <span className="edition-contents-series-count">{contents.length} issues</span>
                  </div>
                  <div className="edition-contents-list">
                    {contents.map((content) => (
                      <EditionContentItem key={content.id} content={content} />
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </>
  );
}

interface EditionContentItemProps {
  content: EditionContent;
}

function EditionContentItem({ content }: EditionContentItemProps) {
  const placeholderCover = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="40" height="60" viewBox="0 0 40 60"%3E%3Crect fill="%232a2d35" width="40" height="60"/%3E%3C/svg%3E';
  
  const displayNumber = content.issueNumberText ?? 
    (content.issueNumber !== null ? `#${content.issueNumber}` : 'N/A');
  
  return (
    <div className={`edition-content-item ${content.issueHasFile ? 'owned' : 'missing'}`}>
      <img 
        src={content.issueCoverImageUrl || placeholderCover}
        alt={displayNumber}
        className="edition-content-cover"
        onError={(e) => {
          (e.target as HTMLImageElement).src = placeholderCover;
        }}
      />
      <div className="edition-content-info">
        <div className="edition-content-number">{displayNumber}</div>
        {content.issueTitle && (
          <div className="edition-content-title" title={content.issueTitle}>
            {content.issueTitle}
          </div>
        )}
      </div>
      <div className="edition-content-status">
        {content.issueHasFile ? (
          <span className="status-badge status-owned"><Check size={12} /> Owned</span>
        ) : (
          <span className="status-badge status-missing"><X size={12} /> Missing</span>
        )}
      </div>
    </div>
  );
}

function getEditionTypeBadge(editionType: string): string {
  switch (String(editionType)) {
    case '0':
    case 'TradesPaperback':
      return 'info';
    case '1':
    case 'Hardcover':
      return 'warning';
    case '2':
    case 'Omnibus':
      return 'success';
    case '4':
    case 'AbsoluteEdition':
      return 'danger';
    default:
      return 'muted';
  }
}

function formatDate(dateStr: string | null): string {
  if (!dateStr) return '';
  const date = new Date(dateStr);
  return date.toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' });
}

function stripHtml(html: string): string {
  const doc = new DOMParser().parseFromString(html, 'text/html');
  return doc.body.textContent || '';
}

