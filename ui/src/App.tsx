import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Layout } from './components/Layout';
import { Dashboard } from './pages/Dashboard';
import { SeriesPage } from './pages/SeriesPage';
import { CollectionsPage } from './pages/CollectionsPage';
import { ActivityPage } from './pages/ActivityPage';
import { ManualImportPage } from './pages/ManualImportPage';
import { HistoryPage } from './pages/HistoryPage';
import { SettingsPage } from './pages/SettingsPage';
import { WantedPage } from './pages/WantedPage';
import './App.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30000,
      retry: 1,
    },
  },
});

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Layout />}>
            <Route index element={<Dashboard />} />
            <Route path="series" element={<SeriesPage />} />
            <Route path="collections" element={<CollectionsPage />} />
            <Route path="wanted" element={<WantedPage />} />
            <Route path="activity" element={<ActivityPage />} />
            <Route path="history" element={<HistoryPage />} />
            <Route path="import" element={<ManualImportPage />} />
            <Route path="settings" element={<SettingsPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

export default App;
