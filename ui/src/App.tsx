import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Suspense, lazy } from 'react';
import { Layout } from './components/Layout';
import { ToastProvider } from './components/toast/ToastProvider';
import { ThemeProvider } from './theme/ThemeProvider';
import { Dashboard } from './pages/Dashboard';
import { SeriesPage } from './pages/SeriesPage';
import { SeriesDetailPage } from './pages/SeriesDetailPage';
import { ActivityPage } from './pages/ActivityPage';
import './App.css';

// Lazy load heavy pages to reduce initial bundle size
const SettingsPage = lazy(() => import('./pages/SettingsPage').then(m => ({ default: m.SettingsPage })));
const PullListPage = lazy(() => import('./pages/PullListPage').then(m => ({ default: m.PullListPage })));
const CollectionsPage = lazy(() => import('./pages/CollectionsPage').then(m => ({ default: m.CollectionsPage })));
const EditionDetailPage = lazy(() => import('./pages/EditionDetailPage').then(m => ({ default: m.EditionDetailPage })));
const ManualImportPage = lazy(() => import('./pages/ManualImportPage').then(m => ({ default: m.ManualImportPage })));
const HistoryPage = lazy(() => import('./pages/HistoryPage').then(m => ({ default: m.HistoryPage })));
const WantedPage = lazy(() => import('./pages/WantedPage').then(m => ({ default: m.WantedPage })));
const CalendarPage = lazy(() => import('./pages/CalendarPage').then(m => ({ default: m.CalendarPage })));
const LogsPage = lazy(() => import('./pages/LogsPage'));
const AddSeriesPage = lazy(() => import('./pages/AddSeriesPage').then(m => ({ default: m.AddSeriesPage })));

function PageLoader() {
  return (
    <div style={{ 
      display: 'flex', 
      justifyContent: 'center', 
      alignItems: 'center', 
      height: '50vh',
      color: 'var(--text-muted)'
    }}>
      Loading...
    </div>
  );
}

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
      <ThemeProvider>
        <ToastProvider>
          <BrowserRouter>
            <Suspense fallback={<PageLoader />}>
              <Routes>
                <Route path="/" element={<Layout />}>
                  <Route index element={<Dashboard />} />
                  <Route path="series" element={<SeriesPage />} />
                  <Route path="series/add" element={<AddSeriesPage />} />
                  <Route path="series/:id" element={<SeriesDetailPage />} />
                  <Route path="collections" element={<CollectionsPage />} />
                  <Route path="collections/:id" element={<EditionDetailPage />} />
                  <Route path="wanted" element={<WantedPage />} />
                  <Route path="pulllist" element={<PullListPage />} />
                  <Route path="calendar" element={<CalendarPage />} />
                  <Route path="activity" element={<ActivityPage />} />
                  <Route path="history" element={<HistoryPage />} />
                  <Route path="import" element={<ManualImportPage />} />
                  <Route path="settings" element={<SettingsPage />} />
                  <Route path="logs" element={<LogsPage />} />
                </Route>
              </Routes>
            </Suspense>
          </BrowserRouter>
        </ToastProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
}

export default App;
