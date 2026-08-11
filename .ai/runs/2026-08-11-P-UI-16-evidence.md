# P-UI-16 evidence log

Prompt: P-UI-16 - Pre-nivelacija priority: no fake reliability + empty/copy polish
Date: 2026-08-11
Status: DONE

Page:
- Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx

Outcome:
- Table uses `reliabilitySignalDisplay`: unavailable → “Nije dostupno” + `signal-na` (never “Nisko”).
- Empty-state copy is SKU-filter oriented (no sales period).
- Diacritic polish on remaining strings; `signal-na` CSS uses theme tokens (deduped).
- Focused tests cover unavailable reliability + empty copy (5/5).

Checks:
- npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx - pass (5/5)

Next READY:
- P-UI-17 (PreNivelacijaPriorityPage chrome modernization)
