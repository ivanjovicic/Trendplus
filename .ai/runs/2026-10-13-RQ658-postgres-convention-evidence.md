# RQ658 evidence

- Bug: ReachesPostgresAvailabilityGuard added members to visitedMembers before checking the call, hiding guard helpers.
- Before conversion (fixed convention, local Testing env): 16 violations: AssortmentNivelacijaOracleTests 195/213/314/359/379/417/445/466, SupplierReconciliationPackTests 31/92/130, SupplierScorecardOracleTests 130/353/401/461/499.
- After: convention + 3 classes: 42 total, 24 passed, 18 skipped (PostgreSQL proof not run), 0 failed.
- PR #148 CI: backend run 38449578209 pass (Complete backend analytics suite 8m4s), governance run 38449578180 pass, frontend run 38449578205 pass.
- Merged with --merge: c094c3ceed6609993344e8beaeaa9aa9a15b0d98
