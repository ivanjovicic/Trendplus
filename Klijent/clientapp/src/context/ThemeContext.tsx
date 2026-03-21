import React, { createContext, useContext, useEffect, useState } from 'react';
import { THEME_TOKENS } from '../styles/themeTokens';

export type ThemeName = 'inventory-dark' | 'light' | 'high-contrast';

export interface Theme {
  name: ThemeName;
  displayName: string;
  description: string;
  cssVars: Record<string, string>;
}

const THEMES: Record<ThemeName, Theme> = {
  'inventory-dark': {
    name: 'inventory-dark',
    displayName: 'Bilans Stanja (Tamna)',
    description: 'Optimizovana za dugotrajno korišćenje i čitanje tabela',
      cssVars: {
      '--surface-default': 'var(--surface-default)',
      '--surface-light': 'var(--surface-light)',
      '--surface-elevated': 'var(--surface-elevated)',
      '--text-primary': 'var(--text-primary)',
      '--text-secondary': 'var(--text-secondary)',
      '--text-muted': 'var(--text-muted)',
      '--border-default': 'var(--border-default)',
      '--border-hover': 'var(--border-hover)',
      '--focus-ring': 'var(--focus-ring)',
      '--gray-50': 'var(--gray-50)',
      '--gray-100': 'var(--gray-100)',
      '--gray-200': 'var(--gray-200)',
      '--gray-300': 'var(--gray-300)',
      '--gray-400': 'var(--gray-400)',
      '--gray-500': 'var(--gray-500)',
      '--gray-600': 'var(--gray-600)',
      '--gray-700': 'var(--gray-700)',
      '--gray-800': 'var(--gray-800)',
      '--gray-900': 'var(--gray-900)',
      '--success': 'var(--success)',
      '--error': 'var(--error)',
      '--warning': 'var(--warning)',
      '--info': 'var(--info)',
      '--surface-darker': 'var(--surface-darker)',
    },
  },
  light: {
    name: 'light',
    displayName: 'Svetla',
    description: 'Klasična svetla tema za dnevno korišćenje',
      cssVars: {
      '--surface-default': 'var(--surface-default)',
      '--surface-light': 'var(--surface-light)',
      '--surface-elevated': 'var(--surface-elevated)',
      '--text-primary': 'var(--text-primary)',
      '--text-secondary': 'var(--text-secondary)',
      '--text-muted': 'var(--text-muted)',
      '--border-default': 'var(--border-default)',
      '--border-hover': 'var(--border-hover)',
      '--focus-ring': 'var(--focus-ring)',
      '--gray-50': 'var(--gray-50)',
      '--gray-100': 'var(--gray-100)',
      '--gray-200': 'var(--gray-200)',
      '--gray-300': 'var(--gray-300)',
      '--gray-400': 'var(--gray-400)',
      '--gray-500': 'var(--gray-500)',
      '--gray-600': 'var(--gray-600)',
      '--gray-700': 'var(--gray-700)',
      '--gray-800': 'var(--gray-800)',
      '--gray-900': 'var(--gray-900)',
      '--success': 'var(--success)',
      '--error': 'var(--error)',
      '--warning': 'var(--warning)',
      '--info': 'var(--info)',
      '--surface-darker': 'var(--surface-darker)',
    },
  },
  'high-contrast': {
    name: 'high-contrast',
    displayName: 'Visoki kontrast',
    description: 'Maksimalni kontrast za bolje čitanje',
      cssVars: {
      '--surface-default': 'var(--surface-default)',
      '--surface-light': 'var(--surface-light)',
      '--surface-elevated': 'var(--surface-elevated)',
      '--text-primary': 'var(--text-primary)',
      '--text-secondary': 'var(--text-secondary)',
      '--text-muted': 'var(--text-muted)',
      '--border-default': 'var(--border-default)',
      '--border-hover': 'var(--border-hover)',
      '--focus-ring': 'var(--focus-ring)',
      '--gray-50': 'var(--gray-50)',
      '--gray-100': 'var(--gray-100)',
      '--gray-200': 'var(--gray-200)',
      '--gray-300': 'var(--gray-300)',
      '--gray-400': 'var(--gray-400)',
      '--gray-500': 'var(--gray-500)',
      '--gray-600': 'var(--gray-600)',
      '--gray-700': 'var(--gray-700)',
      '--gray-800': 'var(--gray-800)',
      '--gray-900': 'var(--gray-900)',
      '--success': 'var(--success)',
      '--error': 'var(--error)',
      '--warning': 'var(--warning)',
      '--info': 'var(--info)',
      '--surface-darker': 'var(--surface-darker)',
    },
  },
};

interface ThemeContextType {
  currentTheme: ThemeName;
  themes: Record<ThemeName, Theme>;
  setTheme: (theme: ThemeName) => void;
  isDark: boolean;
}

const ThemeContext = createContext<ThemeContextType | undefined>(undefined);

interface ThemeProviderProps {
  children: React.ReactNode;
  defaultTheme?: ThemeName;
}

export function ThemeProvider({ children, defaultTheme = 'inventory-dark' }: ThemeProviderProps) {
  const [currentTheme, setCurrentTheme] = useState<ThemeName>(() => {
    // Try to load from localStorage
    const saved = localStorage.getItem('app-theme');
    return (saved && Object.keys(THEMES).includes(saved)) ? saved as ThemeName : defaultTheme;
  });

  const setTheme = (theme: ThemeName) => {
    setCurrentTheme(theme);
    localStorage.setItem('app-theme', theme);
  };

  // Apply CSS custom properties when theme changes
  useEffect(() => {
    const theme = THEMES[currentTheme];
    const root = document.documentElement;

    // Prefer using a data attribute to allow CSS to scope theme variables defined in CSS files.
    root.setAttribute('data-theme', currentTheme);

    // Only set properties that are explicit values (not references to other CSS vars)
    Object.entries(theme.cssVars).forEach(([property, value]) => {
      if (typeof value === 'string' && value.trim().startsWith('var(')) {
        // skip overriding if value references existing CSS variable
        return;
      }
      root.style.setProperty(property, value as string);
    });
  }, [currentTheme]);

  const isDark = currentTheme === 'inventory-dark' || currentTheme === 'high-contrast';

  return (
    <ThemeContext.Provider 
      value={{
        currentTheme,
        themes: THEMES,
        setTheme,
        isDark,
      }}
    >
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  const context = useContext(ThemeContext);
  if (context === undefined) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return context;
}