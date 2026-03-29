import { useState } from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { Search, RefreshCw, Download, BookOpen, Library, Loader2 } from 'lucide-react';
import { api } from '../api/client';
import { useToast } from '../components/toast/useToast';

type WantedTab = 'issues' | 'collections';

function tabFromSearchParams(searchParams: URLSearchParams): WantedTab {
  const t = searchParams.get('type');
  if (t === 'collections' || t === 'issues') return t;
  return 'issues';
}

export function WantedPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const tab = tabFromSearchParams(searchParams);
  const [search, setSearch] = useState('');
  const toast = useToast();

  const handleTabChange = (newTab: WantedTab) => {
    setSearchParams({ type: newTab });
  };

  const { data: wanted, isLoading, refetch } = useQuery({
    queryKey: ['wanted', tab, search],
    queryFn: () => api.getWanted({ type: tab, search }),
  });

  // Search all wanted issues mutation
  const searchAllWanted = useMutation({
    mutationFn: async () => {
      return api.searchAllWanted();
    },
    onSuccess: (result) => {
      if (result.totalSearched === 0) {
        toast.info('No wanted issues to search');
      } else if (result.successCount > 0) {
        toast.success(`Found downloads for ${result.successCount} of ${result.totalSearched} issues`);
      } else {
        toast.warning(`Searched ${result.totalSearched} issues - no results found`);
      }
    },
    onError: () => {
      toast.error('Search failed');
    },
  });

  const handleSearchAllWanted = () => {
    searchAllWanted.mutate();
  };

  // Search individual issue mutation
  const searchIssue = useMutation({
    mutationFn: async (issueId: number) => {
      return api.searchIssue(issueId);
    },
    onSuccess: (result) => {
      if (result.success) {
        toast.success(`Found: ${result.selectedCandidateTitle || 'Download started'}`);
      } else if (result.candidatesFound === 0) {
        toast.info(`No results found for #${result.issueNumber}`);
      } else {
        toast.warning(result.error || 'Search completed but no download started');
      }
    },
    onError: () => {
      toast.error('Search failed');
    },
  });

  const handleSearchIssue = (issueId: number) => {
    searchIssue.mutate(issueId);
  };

  const items = wanted?.items ?? [];

  return (
    <>
      <header className="page-header">
        <h1 className="page-title">Wanted</h1>
        <div className="toolbar-group">
          <button 
            className="btn btn-primary" 
            onClick={handleSearchAllWanted}
            disabled={searchAllWanted.isPending}
          >
            {searchAllWanted.isPending ? <Loader2 size={16} className="spinning" /> : <Search size={16} />}
            Search All
          </button>
        </div>
      </header>
      
      <div className="page-content">
        <div className="toolbar">
          <div className="toolbar-group" style={{ borderRadius: 'var(--radius-md)', overflow: 'hidden' }}>
            <button 
              className={`btn ${tab === 'issues' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => handleTabChange('issues')}
              style={{ borderRadius: 0, borderRight: 'none' }}
            >
              <BookOpen size={16} />
              Issues
            </button>
            <button 
              className={`btn ${tab === 'collections' ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => handleTabChange('collections')}
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
                      {tab === 'issues' && (
                        <button 
                          className="btn btn-icon" 
                          title="Search for download"
                          onClick={() => handleSearchIssue(item.id)}
                          disabled={searchIssue.isPending && searchIssue.variables === item.id}
                        >
                          {searchIssue.isPending && searchIssue.variables === item.id 
                            ? <Loader2 size={16} className="spinning" />
                            : <Search size={16} />
                          }
                        </button>
                      )}
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

