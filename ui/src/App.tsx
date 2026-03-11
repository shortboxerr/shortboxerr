import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { createContext, useContext, useEffect, useState, Suspense, lazy } from 'react';
import { Layout } from './components/Layout';
import { ToastProvider } from './components/Toast';
import { Dashboard } from './pages/Dashboard';
import { SeriesPage } from './pages/SeriesPage';
import { SeriesDetailPage } from './pages/SeriesDetailPage';
import { ActivityPage } from './pages/ActivityPage';
import { api } from './api/client';
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

// Theme context
type Theme = 'dark' | 'light' | 'system';

interface ThemeContextType {
  theme: Theme;
  setTheme: (theme: Theme) => void;
  effectiveTheme: 'dark' | 'light';
}

const ThemeContext = createContext<ThemeContextType | null>(null);

export function useTheme() {
  const context = useContext(ThemeContext);
  if (!context) {
    throw new Error('useTheme must be used within ThemeProvider');
  }
  return context;
}

function getSystemTheme(): 'dark' | 'light' {
  if (typeof window !== 'undefined' && window.matchMedia) {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
  return 'dark';
}

function ThemeProvider({ children }: { children: React.ReactNode }) {
  // User override (when they change theme before API has loaded); null = use API or default
  const [userThemeOverride, setUserThemeOverride] = useState<Theme | null>(null);
  // System preference when theme is 'system'; updated only via media query callback
  const [systemTheme, setSystemTheme] = useState<'dark' | 'light'>(() => getSystemTheme());

  const { data: uiSettings } = useQuery({
    queryKey: ['uiSettings'],
    queryFn: api.getUiSettings,
    staleTime: Infinity,
  });

  // Derive theme in render (no setState in effect)
  const theme: Theme = userThemeOverride ?? uiSettings?.theme ?? 'dark';
  const effectiveTheme: 'dark' | 'light' = theme === 'system' ? systemTheme : theme;

  // Apply theme to document
  useEffect(() => {
    document.documentElement.dataset.theme = effectiveTheme;
  }, [effectiveTheme]);

  // Subscribe to system preference changes when theme is 'system' (setState in callback, not effect body)
  useEffect(() => {
    if (theme !== 'system') return;
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const handler = (e: MediaQueryListEvent) => {
      setSystemTheme(e.matches ? 'dark' : 'light');
    };
    mediaQuery.addEventListener('change', handler);
    return () => mediaQuery.removeEventListener('change', handler);
  }, [theme]);

  const setTheme = async (newTheme: Theme) => {
    setUserThemeOverride(newTheme);
    try {
      await api.updateUiSettings({ theme: newTheme });
      // Invalidate the query to keep cache in sync
      queryClient.invalidateQueries({ queryKey: ['uiSettings'] });
    } catch (e) {
      console.error('Failed to save theme preference:', e);
    }
  };

  return (
    <ThemeContext.Provider value={{ theme, setTheme, effectiveTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}

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
