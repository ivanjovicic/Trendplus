# Trendplus AI Standards README

## Fajlovi

Preporučena struktura:

```text
.github/copilot-instructions.md
AGENTS.md
docs/ai/ANALYTICS_STANDARDS.md
docs/ai/COMMON_FAILURES_AND_FIXES.md
docs/ai/CODEX_TASK_CHECKLIST.md
docs/ai/PROMPT_TEMPLATES.md
docs/ai/COMMIT_STANDARDS.md
docs/ai/BACKEND_STANDARDS.md
docs/ai/FRONTEND_UX_STANDARDS.md
docs/ai/AI_WORKFLOW_AND_TOKEN_BUDGET.md
docs/analytics/ANALYTICS_PRODUCTION_READINESS_CHECKLIST.md
```

## Ko čita šta

GitHub Copilot:
```text
.github/copilot-instructions.md
```

Codex/agents:
```text
AGENTS.md
docs/ai/*
```

## Najkraći prompt za buduće taskove

```text
Pre izmene pročitaj .github/copilot-instructions.md i AGENTS.md.
Poštuj Trendplus analytics standarde:
- no fake zero
- backend source of truth
- shared formatteri
- TrustHeader/ErrorState/EmptyState
- UTF-8 bez mojibake
- theme tokens
- worker/refresh transparentnost
- mali scope i stop rules
Na kraju pokreni relevantne build/check/test komande.
```

## Kada dodavati nove standarde

Dodaj standard kada se greška ponovi bar drugi put:
- period fallback
- encoding
- formatter drift
- missing MV
- fake zero
- worker status
- report export failure
- API URL deployment bug

Ne zatrpavati standarde pravilima koja nemaju realnu vrednost za ovaj repo.

## Production Readiness (Analytics)

Pre merge-a za analytics promene obavezno koristi pre-merge checklist:

- `docs/Analytics/ANALYTICS_PRODUCTION_READINESS_CHECKLIST.md`
