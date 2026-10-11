Task: RQ608/RQ609/RQ610 source review; bounded RQ610 re-entry
Date: 2026-10-11
Origin/main at review: b99884a7917021194e773cac022aa00ca58fa2d2
Scope: source/evidence/queue only; no business database access, import, seed, migration, deploy or code worker mutation.

Verifications:
- Exact RQ608 implementation 35aabd582e754bb293337eace0a6b45aeee58026, GitHub Actions 38116562952: backend + RQ453 both success.
- Exact RQ609 implementation d85fe70a1be4c1e52b8035cdf7549925005322a5, GitHub Actions 38118021843: backend + RQ453 both success.
- Exact RQ610 company-grain implementation 34a5ecf2cdc5f3702b259303952873ac67974366, GitHub Actions 38119404029: backend + RQ453 both success; Planning Governance 38119403974 and 38119571326 success.
- RQ610 was blocked as ProductsDim omits StoreId and SourceBatchId. This is correct for ProductsDim, but code offers another candidate: operational Artikli has IDObjekat, Kolicina, UpdatedAt, SourceTableKey, SourceRowId, SourceHash, SourceUpdatedAtUtc, SourceBatchId; AccessImportService maps store alias and records import lineage; TransferService resolves destination article by store + PLU/name. Candidate != proof: field could be null/default/assignment-only, so do not certify stock before real data reconciliation and mutation-path tests.
- Existing RQ610 owner explicitly handed off to unassigned, so owner-authorized re-entry into source qualification is permitted. RQ611 remains primary READY, RQ612 independently READY.

Changes:
- Added read-only tools/rq610-store-stock-source-qualification.sql.
- Changed RQ610 BLOCKED -> READY ONLY for bounded source proof and conditional code follow-through with collision check.
- Preserved historical blocked note; do not claim store-grain complete, as-of history, batch generation, or production enablement.
- Updated master roadmap to align RQ611 primary / RQ612 and RQ610 independent.

Validation:
- Static reviewed current repository source fields and workflow outcomes via GitHub.
- No local SQL/database/runtime execution possible here; the newly added read-only source probe must be executed by next authorized agent against an actual, verified connection. No new release or business data impact.
