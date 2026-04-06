import React, { createContext, useContext, useEffect, useState } from "react";

export type ThemeName = "inventory-dark" | "light" | "high-contrast" | "soft-gray";

export interface Theme {
  name: ThemeName;
  displayName: string;
  description: string;
  cssVars: Record<string, string>;
}

const GRAY_VARS: Record<string, string> = {
  "--gray-50": "var(--theme-color-f8fafb, #f8fafb)",
  "--gray-100": "var(--theme-color-f1f5f9, #f1f5f9)",
  "--gray-200": "var(--theme-color-e2e8f0, #e2e8f0)",
  "--gray-300": "var(--theme-color-cbd5e1, #cbd5e1)",
  "--gray-400": "var(--theme-color-94a3b8, #94a3b8)",
  "--gray-500": "var(--theme-color-64748b, #64748b)",
  "--gray-600": "var(--theme-color-475569, #475569)",
  "--gray-700": "var(--theme-color-334155, #334155)",
  "--gray-800": "var(--theme-color-1f2933, #1f2933)",
  "--gray-900": "var(--theme-color-0b1220, #0b1220)",
};

const STATUS_VARS: Record<string, string> = {
  "--success": "var(--theme-color-10b981, #10b981)",
  "--error": "var(--theme-color-ef4444, #ef4444)",
  "--warning": "var(--theme-color-f59e0b, #f59e0b)",
  "--info": "var(--theme-color-3b82f6, #3b82f6)",
};

