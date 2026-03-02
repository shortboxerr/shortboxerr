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
  const [theme, setThemeState] = useState<Theme>('dark');
  const [effectiveTheme, setEffectiveTheme] = useState<'dark' | 'light'>('dark');

  // Load theme from API on startup
  const { data: uiSettings } = useQuery({
    queryKey: ['uiSettings'],
    queryFn: api.getUiSettings,
    staleTime: Infinity, // Don't refetch unless explicitly invalidated
  });

  // Update theme when API data loads
  useEffect(() => {
    if (uiSettings?.theme) {
      setThemeState(uiSettings.theme);
    }
  }, [uiSettings]);

  // Apply theme to document
  // Theme variables are defined in CSS via [data-theme="light"] selector
  // This approach is more maintainable and ensures all variables are properly scoped
  useEffect(() => {
    const newEffectiveTheme = theme === 'system' ? getSystemTheme() : theme;
    setEffectiveTheme(newEffectiveTheme);
    
    // Apply theme via data attribute - CSS handles the variable values
    document.documentElement.dataset.theme = newEffectiveTheme;
  }, [theme]);

  // Listen for system theme changes
  useEffect(() => {
    if (theme !== 'system') return;
    
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const handler = (e: MediaQueryListEvent) => {
      setEffectiveTheme(e.matches ? 'dark' : 'light');
    };
    
    mediaQuery.addEventListener('change', handler);
    return () => mediaQuery.removeEventListener('change', handler);
  }, [theme]);

  const setTheme = async (newTheme: Theme) => {
    setThemeState(newTheme);
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
