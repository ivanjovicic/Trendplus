import React, { createContext, useContext, useEffect, useState } from "react";

export type ThemeName = "inventory-dark" | "light" | "high-contrast" | "soft-gray" | "neon-light" | "neon-dark";

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
  "--color-bg": "var(--surface-default)",
  "--color-surface": "var(--surface-elevated)",
  "--color-surface-muted": "var(--surface-darker)",
  "--color-surface-subtle": "var(--surface-light)",
  "--color-text": "var(--text-primary)",
  "--color-muted": "var(--text-muted)",
  "--color-border": "var(--border-default)",
  "--color-primary": "var(--accent-primary)",
  "--color-success": "var(--success)",
  "--color-warning": "var(--warning)",
  "--color-error": "var(--error)",
  "--color-info": "var(--info)",
  "--color-transparent": "transparent",
  "--text-on-primary": "var(--theme-color-ffffff, #ffffff)",
  "--text-on-success": "var(--theme-color-0f5132, #0f5132)",
  "--text-on-error": "var(--theme-color-ffffff, #ffffff)",
  "--text-on-warning": "var(--theme-color-713f12, #713f12)",
  "--text-on-info": "var(--theme-color-1e3a8a, #1e3a8a)",
  "--text-on-surface": "var(--text-primary)",
  "--focus-ring-shadow": "0 0 0 var(--space-1) var(--theme-color-rgba-37-99-235-0p08, rgba(37, 99, 235, 0.08))",
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
  "--accent-secondary": "var(--theme-color-7c3aed, #7c3aed)",
  "--glow-accent": "0 0 0 transparent",
  "--glow-panel": "0 16px 34px -32px var(--card-shadow)",
  "--space-px": "1px",
  "--space-0": "0",
  "--space-1": "0.25rem",
  "--space-2": "0.5rem",
  "--space-3": "0.75rem",
  "--space-4": "1rem",
  "--space-5": "1.25rem",
  "--space-6": "1.5rem",
  "--space-8": "2rem",
  "--space-10": "2.5rem",
  "--space-12": "3rem",
  "--radius-sm": "0.375rem",
  "--radius-md": "0.5rem",
  "--radius-lg": "0.75rem",
  "--radius-xl": "1rem",
  "--radius-pill": "999rem",
  "--border-width-sm": "1px",
  "--font-size-xs": "0.72rem",
  "--font-size-sm": "0.78rem",
  "--font-size-md": "0.86rem",
  "--font-size-base": "0.9rem",
  "--font-size-lg": "0.95rem",
  "--font-size-xl": "1.35rem",
  "--font-size-2xl": "1.55rem",
  "--line-height-tight": "1.1",
  "--line-height-title": "1.25",
  "--font-weight-medium": "650",
  "--font-weight-semibold": "720",
  "--font-weight-bold": "760",
  "--font-weight-heavy": "780",
  "--letter-spacing-label": "0",
  "--size-control": "2.5rem",
  "--size-icon-button": "2.5rem",
  "--size-status-dot": "0.5rem",
  "--size-table-max-height": "40rem",
  "--size-detail-max-height": "16.25rem",
  "--size-chart": "17.5rem",
  "--size-chart-short": "13.75rem",
  "--size-filter-min": "11.25rem",
  "--size-actions-column": "6.75rem",
  "--observability-page-max": "100%",
  "--observability-log-table-min-width": "72rem",
  "--observability-perf-table-min-width": "64rem",
  "--observability-log-col-time": "10%",
  "--observability-log-col-severity": "10%",
  "--observability-log-col-message": "25%",
  "--observability-log-col-path": "25%",
  "--observability-log-col-correlation": "22%",
  "--observability-log-col-actions": "8%",
  "--transition-fast": "160ms ease",
  "--opacity-disabled": "0.55",
  "--opacity-muted": "0.72",
  "--z-sticky": "1",
  "--shadow-panel": "var(--glow-panel)",
  "--shadow-inset-border": "inset 0 0 0 var(--border-width-sm) var(--color-border)",
  "--font-family-mono": "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace",
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
    displayName: "Tamna",
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
  "neon-light": {
    name: "neon-light",
    displayName: "Neon Light",
    description: "Bright analytical theme with restrained neon accents",
    cssVars: withBaseVars({
      "--surface-default": "var(--theme-color-f7fbff, #f7fbff)",
      "--surface-light": "var(--theme-color-ffffff, #ffffff)",
      "--surface-elevated": "var(--theme-color-ffffff, #ffffff)",
      "--surface-darker": "var(--theme-color-eaf4ff, #eaf4ff)",
      "--text-primary": "var(--theme-color-06152f, #06152f)",
      "--text-secondary": "var(--theme-color-31445f, #31445f)",
      "--text-muted": "var(--theme-color-64748b, #64748b)",
      "--border-default": "var(--theme-color-c9e4ff, #c9e4ff)",
      "--border-hover": "var(--theme-color-6ee7ff, #6ee7ff)",
      "--focus-ring": "var(--theme-color-0057d8, #0057d8)",
      "--focus-ring-shadow": "0 0 0 var(--space-1) var(--theme-color-rgba-0-217-255-0p18, rgba(0, 217, 255, 0.18))",
      "--accent-primary": "var(--theme-color-0057d8, #0057d8)",
      "--accent-secondary": "var(--theme-color-9b1fe8, #9b1fe8)",
      "--info": "var(--theme-color-0057d8, #0057d8)",
      "--warning": "var(--theme-color-b45309, #b45309)",
      "--warning-strong": "var(--theme-color-92400e, #92400e)",
      "--error": "var(--theme-color-d11a2a, #d11a2a)",
      "--success": "var(--theme-color-087f5b, #087f5b)",
      "--info-soft": "var(--theme-color-rgba-0-87-216-0p12, rgba(0, 87, 216, 0.12))",
      "--warning-soft": "var(--theme-color-rgba-180-83-9-0p12, rgba(180, 83, 9, 0.12))",
      "--error-soft": "var(--theme-color-rgba-209-26-42-0p1, rgba(209, 26, 42, 0.10))",
      "--success-soft": "var(--theme-color-rgba-8-127-91-0p12, rgba(8, 127, 91, 0.12))",
      "--card-shadow": "var(--theme-color-rgba-0-87-216-0p16, rgba(0, 87, 216, 0.16))",
      "--glow-accent": "0 0 18px var(--theme-color-rgba-0-217-255-0p22, rgba(0, 217, 255, 0.22))",
      "--glow-panel": "0 18px 44px -32px var(--theme-color-rgba-0-87-216-0p34, rgba(0, 87, 216, 0.34))",
      "--text-on-primary": "var(--theme-color-ffffff, #ffffff)",
    }),
  },
  "neon-dark": {
    name: "neon-dark",
    displayName: "Neon Dark",
    description: "Dark analytical theme with cyan and violet signal accents",
    cssVars: withBaseVars({
      "--surface-default": "var(--theme-color-050912, #050912)",
      "--surface-light": "var(--theme-color-0b1220, #0b1220)",
      "--surface-elevated": "var(--theme-color-0f172a, #0f172a)",
      "--surface-darker": "var(--theme-color-020617, #020617)",
      "--text-primary": "var(--theme-color-e8fbff, #e8fbff)",
      "--text-secondary": "var(--theme-color-9ec8d8, #9ec8d8)",
      "--text-muted": "var(--theme-color-6f8da0, #6f8da0)",
      "--border-default": "var(--theme-color-183047, #183047)",
      "--border-hover": "var(--theme-color-00d9ff, #00d9ff)",
      "--focus-ring": "var(--theme-color-00d9ff, #00d9ff)",
      "--focus-ring-shadow": "0 0 0 var(--space-1) var(--theme-color-rgba-0-217-255-0p18, rgba(0, 217, 255, 0.18))",
      "--accent-primary": "var(--theme-color-00d9ff, #00d9ff)",
      "--accent-secondary": "var(--theme-color-b86bff, #b86bff)",
      "--info": "var(--theme-color-5ee7ff, #5ee7ff)",
      "--warning": "var(--theme-color-facc15, #facc15)",
      "--warning-strong": "var(--theme-color-f59e0b, #f59e0b)",
      "--error": "var(--theme-color-ff6b8a, #ff6b8a)",
      "--success": "var(--theme-color-45f3b2, #45f3b2)",
      "--info-soft": "var(--theme-color-rgba-0-217-255-0p13, rgba(0, 217, 255, 0.13))",
      "--warning-soft": "var(--theme-color-rgba-250-204-21-0p13, rgba(250, 204, 21, 0.13))",
      "--error-soft": "var(--theme-color-rgba-255-107-138-0p13, rgba(255, 107, 138, 0.13))",
      "--success-soft": "var(--theme-color-rgba-69-243-178-0p13, rgba(69, 243, 178, 0.13))",
      "--card-shadow": "var(--theme-color-rgba-0-0-0-0p72, rgba(0,0,0,0.72))",
      "--glow-accent": "0 0 18px var(--theme-color-rgba-0-217-255-0p25, rgba(0, 217, 255, 0.25))",
      "--glow-panel": "0 20px 52px -34px var(--theme-color-rgba-0-217-255-0p32, rgba(0, 217, 255, 0.32))",
      "--text-on-primary": "var(--theme-color-03111a, #03111a)",
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

export function ThemeProvider({ children, defaultTheme = "neon-dark" }: ThemeProviderProps) {
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

  const isDark = currentTheme === "inventory-dark" || currentTheme === "high-contrast" || currentTheme === "neon-dark";

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
