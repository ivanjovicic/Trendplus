# PreNivelacijaPriorityPage defect audit → P-UI-16 / P-UI-17

Date: 2026-08-11
Screen: Prioriteti pre-nivelacije (`PreNivelacijaPriorityPage.tsx`)
Owner program: P-UI (presentation). Filter catalog completeness deferred (needs RQ/backend; not promoted over BCI).

## Defects found (cited)

1. Unavailable reliability rendered as "Nisko" (table ignores `reliabilityAvailable`) — P-UI-16
2. Empty-state copy references sales period on a non-period screen — P-UI-16
3. Serbian diacritics / English toolbar leftovers — P-UI-16
4. Local filter/table chrome (not ControlBar/DataTable) — P-UI-17 WAITING
5. Chart tooltip hardcoded `#ef4444`/`#16a34a` — P-UI-17 WAITING
6. Season/footwear options only from current candidates page — out of P-UI scope (RQ later)

## Queue action

- Wrote `P-UI-16` READY (signal/copy)
- Wrote `P-UI-17` WAITING (chrome; Ready after P-UI-16)
- Updated `MASTER_ROADMAP.md` + premium queue header
- One READY per P-UI program respected
