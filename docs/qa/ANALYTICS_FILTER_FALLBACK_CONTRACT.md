# Analytics Filter Fallback Contract

Purpose
-------
Analytics filter endpoints must not let a fallback look like a clean empty list.

Scope
-----
- `GET /api/analytics/cached/filters/suppliers`
- `GET /api/analytics/cached/filters/stores`

Response shape
-------------
- The response body stays an array.
- On fallback paths, the backend emits response headers:
  - `X-Analytics-Fallback: true`
  - `X-Analytics-Fallback-Code`
  - `X-Analytics-Fallback-Reason`
- The frontend attaches that fallback metadata to the returned array as `meta` without breaking existing array consumers.

UI behavior
-----------
- If filter metadata is present, keep the last known good filter list instead of replacing it with a silent empty list.
- Show a short warning near the affected filter control.
- Do not convert the warning into a hard error unless the request itself failed.

Notes
-----
- This contract is intentionally backward-compatible.
- The filter list can still be empty when that is the real data state, but fallback emptiness must stay visible.
