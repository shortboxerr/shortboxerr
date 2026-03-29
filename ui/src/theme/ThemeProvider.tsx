import { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ThemeContext, type Theme, type ThemeContextType } from './ThemeContext';
import { api } from '../api/client';

function getSystemTheme(): 'dark' | 'light' {
  if (typeof window !== 'undefined' && window.matchMedia) {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
  return 'dark';
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const queryClient = useQueryClient();
  const [userThemeOverride, setUserThemeOverride] = useState<Theme | null>(null);
  const [systemTheme, setSystemTheme] = useState<'dark' | 'light'>(() => getSystemTheme());

  const { data: uiSettings } = useQuery({
    queryKey: ['settings', 'ui'],
    queryFn: api.getUiSettings,
    staleTime: Infinity,
  });

  const theme: Theme = userThemeOverride ?? uiSettings?.theme ?? 'dark';
  const effectiveTheme: 'dark' | 'light' = theme === 'system' ? systemTheme : theme;

  useEffect(() => {
    document.documentElement.dataset.theme = effectiveTheme;
  }, [effectiveTheme]);

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
    const previousTheme = userThemeOverride;
    setUserThemeOverride(newTheme);
    try {
      await api.updateUiSettings({ theme: newTheme });
      queryClient.invalidateQueries({ queryKey: ['settings', 'ui'] });
    } catch (e) {
      console.error('Failed to save theme preference:', e);
      setUserThemeOverride(previousTheme);
    }
  };

  const value: ThemeContextType = { theme, setTheme, effectiveTheme };

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}
