import { createContext, useContext } from 'react';

export type Theme = 'dark' | 'light' | 'system';

export interface ThemeContextType {
  theme: Theme;
  setTheme: (theme: Theme) => void;
  effectiveTheme: 'dark' | 'light';
}

export const ThemeContext = createContext<ThemeContextType | null>(null);

export function useTheme(): ThemeContextType {
  const context = useContext(ThemeContext);
  if (!context) {
    throw new Error('useTheme must be used within ThemeProvider');
  }
  return context;
}
