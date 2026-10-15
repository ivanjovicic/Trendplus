# RQ674 evidence

- Batch list no-cache fallbacks (timeout, Npgsql, TimeoutException, unexpected) now return 503, Retry-After 5, Serbian error
- cached-rows fallback unchanged (regression test)
- client keeps last rows and shows role=status warning, cleared on success
- import/start/recovery/cancel paths untouched
- PR #165 merged as 07df82c3