const COMMON_VARS: Record<string, string> = {
  "--text-on-primary": "var(--theme-color-ffffff, #ffffff)",
  "--text-on-success": "var(--theme-color-0f5132, #0f5132)",
  "--text-on-error": "var(--theme-color-ffffff, #ffffff)",
  "--text-on-warning": "var(--theme-color-713f12, #713f12)",
  "--text-on-info": "var(--theme-color-1e3a8a, #1e3a8a)",
  "--text-on-surface": "var(--text-primary)",
  "--focus-ring-shadow": "var(--theme-color-rgba-37-99-235-0p08, rgba(37, 99, 235, 0.08))",
  "--surface-elevated-dark": "var(--theme-color-0f1116, #0f1116)",
  "--surface-elevated-light": "var(--theme-color-1f2430, #1f2430)",
  "--surface-border-strong": "var(--theme-color-1f2733, #1f2733)",
  "--surface-card": "var(--surface-elevated)",
  "--card-shadow": "var(--theme-color-rgba-0-0-0-0p9, rgba(0,0,0,0.9))",
  "--muted": "var(--theme-color-9ca3af, #9ca3af)",
  "--accent-primary": "var(--theme-color-2563eb, #2563eb)",
  "--success-soft": "var(--theme-color-rgba-16-185-129-0p15, rgba(16, 185, 129, 0.15))",
  "--warning-soft": "var(--theme-color-rgba-245-158-11-0p15, rgba(245, 158, 11, 0.15))",
  "--error-soft": "var(--theme-color-rgba-239-68-68-0p15, rgba(239, 68, 68, 0.15))",
  "--neutral-soft": "var(--theme-color-rgba-140-164-220-0p14, var(--theme-color-rgba-140-164-220-0p14, rgba(140, 164, 220, 0.14)))",
  "--info-soft": "var(--theme-color-rgba-59-130-246-0p12, rgba(59, 130, 246, 0.12))",
  "--warning-strong": "var(--theme-color-f97316, #f97316)",
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
      "--surface-default": "var(--theme-color-0f1318, #0f1318)",
      "--surface-light": "var(--theme-color-10141c, #10141c)",
      "--surface-elevated": "var(--theme-color-12161f, #12161f)",
      "--surface-darker": "var(--theme-color-0a0d14, #0a0d14)",
      "--text-primary": "var(--theme-color-dbe6fb, #dbe6fb)",
      "--text-secondary": "var(--theme-color-9aa9c6, #9aa9c6)",
      "--text-muted": "var(--theme-color-73809a, #73809a)",
      "--border-default": "var(--theme-color-283042, #283042)",
      "--border-hover": "var(--theme-color-344a66, #344a66)",
      "--focus-ring": "var(--theme-color-44d0ff, #44d0ff)",
      "--card-shadow": "var(--theme-color-rgba-0-0-0-0p9, rgba(0,0,0,0.9))",
    }),
  },
  "soft-gray": {
    name: "soft-gray",
    displayName: "Meka siva",
    description: "Nježna siva paleta za niskokontrastne prikaze",
    cssVars: withBaseVars({
      "--surface-default": "var(--theme-color-f5f6f8, #f5f6f8)",
      "--surface-light": "var(--theme-color-ffffff, #ffffff)",
      "--surface-elevated": "var(--theme-color-ffffff, #ffffff)",
      "--surface-darker": "var(--theme-color-e9ecef, #e9ecef)",
      "--text-primary": "var(--theme-color-0f172a, #0f172a)",
      "--text-secondary": "var(--theme-color-475569, #475569)",
      "--text-muted": "var(--theme-color-64748b, #64748b)",
      "--border-default": "var(--theme-color-e6e9ee, #e6e9ee)",
      "--border-hover": "var(--theme-color-d0d6de, #d0d6de)",
      "--focus-ring": "var(--theme-color-94a3b8, #94a3b8)",
      "--card-shadow": "var(--theme-color-rgba-16-24-40-0p04, rgba(16,24,40,0.04))",
      "--accent-primary": "var(--theme-color-6b7280, #6b7280)",
      "--success": "var(--theme-color-10b981, #10b981)",
      "--error": "var(--theme-color-ef4444, #ef4444)",
      "--warning": "var(--theme-color-f59e0b, #f59e0b)",
      "--info": "var(--theme-color-3b82f6, #3b82f6)",
    }),
  },
  light: {
    name: "light",
    displayName: "Svetla",
    description: "Klasicna svetla tema za dnevno koriscenje",
    cssVars: withBaseVars({
      "--surface-default": "var(--theme-color-f4f7fb, #f4f7fb)",
      "--surface-light": "var(--theme-color-ffffff, #ffffff)",
      "--surface-elevated": "var(--theme-color-ffffff, #ffffff)",
      "--surface-darker": "var(--theme-color-e6edf7, #e6edf7)",
      "--text-primary": "var(--theme-color-0f172a, #0f172a)",
      "--text-secondary": "var(--theme-color-334155, #334155)",
      "--text-muted": "var(--theme-color-64748b, #64748b)",
      "--border-default": "var(--theme-color-d3dce9, #d3dce9)",
      "--border-hover": "var(--theme-color-a7b9d3, #a7b9d3)",
      "--focus-ring": "var(--theme-color-2563eb, #2563eb)",
      "--card-shadow": "var(--theme-color-rgba-16-24-40-0p06, rgba(16,24,40,0.06))",
    }),
  },
  "high-contrast": {
    name: "high-contrast",
    displayName: "Visoki kontrast",
    description: "Maksimalni kontrast za bolje citanje",
    cssVars: withBaseVars({
      "--surface-default": "var(--theme-color-000000, #000000)",
      "--surface-light": "var(--theme-color-090909, #090909)",
      "--surface-elevated": "var(--theme-color-111111, #111111)",
      "--surface-darker": "var(--theme-color-000000, #000000)",
      "--text-primary": "var(--theme-color-ffffff, #ffffff)",
      "--text-secondary": "var(--theme-color-f5f5f5, #f5f5f5)",
      "--text-muted": "var(--theme-color-d4d4d4, #d4d4d4)",
      "--border-default": "var(--theme-color-ffffff, #ffffff)",
      "--border-hover": "var(--theme-color-ffe066, #ffe066)",
      "--focus-ring": "var(--theme-color-ffe066, #ffe066)",
      "--card-shadow": "var(--theme-color-rgba-0-0-0-0p9, rgba(0,0,0,0.9))",
      "--success": "var(--theme-color-00ff88, #00ff88)",
      "--error": "var(--theme-color-ff5c5c, #ff5c5c)",
      "--warning": "var(--theme-color-ffd166, #ffd166)",
      "--info": "var(--theme-color-5cc8ff, #5cc8ff)",
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
