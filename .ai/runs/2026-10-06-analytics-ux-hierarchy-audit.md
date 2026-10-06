# 2026-10-06 — Analytics UX hierarchy & readability audit (evidence)

- Base: `origin/main` @ `cc57cace76e6c737e10dc0cb50d5ea5e296885c4` (builds on `docs/qa/UI_THEME_AUDIT_2026-10-06.md`).
- Scope: `Klijent/clientapp` presentation only (TSX markup order, CSS, UI copy). No formulas, SQL, API, aggregations,
  periods, supplier semantics, DUG/KOREKCIJA rules, margin calculations or signal definitions changed.
- Not touched: `AnalyticsTrustHeader` (P-UI-43 IN_PROGRESS), queue entries, theme architecture, rounding.
- Final SHA: `033129888eba6e4fa89b7d734d000eaa02a48867` (rebased onto other agents' commits 2d675b60 and 8637d93d without conflicts; re-validated: vitest 1823/1823, guardrails, typecheck, check:encoding, build, git diff --check) · CI: Analytics Quality Gates run #820 — success (JavaScript SDK pin availability: success; POS UI dependency audit and build: success; Frontend analytics tests, guardrails and build: success). No failures to classify.

## 5-second test (Prodaja po dobavljačima, 1280×900 fixture)

| Question | Before | After |
| --- | --- | --- |
| What matters most? | 7 equal KPI cards, first KPI at y≈1494 | 3 primary cards (promet, maržni doprinos, PoP) + 4 demoted; first KPI y≈976 |
| Good or bad? | Colour-only trend, tones on every card | Neutral PoP card with signed value; tone only where meaningful |
| What changed? | Hidden in table | "Istaknuto u periodu": najveći dobavljač, najveći rast/pad (PoP) with ▲/▼ + share |
| Where is the problem? | Stale-supplier warning as narrow uppercase column, warning text ~2:1 | Full-row sentence-case warning with status-warning tokens |
| Where to click next? | Priority list after charts and inventory panel | "Za proveru: Oprez N · Smanji / Ne veruj N" + "Otvori prioritetnu listu"; list comes before charts |

Residual: the trust header (P-UI-43) and sticky filters still occupy most of the first viewport at 1280×900.

## Per-screen before/after

- **Shared KPI system** (`analytics-system.css`): label quieter (text-secondary, 600, 0.06em) vs value; new
  `analytics-kpi-tier--primary/--secondary`, `analytics-kpi-card--estimate`, `analytics-kpi-tier-heading`, `__caveat`;
  info tip stays next to the label; coverage `<em>` uses text-secondary.
- **Supplier overview**: KPI tiers; highlight strip; order KPI → highlights → quality note → priority list → detail → charts
  (side by side ≥1440px) → inventory buying signal (context, not period result); priority chip counts 1.7:1 → AA;
  info message uses status-info tokens; table chips/status pills on status tokens.
- **Supplier consolidated shell**: duplicate overline/H2 removed visually (H2 kept sr-only), header row compacted,
  context cards + counting basis moved under the content; stale supplier-list warning readable full row.
- **Prodaja po vrsti obuće**: same primary/secondary KPI tiers; priority chips contrast; info message tokens.
- **Prodaja po smenama**: revenue column wider so the amount does not wrap; KPI labels calmer; "canonical Supplier"
  cross-link moved below the shift content with natural Serbian copy. Daily table still collapsed + paginated.
- **Prodaja po boji**: KPI label contrast 4.44/3.68 → AA, calmer tracking.
- **Pre/posle nivelacije**: result KPIs moved before "Ishod sniženja", signed change first; non-strong signal shows
  "Procena, ne potvrđen efekat — kvalitet signala: …"; table change column has explicit sign; amounts don't wrap;
  duplicate visible H2 → sr-only.
- **Pre-nivelacija prioriteti**: measured state tier (Visok prioritet, Zaliha pod rizikom, Kandidati) separated from
  "Procene modela — heurističke, nekalibrisane · nisu potvrđen rezultat" (dashed estimate cards, neutral value colour);
  "(procena)" marker on the Isticanje vs sniženje column; active focus tab 1.7:1 → AA; queue headings on status tokens.
- **Data Quality**: second visible H1 → sr-only H2; filter note/link no longer run together; English copy
  ("Low priority issues", "Health snapshot", DESC/ASC) → Serbian.
- **Operations/trust**: refresh banner collapses to a single quiet line only when fresh and healthy; stale, running,
  error, failed objects and worker warnings keep the full banner. Error state heading is bold/structured.

## Validation

- Vitest (full): 228 files / 1823 tests passed
- `npm run check:analytics-guardrails`: pass (5 pre-existing baseline entries re-pointed to shifted line numbers only; same code, same reasons)
- `npm run typecheck`: pass · `npm run build`: pass · `npm run check:encoding`: pass · `git diff --check`: clean
- ESLint on touched files: base 44 errors / 28 warnings → after 44 errors / 28 warnings (no new findings)
- Browser (puppeteer, fixture mode, 9 routes × 390/1280/1920 × light/dark): root overflow 0/54 before and after; contrast findings below floor 150 → 9 (remaining: ppn "Nizak signal" 4.5 dark, shoe sorted header 4.01, decorative ▸ caret 3.99); first KPI top @1280: supplier 1494→976, pre/post 1565→1128, pre-nivelacija 1285→1122, daily 1065→880, color 1056→961, shoe 1050→935

## Suspicious / left intentionally

- Data Quality trust header reads "Podaci deluju pouzdano" while every data request failed (fixture) — header is P-UI-43.
- Many service error strings lack diacritics (`Greska pri ucitavanju…`, `Neuspesno citanje…`) — service layer, not changed.
- `validateDOMNesting`: InfoTip button inside sortable header button (AnalyticsTableToolbar/table headers).
- Inventory and supplier report had no fixture data; only error-state hierarchy was improved there.
