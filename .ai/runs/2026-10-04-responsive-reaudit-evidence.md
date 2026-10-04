# Evidence — responsive re-audit 2026-10-04

- Agent: Grok Bot (executor), for Ivan Jovicic
- Base: `origin/main` `6a2a23b39bac9be24d09e13620b902c7721daff3` (worktree Trendplus2-grok, detached)
- Target: `https://trendplus.vercel.app` (no auth), 2026-10-04 21:10–21:40 Europe/Belgrade
- Tool: playwright-core 1.47 driving `/usr/bin/google-chrome` headless (Chromium emulation). Profiles: phone 360x780 and 390x844 (`isMobile`, `hasTouch`); tablet 768x1024 and 1024x768 (`hasTouch`); laptop 1280x800. Pass 1: 34 routes, default period, 9s settle. Pass 2: 10 routes, `fromDate=2026-07-07&toDate=2026-08-05` where supported, 15s settle, full-page screenshots. Pass 3: offending-element search at 360.
- Limits: Chromium emulation (not real iOS/iPadOS Safari); deployed Vercel SHA not readable; chart tick legibility not measured; screenshots are kept out of the repo (disk space) and the measurements are reproduced below.
- Report: `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md`. Prompts: `P-UI-39`..`P-UI-46` in `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`.

## Pass 1 — all routes (ov = documentElement.scrollWidth - innerWidth; hdr = header height px; small = interactive targets < 44px in either dimension)

| Route | 360 ov/innerW/hdr/small/fieldPx<16 | 768 ov/hdr/small/fields<16 | 1024 ov/hdr | 1280 ov/hdr |
|---|---|---|---|---|
| / | 0/360/80/5of13/0of0 | 0/64/6of13/0of0 | 0/177 | 0/166 |
| /access-import | 0/360/80/8of17/0of0 | 0/64/17of17/0of0 | 0/177 | 0/166 |
| /admin/configuration | 0/360/80/9of21/0of1 | 0/64/10of21/1of1 | 0/177 | 0/166 |
| /analytics | 0/360/80/18of22/0of3 | 0/64/21of22/3of3 | 0/177 | 0/166 |
| /analytics/actions | 0/360/80/10of23/0of7 | 0/64/21of23/7of7 | 0/177 | 0/166 |
| /analytics/color-sales-stats | 0/360/80/17of21/0of5 | 0/64/21of21/5of5 | 106/177 | 0/166 |
| /analytics/daily-sales | 0/360/80/17of21/0of5 | 0/64/21of21/5of5 | 0/177 | 0/166 |
| /analytics/data-quality | 0/360/80/10of25/0of4 | 0/64/14of25/3of4 | 0/177 | 0/166 |
| /analytics/decision-board | 0/360/80/10of13/0of0 | 0/64/13of13/0of0 | 0/177 | 0/166 |
| /analytics/decision-pulse | 0/360/80/5of7/0of0 | 0/64/7of7/0of0 | 0/177 | 0/166 |
| /analytics/insight-studio | 0/360/80/5of27/0of0 | 0/64/10of27/0of0 | 0/177 | 0/166 |
| /analytics/inventory | 0/360/80/16of20/0of8 | 0/64/20of20/8of8 | 0/177 | 0/166 |
| /analytics/nivelacije-pre-post | 0/360/80/18of23/0of6 | 0/64/23of23/6of6 | 0/177 | 0/166 |
| /analytics/pilot-readiness | 0/360/80/18of20/0of0 | 0/64/20of20/0of0 | 0/177 | 0/166 |
| /analytics/pre-nivelacija-prioriteti | 0/360/80/18of21/0of6 | 0/64/21of21/6of6 | 104/177 | 0/166 |
| /analytics/products | 0/360/80/8of34/0of9 | 0/64/30of34/9of9 | 0/177 | 0/166 |
| /analytics/reports/pilot-intake | 0/433/80/16of20/0of0 | 0/64/20of20/0of0 | 0/177 | 0/166 |
| /analytics/shoe-type-sales-stats | 0/360/80/16of20/0of5 | 0/64/20of20/5of5 | 106/177 | 0/166 |
| /analytics/supplier | 0/360/80/16of21/0of6 | 0/64/18of21/0of6 | 0/177 | 0/166 |
| /analytics/supplier/report | 0/360/80/6of8/0of0 | 0/64/8of8/0of0 | 0/177 | 0/166 |
| /artikli | 0/360/101/12of15/0of5 | 0/64/15of15/5of5 | 0/177 | 0/187 |
| /artikli/lista | 0/360/80/6of67/0of2 | 0/64/7of67/2of2 | 0/177 | 0/166 |
| /dnevnik-promena | 0/360/80/8of64/0of1 | 0/64/60of64/1of1 | 0/177 | 0/166 |
| /dobavljaci | 0/360/80/5of237/0of0 | 0/64/237of237/0of0 | 0/177 | 0/166 |
| /logs | 0/360/80/11of18/0of6 | 0/64/17of18/6of6 | 81/177 | 0/166 |
| /nivelacija | 0/360/80/5of9/0of1 | 0/64/9of9/1of1 | 0/177 | 0/166 |
| /nivelacije | 0/360/80/11of13/0of4 | 0/64/13of13/4of4 | 0/177 | 0/166 |
| /povracaj | 0/360/80/7of9/0of2 | 0/64/9of9/2of2 | 0/177 | 0/166 |
| /prodaja | 0/360/80/5of15/0of5 | 0/64/15of15/5of5 | 0/177 | 0/166 |
| /sezone | 0/360/80/10of12/0of3 | 0/64/12of12/3of3 | 0/177 | 0/166 |
| /tipovi-obuce | 0/360/80/6of8/0of1 | 0/64/8of8/1of1 | 0/177 | 0/166 |
| /transfers | 0/360/80/7of21/0of6 | 0/64/21of21/6of6 | 0/177 | 0/166 |
| /unos | 0/360/80/10of11/0of0 | 0/64/11of11/0of0 | 0/177 | 0/166 |
| /unos-robe | 0/360/80/5of9/0of2 | 0/64/9of9/2of2 | 0/177 | 0/166 |

