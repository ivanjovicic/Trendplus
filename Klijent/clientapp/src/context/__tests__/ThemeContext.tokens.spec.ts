import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { createElement, useEffect } from "react";
import { render } from "@testing-library/react";
import { ThemeProvider, useTheme, type Theme, type ThemeName } from "../ThemeContext";
import { THEME_TOKENS } from "../../styles/themeTokens";

const themesCss = readFileSync(resolve(process.cwd(), "src/styles/themes.css"), "utf8");

const THEME_NAMES: ThemeName[] = ["inventory-dark", "soft-gray", "light", "neon-light", "neon-dark", "high-contrast"];
const STATUS_KINDS = ["success", "warning", "error", "info"] as const;
const SHARED_COLOR_TOKENS = [
  "--surface-default",
  "--surface-light",
  "--surface-elevated",
  "--surface-darker",
  "--surface-elevated-light",
  "--surface-elevated-dark",
  "--text-primary",
  "--text-secondary",
  "--text-muted",
  "--muted",
  "--border-default",
  "--border-hover",
  "--focus-ring",
  "--accent-primary",
  "--accent-text",
  "--success",
  "--warning",
  "--error",
  "--info",
  "--error-text",
  "--warning-text",
  "--text-on-primary",
  "--success-soft",
  "--warning-soft",
  "--error-soft",
  "--info-soft",
  "--overlay",
  "--overlay-strong",
  "--modal-overlay",
  "--surface-inverse",
  "--surface-inverse-outline",
  "--icon-stroke",
  "--text-on-dark",
  "--link",
  "--surface-muted",
  "--tooltip-box-shadow",
  "--box-shadow-xs",
  "--box-shadow-sm",
  "--box-shadow-md",
  "--modal-box-shadow",
  "--toast-box-shadow",
  ...STATUS_KINDS.flatMap((status) => [
    `--status-${status}-fill`,
    `--status-${status}-border`,
    `--status-${status}-text`,
  ]),
  ...Array.from({ length: 8 }, (_, index) => `--chart-series-${index + 1}`),
  "--chart-axis",
  "--chart-grid",
  "--chart-tooltip-bg",
  "--chart-tooltip-text",
  "--chart-positive",
  "--chart-negative",
  "--analytics-value-glow-color",
  "--panel",
  "--panel-border",
  "--row-hover",
];

function captureThemes(): Record<ThemeName, Theme> {
  let result: Record<ThemeName, Theme> | null = null;

  function ThemeProbe() {
    const { themes } = useTheme();
    useEffect(() => {
      result = themes;
    }, [themes]);
    return null;
  }

  const view = render(createElement(ThemeProvider, { defaultTheme: "light" }, createElement(ThemeProbe)));
  view.unmount();
  if (!result) throw new Error("ThemeProvider did not expose its canonical theme values");
  return result;
}

function declarationsForTheme(theme: ThemeName): Record<string, string> {
  const declarations: Record<string, string> = {};
  const uncommentedCss = themesCss.replace(/\/\*[\s\S]*?\*\//g, "");
  const rules = uncommentedCss.matchAll(/([^{}]+)\{([^{}]*)\}/g);
  const matchedRules = [...rules];

  for (const [, selectorText, body] of matchedRules) {
    const selectors = selectorText.split(",").map((selector) => selector.trim());
    const applies = selectors.some((selector) =>
      selector === ":root" || selector === `[data-theme="${theme}"]`,
    );
    if (!applies) continue;

    for (const [, property, value] of body.matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)) {
      declarations[property] = value.trim();
    }
  }

  return declarations;
}

