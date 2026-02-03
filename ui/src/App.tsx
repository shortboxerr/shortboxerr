import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { createContext, useContext, useEffect, useState } from 'react';
import { Layout } from './components/Layout';
import { Dashboard } from './pages/Dashboard';
import { SeriesPage } from './pages/SeriesPage';
import { SeriesDetailPage } from './pages/SeriesDetailPage';
import { CollectionsPage } from './pages/CollectionsPage';
import { EditionDetailPage } from './pages/EditionDetailPage';
import { ActivityPage } from './pages/ActivityPage';
import { ManualImportPage } from './pages/ManualImportPage';
import { HistoryPage } from './pages/HistoryPage';
import { SettingsPage } from './pages/SettingsPage';
import { WantedPage } from './pages/WantedPage';
import { PullListPage } from './pages/PullListPage';
import { api } from './api/client';
import './App.css';

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
  useEffect(() => {
    const newEffectiveTheme = theme === 'system' ? getSystemTheme() : theme;
    setEffectiveTheme(newEffectiveTheme);
    
    // Apply theme class to document
    document.documentElement.dataset.theme = newEffectiveTheme;
    
    // Also update CSS custom properties for immediate effect
    if (newEffectiveTheme === 'light') {
      document.documentElement.style.setProperty('--bg-primary', '#f8f9fa');
      document.documentElement.style.setProperty('--bg-secondary', '#ffffff');
      document.documentElement.style.setProperty('--bg-tertiary', '#e9ecef');
      document.documentElement.style.setProperty('--bg-active', '#dee2e6');
      document.documentElement.style.setProperty('--text-primary', '#212529');
      document.documentElement.style.setProperty('--text-secondary', '#495057');
      document.documentElement.style.setProperty('--text-muted', '#6c757d');
      document.documentElement.style.setProperty('--border-color', '#dee2e6');
    } else {
      // Reset to dark theme (default CSS variables)
      document.documentElement.style.removeProperty('--bg-primary');
      document.documentElement.style.removeProperty('--bg-secondary');
      document.documentElement.style.removeProperty('--bg-tertiary');
      document.documentElement.style.removeProperty('--bg-active');
      document.documentElement.style.removeProperty('--text-primary');
      document.documentElement.style.removeProperty('--text-secondary');
      document.documentElement.style.removeProperty('--text-muted');
      document.documentElement.style.removeProperty('--border-color');
    }
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
        <BrowserRouter>
          <Routes>
            <Route path="/" element={<Layout />}>
              <Route index element={<Dashboard />} />
              <Route path="series" element={<SeriesPage />} />
              <Route path="series/:id" element={<SeriesDetailPage />} />
              <Route path="collections" element={<CollectionsPage />} />
              <Route path="collections/:id" element={<EditionDetailPage />} />
              <Route path="wanted" element={<WantedPage />} />
              <Route path="pulllist" element={<PullListPage />} />
              <Route path="activity" element={<ActivityPage />} />
              <Route path="history" element={<HistoryPage />} />
              <Route path="import" element={<ManualImportPage />} />
              <Route path="settings" element={<SettingsPage />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </ThemeProvider>
    </QueryClientProvider>
  );
}

export default App;
