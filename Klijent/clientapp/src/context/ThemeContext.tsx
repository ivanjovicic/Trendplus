import React, { createContext, useContext, useEffect, useState } from "react";

export type ThemeName = "inventory-dark" | "light" | "high-contrast";

export interface Theme {
  name: ThemeName;
  displayName: string;
  description: string;
  cssVars: Record<string, string>;
}

const GRAY_VARS: Record<string, string> = {
  "--gray-50": "#f8fafb",
  "--gray-100": "#f1f5f9",
  "--gray-200": "#e2e8f0",
  "--gray-300": "#cbd5e1",
  "--gray-400": "#94a3b8",
  "--gray-500": "#64748b",
  "--gray-600": "#475569",
  "--gray-700": "#334155",
  "--gray-800": "#1f2933",
  "--gray-900": "#0b1220",
};

const STATUS_VARS: Record<string, string> = {
  "--success": "#10b981",
  "--error": "#ef4444",
  "--warning": "#f59e0b",
  "--info": "#3b82f6",
};

function withBaseVars(overrides: Record<string, string>): Record<string, string> {
  return {
    ...GRAY_VARS,
    ...STATUS_VARS,
    ...overrides,
  };
}

const THEMES: Record<ThemeName, Theme> = {
  "inventory-dark": {
    name: "inventory-dark",
    displayName: "Bilans Stanja (Tamna)",
    description: "Optimizovana za dugotrajno citanje tabela",
    cssVars: withBaseVars({
      "--surface-default": "#0f1318",
      "--surface-light": "#10141c",
      "--surface-elevated": "#12161f",
      "--surface-darker": "#0a0d14",
      "--text-primary": "#dbe6fb",
      "--text-secondary": "#9aa9c6",
      "--text-muted": "#73809a",
      "--border-default": "#283042",
      "--border-hover": "#344a66",
      "--focus-ring": "#44d0ff",
    }),
  },
  light: {
    name: "light",
    displayName: "Svetla",
    description: "Klasicna svetla tema za dnevno koriscenje",
    cssVars: withBaseVars({
      "--surface-default": "#f4f7fb",
      "--surface-light": "#ffffff",
      "--surface-elevated": "#ffffff",
      "--surface-darker": "#e6edf7",
      "--text-primary": "#0f172a",
      "--text-secondary": "#334155",
      "--text-muted": "#64748b",
      "--border-default": "#d3dce9",
      "--border-hover": "#a7b9d3",
      "--focus-ring": "#2563eb",
    }),
  },
  "high-contrast": {
    name: "high-contrast",
    displayName: "Visoki kontrast",
    description: "Maksimalni kontrast za bolje citanje",
    cssVars: withBaseVars({
      "--surface-default": "#000000",
      "--surface-light": "#090909",
      "--surface-elevated": "#111111",
      "--surface-darker": "#000000",
      "--text-primary": "#ffffff",
      "--text-secondary": "#f5f5f5",
      "--text-muted": "#d4d4d4",
      "--border-default": "#ffffff",
      "--border-hover": "#ffe066",
      "--focus-ring": "#ffe066",
      "--success": "#00ff88",
      "--error": "#ff5c5c",
      "--warning": "#ffd166",
      "--info": "#5cc8ff",
    }),
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

export function ThemeProvider({ children, defaultTheme = "inventory-dark" }: ThemeProviderProps) {
  const [currentTheme, setCurrentTheme] = useState<ThemeName>(() => {
    const saved = localStorage.getItem("app-theme");
    return saved && (saved in THEMES) ? (saved as ThemeName) : defaultTheme;
  });

  const setTheme = (theme: ThemeName) => {
    setCurrentTheme(theme);
    localStorage.setItem("app-theme", theme);
  };

  useEffect(() => {
    const theme = THEMES[currentTheme];
    const root = document.documentElement;

    root.setAttribute("data-theme", currentTheme);
    Object.entries(theme.cssVars).forEach(([property, value]) => {
      root.style.setProperty(property, value);
    });
  }, [currentTheme]);

  const isDark = currentTheme === "inventory-dark" || currentTheme === "high-contrast";

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
    throw new Error("useTheme must be used within a ThemeProvider");
  }
  return context;
}