function resolveColor(theme: ThemeName, property: string, themes: Record<ThemeName, Theme>): string {
  const vars = themes[theme].cssVars;
  let value = vars[property];
  for (let depth = 0; depth < 4; depth += 1) {
    const alias = value?.match(/^var\((--[\w-]+)\)$/)?.[1];
    if (!alias || !vars[alias]) break;
    value = vars[alias];
  }

  const color = value?.match(/#([0-9a-f]{6})\b/i)?.[0];
  if (!color) throw new Error(`${theme} ${property} is not a six-digit hex color: ${value}`);
  return color;
}

function luminance(color: string): number {
  const channels = color.match(/[0-9a-f]{2}/gi)!.map((channel) => parseInt(channel, 16) / 255);
  const [red, green, blue] = channels.map((channel) =>
    channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4,
  );
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

function contrastRatio(first: string, second: string): number {
  const [lighter, darker] = [luminance(first), luminance(second)].sort((a, b) => b - a);
  return (lighter + 0.05) / (darker + 0.05);
}

describe("theme semantic tokens", () => {
  it("keeps the no-JS CSS fallback in parity with the canonical theme values", () => {
    expect(THEME_NAMES).toHaveLength(6);
    const themes = captureThemes();

    for (const theme of THEME_NAMES) {
      const fallback = declarationsForTheme(theme);
      for (const token of SHARED_COLOR_TOKENS) {
        expect(fallback[token], `${theme} ${token}`).toBe(themes[theme].cssVars[token]);
      }
    }
  });

  it("keeps text, status and chart contrast above their design-system floors in every theme", () => {
    const themes = captureThemes();
    for (const theme of THEME_NAMES) {
      const elevated = resolveColor(theme, "--surface-elevated", themes);
      const defaultSurface = resolveColor(theme, "--surface-default", themes);

      for (const text of ["--text-primary", "--text-secondary", "--text-muted", "--muted"]) {
        expect(contrastRatio(resolveColor(theme, text, themes), defaultSurface), `${theme} ${text} on default`).toBeGreaterThanOrEqual(4.5);
        expect(contrastRatio(resolveColor(theme, text, themes), elevated), `${theme} ${text} on elevated`).toBeGreaterThanOrEqual(4.5);
      }

      for (const status of STATUS_KINDS) {
        const text = resolveColor(theme, `--status-${status}-text`, themes);
        expect(contrastRatio(text, resolveColor(theme, `--status-${status}-fill`, themes)), `${theme} ${status} on fill`).toBeGreaterThanOrEqual(4.5);
        expect(contrastRatio(text, elevated), `${theme} ${status} on elevated`).toBeGreaterThanOrEqual(4.5);
      }

      // Base status colours are used directly as text (text-success, .trend-up, ...).
      for (const status of STATUS_KINDS) {
        const base = resolveColor(theme, `--${status}`, themes);
        expect(contrastRatio(base, defaultSurface), `${theme} --${status} text on default`).toBeGreaterThanOrEqual(4.5);
        expect(contrastRatio(base, elevated), `${theme} --${status} text on elevated`).toBeGreaterThanOrEqual(4.5);
      }

      expect(contrastRatio(resolveColor(theme, "--chart-axis", themes), elevated), `${theme} chart axis`).toBeGreaterThanOrEqual(3);
      expect(contrastRatio(resolveColor(theme, "--chart-grid", themes), elevated), `${theme} chart grid`).toBeGreaterThanOrEqual(3);
      expect(contrastRatio(resolveColor(theme, "--chart-tooltip-text", themes), resolveColor(theme, "--chart-tooltip-bg", themes)), `${theme} chart tooltip`).toBeGreaterThanOrEqual(4.5);
      expect(contrastRatio(resolveColor(theme, "--text-on-primary", themes), resolveColor(theme, "--accent-primary", themes)), `${theme} primary action`).toBeGreaterThanOrEqual(4.5);
      if (["soft-gray", "light", "neon-light"].includes(theme)) {
        expect(themes[theme].cssVars["--warning-soft"]).toBe("var(--status-warning-fill)");
      }
    }
  });

  it("keeps on-primary text readable on solid status fills (bg-info, bg-success, bg-error + text-on-primary)", () => {
    const themes = captureThemes();
    // inventory-dark is the legacy palette: its bright status fills need dark text but its
    // primary action needs white text, so one on-fill colour cannot serve both. Tracked in
    // docs/qa/UI_THEME_AUDIT_2026-10-06.md as future polish.
    for (const theme of THEME_NAMES.filter((name) => name !== "inventory-dark")) {
      const onPrimary = resolveColor(theme, "--text-on-primary", themes);
      for (const status of ["success", "error", "info"] as const) {
        expect(contrastRatio(onPrimary, resolveColor(theme, `--${status}`, themes)), `${theme} on-primary on --${status}`).toBeGreaterThanOrEqual(4.5);
      }
    }
  });

  it("keeps the analytics value glow off in light and high-contrast palettes", () => {
    const themes = captureThemes();
    for (const theme of ["soft-gray", "light", "neon-light", "high-contrast"] as const) {
      expect(themes[theme].cssVars["--analytics-value-glow-color"], theme).toBe("transparent");
    }
  });

  it("keeps shared theme tokens on defined semantic variables", () => {
    const references = [
      ...Object.values(THEME_TOKENS.gray),
      ...Object.values(THEME_TOKENS.surface),
      ...Object.values(THEME_TOKENS.text),
      THEME_TOKENS.focus,
    ];

    const themes = captureThemes();
    for (const reference of references) {
      const variable = reference.match(/^var\((--[\w-]+)\)$/)?.[1];
      expect(variable, reference).toBeTruthy();
      for (const theme of THEME_NAMES) {
        expect(themes[theme].cssVars[variable!], `${theme} defines ${reference}`).toBeTruthy();
      }
    }
  });

  it("keeps existing dark soft-status surfaces while using theme fills in light palettes", () => {
    const themes = captureThemes();
    for (const theme of ["soft-gray", "light", "neon-light"] as const) {
      expect(themes[theme].cssVars["--warning-soft"]).toBe("var(--status-warning-fill)");
    }

    expect(themes["inventory-dark"].cssVars["--warning-soft"]).toBe(
      "var(--theme-color-rgba-245-158-11-0p15, rgba(245, 158, 11, 0.15))",
    );
    expect(themes["neon-dark"].cssVars["--warning-soft"]).toBe(
      "var(--theme-color-rgba-250-204-21-0p13, rgba(250, 204, 21, 0.13))",
    );
  });
});
