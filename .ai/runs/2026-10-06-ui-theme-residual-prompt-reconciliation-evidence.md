# UI/theme residual prompt reconciliation — 2026-10-06

Base: `2d675b60fd9ac5d6943413b06b0d3b9a11b4f549`

## Decision

No new P-UI task was created. The residuals from the UI/theme audit are already substantially owned by existing queue items, so the canonical prompts were tightened instead of duplicating scope.

## Changes

- **P-UI-38 (WAITING):** added a post-theme-audit residual hardening addendum for bounded pseudo-token cleanup/ratchets, `inventory-dark`, Tailwind v4 semantic colour mappings, fixed white/black on theme-aware surfaces, duplicate/fallback hygiene and consumption of the existing theme guards.
- **P-UI-52 (READY):** expanded only the copy portion to a bounded user-facing Serbian diacritics sweep in disjoint frontend files. Machine-readable contracts are explicitly excluded; `check:encoding` is required.
- **P-UI-53 (WAITING):** clarified that P-UI-47/P-UI-31/P-UI-35/P-UI-36 are satisfied, but P-UI-44 remains an intentional serialization gate because it overlaps Daily Sales/Inventory page/table paths. Fresh review found no P-UI-53 branch or open PR.
- **P-UI-43:** left unchanged because it is currently IN_PROGRESS and owns AnalyticsTrustHeader.
- Synchronized `MASTER_ROADMAP.md` and `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md`.

## Routing outcome

- P-UI-43: IN_PROGRESS
- P-UI-44: READY
- P-UI-52: READY
- P-UI-53: WAITING behind P-UI-44 overlap/release
- P-UI-38: WAITING as the final responsive/theme/a11y gate

No implementation/business-logic code changed.
