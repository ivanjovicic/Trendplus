import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";

const SRC = resolve(process.cwd(), "src");

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) return entry === "__tests__" ? [] : sourceFiles(path);
    if (!/\.(css|ts|tsx)$/.test(entry) || /\.(spec|test)\.tsx?$/.test(entry)) return [];
    return [path];
  });
}

const files = sourceFiles(SRC).map((path) => ({ path, text: readFileSync(path, "utf8") }));

describe("theme token usage", () => {
  it("only reads CSS custom properties that are declared somewhere (or carry a fallback)", () => {
    const declared = new Set<string>();
    for (const { text } of files) {
      for (const match of text.matchAll(/(--[\w-]+)\s*:/g)) declared.add(match[1]);
      for (const match of text.matchAll(/["'`](--[\w-]+)["'`]/g)) declared.add(match[1]);
    }

    const undefinedReads: string[] = [];
    for (const { path, text } of files) {
      for (const match of text.matchAll(/var\(\s*(--[\w-]+)\s*\)/g)) {
        if (!declared.has(match[1])) undefinedReads.push(`${relative(SRC, path)}: ${match[1]}`);
      }
    }

    // An undeclared var() without fallback silently becomes "unset" (transparent
    // background, inherited text), which is how several light-theme bugs hid.
    expect(undefinedReads).toEqual([]);
  });

  it("maps every semantic Tailwind colour used in components onto a theme token", () => {
    const tailwindCss = readFileSync(join(SRC, "tailwind.css"), "utf8");
    const themeBlock = tailwindCss.match(/@theme inline\s*\{([^}]*)\}/)?.[1] ?? "";
    const mapped = new Set([...themeBlock.matchAll(/--color-([\w-]+)\s*:/g)].map((match) => match[1]));

    const used = new Set<string>();
    const pattern =
      /(?:^|[\s"'`:])(?:bg|text|border|ring|divide|from|to|via|fill|stroke|outline)-((?:border|foreground|muted|primary|on-primary|success|warning|error|danger|info|accent)(?:-(?:hover|success|warning|error))?)(?:\/\d+)?(?=[\s"'`]|$)/gm;
    for (const { path, text } of files) {
      if (!path.endsWith(".tsx")) continue;
      for (const match of text.matchAll(pattern)) used.add(match[1]);
    }

    expect(used.size).toBeGreaterThan(0);
    const missing = [...used].filter((name) => !mapped.has(name));
    expect(missing).toEqual([]);
    // Every mapping must point at a runtime token rather than a literal colour.
    for (const match of themeBlock.matchAll(/--color-[\w-]+\s*:\s*([^;]+);/g)) {
      expect(match[1].trim()).toMatch(/^var\(--[\w-]+\)$/);
    }
  });

  it("keeps theme-aware inventory and training surfaces off fixed white utilities", () => {
    const themeAwarePaths = [
      "components/inventory/InventoryInsightPanels.tsx",
      "components/inventory/StoreComparisonPanel.tsx",
      "components/inventory/InventoryPriorityPanels.tsx",
      "components/inventory/SizeCurveVisualization.tsx",
      "components/inventory/DemandForecastPanel.tsx",
      "components/inventory/MailSchedulerPanel.tsx",
      "pages/OpenTrainingPage.tsx",
    ];

    for (const relativePath of themeAwarePaths) {
      const source = readFileSync(join(SRC, relativePath), "utf8");
      expect(source, relativePath).not.toMatch(/\b(?:text|bg)-white\b/);
    }

    const skuDetail = readFileSync(join(SRC, "components/inventory/SKUDetailModal.tsx"), "utf8");
    expect(skuDetail).not.toMatch(/\bbg-white\b(?!\/)/);
  });
});
