# 2026-10-08 — owner-delegated RQ481/RQ319/RQ320 decision and queue recovery

Base: `ea31b2b05e04d56c9d4e7a4690595c254ce2bef9` (fresh main ref and tree inspected before preparing this change).
Scope: product-contract decisions and queue status only. No runtime, SQL or production data mutations.
Authority: user asked the assistant to decide what is needed to unblock Trendplus.

- Inspected full canonical RQ queue (including RQ481/RQ319/RQ320/RQ140), active Decision Pulse page/client/backend, design system §6.3 and business roadmap.
- Main RQ140 currently IN_PROGRESS (owner Codex), with shared Shoe Type/Pre-Post scope; RQ319 kept WAITING for path release. RQ320 stays WAITING behind RQ319.
- Main RQ481 WAITING on only the product choice; chose shared applied period/dataScope with provenance and no silent source broadening. RQ480/RQ482 are DONE; no open PR lists Pulse path ownership. Promoted RQ481 to READY, not DONE.
- Open PRs examined before decision: #112 analytics SQL; #116 queue idle evidence; #102/#103 historical P-UI docs. PR #116 Zero-READY proof was valid only for its older base and must not be treated as the post-decision routing state.
- STAB16/RQ592, journal authority, scoring policy and tenant identity remain evidence/authority gated.
- Documentation-only edit: SQL/backend/frontend behavior is **not yet implemented**; don't claim tests of RQ481 have passed.
- Required follow-up: claim RQ481, implement bounded contract and execute frontend/backend focused tests; after RQ140 handoff, run RQ319/RQ320 separately.

Static validation planned on the resulting diff: exactly one changed canonical status (RQ481 WAITING -> READY), RQ319/RQ320 remain WAITING; status headings unique, preserved historical lines.
