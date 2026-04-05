## Plan: Add Soft-Gray Theme & Apply Globally

TL;DR: Add a new `soft-gray` theme to the existing theme system by extending `THEMES` in `ThemeContext.tsx`, ensure `data-theme` selectors and CSS fallback blocks match the theme names, expose the theme in the ThemeSettings UI, and replace remaining hardcoded colors/fallbacks with CSS variables so the new theme affects all screens.

**Steps**
1. Update theme registry (*depends on step 2*):
   - Add `soft-gray` to `ThemeName` and `THEMES` in `Klijent/clientapp/src/context/ThemeContext.tsx` with a complete `cssVars` object using the same token names as other themes (surface-*, text-*, border-*, accent-*, status-*, panel-*, focus-ring, card-shadow).
   - Rename the `Bilans Stanja` displayName in the same `THEMES` entry to the requested label.
2. Align CSS fallback blocks (*parallel-safe*):
   - Fix selector inconsistencies in `Klijent/clientapp/src/styles/themes.css` and `Klijent/clientapp/src/skeleton.css` so `[data-theme\"<themeName>\"]` values exactly match the `name` strings in `THEMES` (e.g., change `[data-theme\"dark\"]` → `[data-theme\"inventory-dark\"]` or add a `[data-theme\"soft-gray\"]` block). Optionally add a static CSS block for `soft-gray` as a fallback if JS is disabled.
3. Tailwind integration (*depends on step 1*):
   - Ensure `Klijent/clientapp/tailwind.config.js` maps colors to CSS vars where possible (e.g., `primary: 'var(--primary)'`). If Tailwind requires static palettes for utilities, add a `softGray` static palette mirroring the `soft-gray` token choices.
4. Replace page-level hardcoded fallbacks (*parallelizable per-file*):
   - Replace `var(--x, #fallback)` usages and direct `#hex`/`rgba(...)` occurrences in page CSS files (e.g., `src/pages/ColorSalesStatsPage.css`, `ShoeTypeSalesStatsPage.css`, `ProdajaPrePostNivelacijePage.css`, `AnalyticsDashboard.css`) with `var(--token)` tokens defined in themes.
   - Replace RGBA shadows/overlays in `src/styles/interactionTokens.ts` with `var(--interactive-*)` tokens.
5. ThemeSettings & UI integration (*depends on step 1*):
   - Confirm `Klijent/clientapp/src/pages/ThemeSettingsPage.tsx` automatically lists the new theme (it reads `THEMES`). Update any static description text that references the old display name.
6. Validation & QA:
   - Run `npm run build` / `npm run dev` and `tsc --noEmit` to ensure no TypeScript/JSX regressions.
   - Visual QA: open ThemeSettingsPage and at least the main analytics pages and confirm `light`, `inventory-dark`, `soft-gray`, and `high-contrast` apply as expected.
   - Automated grep: run a repo search for hex/rgb patterns (`#([0-9a-fA-F]{3,6})` and `rgba\(`) to confirm remaining occurrences; iterate until acceptable coverage.
7. Documentation & fallback guidance:
   - Update README or STARTUP_GUIDE with the new theme name and usage notes (how to add additional themes).

**Relevant files**
- `Klijent/clientapp/src/context/ThemeContext.tsx` — add `soft-gray` to `THEMES`, rename displayName, and ensure `withBaseVars()` covers new tokens.
- `Klijent/clientapp/src/styles/themes.css` — fix `[data-theme]` selectors, optionally add a `soft-gray` block for no-JS fallback.
- `Klijent/clientapp/src/skeleton.css` — add or align `[data-theme\"soft-gray\"]` block for CSS-only fallbacks.
- `Klijent/clientapp/tailwind.config.js` — map Tailwind colors to CSS vars or add `softGray` static palette.
- `Klijent/clientapp/src/pages/ThemeSettingsPage.tsx` — adjust static text if necessary.
- Page CSS files to scrub: `Klijent/clientapp/src/pages/*.css` (search & replace list will be generated during implementation).
- `Klijent/clientapp/src/styles/interactionTokens.ts` — move rgba literals into CSS vars.

**Verification**
1. Run `npm ci` then `npm run build` and `tsc --noEmit` in `Klijent/clientapp` to catch compile errors.
2. Open ThemeSettingsPage and verify the new `Soft Gray` appears and can be selected; inspect `document.documentElement.getAttribute('data-theme')` and computed styles to confirm CSS vars are applied.
3. Visual check: confirm UI elements (buttons, badges, focus rings, panels) respect the `soft-gray` tokens across the main pages.
4. Run `git grep -n "#\\|rgba(" -- Klijent/clientapp | wc -l` and reduce to an acceptable number of occurrences (goal: only build artifacts or unavoidable inline images remain).

**Decisions & assumptions**
- JS-driven theme application (ThemeContext writing `cssVars`) is primary. Add a static `[data-theme\"soft-gray\"]` CSS block for no-JS fallback, but keep single source-of-truth in `THEMES`.
- Prefer CSS vars in `tailwind.config.js` where possible; if JIT requires static hexes for some utilities, include a `softGray` static palette.
- Keep token names identical to existing ones (surface-*, text-*, border-*, accent-*, status-*) to maximize compatibility.

**Further considerations**
1. Accessibility: choose `soft-gray` text/surface tokens to meet WCAG contrast for primary actions; provide alternate stronger tokens if needed.
2. Snapshot tests / storybook: update snapshots where color-driven snapshots may break.
3. Rollout: apply changes incrementally per page to avoid large visual regressions; create a single PR per theme addition + token replacements.

---

If you approve, I can (A) create the exact `soft-gray` `cssVars` snippet and patch `ThemeContext.tsx` and skeleton.css, and (B) apply representative replacements in two sample page CSS files so you can review the pattern before a full sweep. Which would you like me to do?