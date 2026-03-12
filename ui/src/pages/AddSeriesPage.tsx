import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { ArrowLeft } from 'lucide-react';
import { AddSeriesContent } from '../components/AddSeriesContent';

export function AddSeriesPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const handleAdded = async () => {
    await queryClient.refetchQueries({ queryKey: ['series'], type: 'active' });
    await queryClient.invalidateQueries({ queryKey: ['dashboard-stats'] });
    await queryClient.invalidateQueries({ queryKey: ['series-filter-options'] });
    navigate('/series');
  };

  return (
    <>
      <header className="page-header">
        <div className="toolbar-group" style={{ alignItems: 'center', gap: '8px' }}>
          <button type="button" className="btn btn-icon" onClick={() => navigate('/series')} title="Back to Series">
            <ArrowLeft size={20} />
          </button>
          <h1 className="page-title" style={{ margin: 0 }}>Add Series</h1>
        </div>
      </header>
      <div className="page-content" style={{ display: 'flex', flexDirection: 'column', minHeight: '60vh' }}>
        <AddSeriesContent onClose={() => navigate('/series')} onAdded={handleAdded} />
      </div>
    </>
  );
}
