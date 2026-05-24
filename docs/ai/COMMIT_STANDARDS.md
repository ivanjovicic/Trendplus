# Trendplus Commit Standards

## Format

```text
type(scope): concrete change
```

Tipovi:
- fix
- feat
- test
- docs
- refactor
- chore

Scope:
- analytics
- scorecard
- supplier
- product-decision
- inventory
- reports
- data-quality
- workers
- ui
- api
- migrations

---

## Dobro

```text
fix(scorecard): prevent 30d fallback to wider supplier dataset
fix(analytics): prevent fake-zero dashboard state
fix(analytics-ui): repair Serbian encoding and formatter guardrails
fix(workers): show configured-but-not-running refresh state
feat(reports): add supplier decision print report
test(analytics): add scorecard fallback regression tests
docs(ai): add Copilot and Codex analytics standards
```

## Loše

```text
analytics fix
chore: commit all
commit changes
update
final
```

---

## Kada podeliti commit

Podeli ako menjaš više od jedne odgovornosti.

Primer:
1. `fix(api): add meta contract to inventory endpoints`
2. `fix(inventory): show empty/error states for stock decisions`
3. `test(analytics): cover inventory missing MV error`

---

## Commit body za veće izmene

```text
fix(scorecard): enforce supplier period semantics

- Prevent explicit 30d/90d requests from silently falling back
- Add requested/effective dataset metadata
- Gate final recommendation when fallback is used
- Show fallback warning in SupplierDecisionHubPage
- Add regression tests

Checks:
- dotnet test
- npm run check:analytics-guardrails
- npm run build
```

---

## Obavezno navesti ako važi

Period/fallback:
- requested/effective period
- fallback behavior
- recommendationAllowed

Worker:
- web vs worker behavior
- last success/failure
- manual run

UI/encoding:
- UTF-8/dijakritika
- guardrails
- responsive/theme

Report:
- print/export behavior
- fallback/warning
- graceful failure

Backend semantics:
- no fake zero
- meta contract
- tests