## Pass 2 — data-loaded key screens

| Route (dated where supported) | 360 innerW | trust header 360 | first KPI 360 | tables 360 (w/cols/scroller/sticky) | 1024 ov |
|---|---|---|---|---|---|
| /analytics/supplier?fromDate=2026-07-07&toDate=2026-08-05 | 360 | 749 | - | - | 0 |
| /analytics/color-sales-stats?fromDate=2026-07-07&toDate=2026-08-05 | 426 | 1036 | - | - | 106 |
| /analytics/daily-sales?fromDate=2026-07-07&toDate=2026-08-05 | 360 | 1139 | 1777 | 1795/21/297/static | 0 |
| /analytics/shoe-type-sales-stats?fromDate=2026-07-07&toDate=2026-08-05 | 433 | 1436 | 2403 | 1730/10/260/static | 111 |
| /analytics/reports/pilot-intake | 433 | 649 | 1711 | 406/3/null/static; 309/3/null/static | 0 |
| /analytics/nivelacije-pre-post?fromDate=2026-07-07&toDate=2026-08-05 | 433 | 928 | - | - | 0 |
| /analytics/products | 360 | 855 | 1402 | - | 0 |
| /analytics/pre-nivelacija-prioriteti | 360 | 1287 | 4056 | 956/10/268/static | 104 |
| /analytics/actions | 360 | 682 | 981 | - | 0 |
| /analytics/inventory | 431 | 1341 | 1912 | 1323/17/284/static | 0 |

Drawer at 360: {"w":320,"h":780,"role":"dialog","focusInside":true,"bodyOverflow":"hidden","links":[44,60,60,60,60,60,40,40,40,60,60,60,60,60,60]}; open after Esc: false

## Pass 3 — elements wider than 360px at 360 (outside scroll containers)

- Color / Shoe Type / Pre-Post: `label.analytics-control-bar__field` 380px (Objekat "Komision (Gospodska 6, N/A) [I…", Sezona "Proleće-leto 2021", Dobavljač "\"Tim Tod\"doo") → `innerWidth` 426/433/433.
- Pilot intake: `section.analytics-trust-header`, `section.analytics-refresh-banner`, `div.pirp-actions`, `section.pilot-intake-card` 417px, driven by unstyled `table.pilot-intake-durable-table` (406–417px, no scroller) → 433.
- Inventory: `div.rounded-[28px]` "Zastarelost i obrt zalihe" / "ABC segmentacija kapitala" 415px, "Rizik i prioriteti" / "Vrednost po dobavljaču" 378px, `div.recharts-wrapper` 336px → 431.
- 1024 offenders: 4th `select` of `.analytics-control-bar__fields` ending at x≈1128–1130 (Color, Shoe Type, Pre-Nivelacija); `/logs` `button.button-big.button-danger` right 1105.

## Second-pass code verification (lines re-read on 6a2a23b3)

`AnalyticsControlBar.css:153-158,160-163,174-183,192-203,211-215,227-230`; `AnalyticsControlBar.tsx:89,139`; `HeaderStatus.tsx:371-375,400-401,406,439,457,544`; `Sidebar.tsx:90,219,231`; `AppLayout.tsx:13,42-44`; `PilotDataQualityIntakeReport.tsx:220-221` (no CSS rule for either class anywhere); `InventoryInsightPanels.tsx:66-67,84,151`; `tailwind.css:604-622` (corrected from :470, which is the modal sheet rule); `styles/forms.css:544,565,578`; `AnalyticsTrustHeader.tsx:292`, `.css:409`; `AnalyticsDataTable.css:125-160`; `GlobalRequestSpinner.tsx:26-29`; `SeasonalImageCarousel.tsx:74-79,88-93` (corrected from 86-90); `ConfigurationPage.css:493`; `responsive_baseline.mjs:8-22`; `ColorSalesStatsPage.tsx:1219`, `ArtikliListPage.tsx:487`, `AnalyticsActionsPage.tsx:1297`.
