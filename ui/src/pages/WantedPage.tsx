import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Search, RefreshCw, Download, BookOpen, Library } from 'lucide-react';
import { api } from '../api/client';

// WantedItem interface is used implicitly through the API response

export function WantedPage() {
  const [tab, setTab] = useState<'issues' | 'collections'>('issues');
  const [search, setSearch] = useState('');

  const { data: wanted, isLoading, refetch } = useQuery({
    queryKey: ['wanted', tab, search],
    queryFn: () => api.getWanted({ type: tab, search }),
  });

  const items = wanted?.items ?? [];

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">Wanted</h1>
        <div className="toolbar-group">
          <button className="btn btn-primary">
            <Search size={16} />
            Search All
          </button>
        </div>
      </header>
      
      <div className="page-content">
        <div className="toolbar">
          <div className="toolbar-group" style={{ borderRadius: 'var(--radius-md)', overflow: 'hidden' }}>
            <button 
              className={`btn ${tab === 'issues' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setTab('issues')}
              style={{ borderRadius: 0, borderRight: 'none' }}
            >
              <BookOpen size={16} />
              Issues
            </button>
            <button 
              className={`btn ${tab === 'collections' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setTab('collections')}
              style={{ borderRadius: 0 }}
            >
              <Library size={16} />
              Collections
            </button>
          </div>
          
          <input
            type="text"
            className="input search-input"
            placeholder="Filter wanted..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          
          <div className="toolbar-spacer" />
          
          <button className="btn btn-icon" onClick={() => refetch()} title="Refresh">
            <RefreshCw size={18} />
          </button>
        </div>
        
        <div className="table-container">
          {isLoading ? (
            <div className="loading"><div className="spinner" /></div>
          ) : items.length === 0 ? (
            <div className="empty-state">
              <Search size={48} />
              <div className="empty-state-title">No wanted {tab}</div>
              <div className="empty-state-text">
                {search 
                  ? 'No matches found.' 
                  : `All ${tab} have been downloaded or nothing is being tracked.`
                }
              </div>
            </div>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Title</th>
                  <th>Series</th>
                  {tab === 'issues' && <th>Issue</th>}
                  {tab === 'collections' && <th>Type</th>}
                  {tab === 'collections' && <th>Volume</th>}
                  <th>Added</th>
                  <th className="table-actions"></th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr key={item.id}>
                    <td style={{ color: 'var(--text-primary)', fontWeight: 500 }}>
                      {item.title}
                    </td>
                    <td>{item.series}</td>
                    {tab === 'issues' && <td>#{item.issueNumber}</td>}
                    {tab === 'collections' && (
                      <td>
                        <span className="badge badge-info">{item.editionType}</span>
                      </td>
                    )}
                    {tab === 'collections' && <td>{item.volumeNumber ?? '-'}</td>}
                    <td style={{ color: 'var(--text-muted)' }}>{item.dateAdded}</td>
                    <td className="table-actions">
                      <button className="btn btn-icon" title="Search for download">
                        <Search size={16} />
                      </button>
                      <button className="btn btn-icon" title="Manual download">
                        <Download size={16} />
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

