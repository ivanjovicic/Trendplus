import React, { createContext, useContext, useEffect, useState } from "react";

export type ThemeName = "inventory-dark" | "light" | "high-contrast" | "soft-gray";

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

const COMMON_VARS: Record<string, string> = {
  "--text-on-primary": "#ffffff",
  "--text-on-success": "#0f5132",
  "--text-on-error": "#ffffff",
  "--text-on-warning": "#713f12",
  "--text-on-info": "#1e3a8a",
  "--text-on-surface": "var(--text-primary)",
  "--focus-ring-shadow": "rgba(37, 99, 235, 0.08)",
  "--surface-elevated-dark": "#0f1116",
  "--surface-elevated-light": "#1f2430",
  "--surface-border-strong": "#1f2733",
  "--surface-card": "var(--surface-elevated)",
  "--card-shadow": "rgba(0,0,0,0.9)",
  "--muted": "#9ca3af",
  "--accent-primary": "#2563eb",
  "--success-soft": "rgba(16, 185, 129, 0.15)",
  "--warning-soft": "rgba(245, 158, 11, 0.15)",
  "--error-soft": "rgba(239, 68, 68, 0.15)",
  "--neutral-soft": "rgba(140, 164, 220, 0.14)",
  "--info-soft": "rgba(59, 130, 246, 0.12)",
  "--warning-strong": "#f97316",
  "--success-rgb": "22,185,129",
  "--warning-rgb": "245,158,11",
  "--error-rgb": "239,68,68",
  "--info-rgb": "59,130,246",
  "--border-muted": "var(--border-default)",
};

function withBaseVars(overrides: Record<string, string>): Record<string, string> {
  return {
    ...GRAY_VARS,
    ...STATUS_VARS,
    ...COMMON_VARS,
    ...overrides,
  };
}

const THEMES: Record<ThemeName, Theme> = {
  "inventory-dark": {
    name: "inventory-dark",
    displayName: "Bilans Stanja — Tamna",
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
      "--card-shadow": "rgba(0,0,0,0.9)",
    }),
  },
  "soft-gray": {
    name: "soft-gray",
    displayName: "Meka siva",
    description: "Nježna siva paleta za niskokontrastne prikaze",
    cssVars: withBaseVars({
      "--surface-default": "#f5f6f8",
      "--surface-light": "#ffffff",
      "--surface-elevated": "#ffffff",
      "--surface-darker": "#e9ecef",
      "--text-primary": "#0f172a",
      "--text-secondary": "#475569",
      "--text-muted": "#64748b",
      "--border-default": "#e6e9ee",
      "--border-hover": "#d0d6de",
      "--focus-ring": "#94a3b8",
      "--card-shadow": "rgba(16,24,40,0.04)",
      "--accent-primary": "#6b7280",
      "--success": "#10b981",
      "--error": "#ef4444",
      "--warning": "#f59e0b",
      "--info": "#3b82f6",
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
      "--card-shadow": "rgba(16,24,40,0.06)",
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
      "--card-shadow": "rgba(0,0,0,0.9)",
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
