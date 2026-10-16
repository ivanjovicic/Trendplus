# Windows development storage

On Ivan's Windows development machine, prefer `F:\Trendplus-dev` for disposable development state when the drive is available and has adequate free space. Keep the established source checkout where it is unless a task explicitly requires moving it; use named worktree folders under `F:\Trendplus-dev` for isolated work, and put package caches, temporary files and generated test/build artifacts there when practical.

## Safe use

- Check that `F:` is mounted and has enough free space before starting dependency installation or a large build. Set tool-specific cache/temp locations under `F:\Trendplus-dev` rather than filling the system drive.
- Use a task-specific subfolder, such as `F:\Trendplus-dev\<repo>-<task>`, and record the absolute worktree path in any shared queue reservation and local lock.
- Do not move the user's canonical checkout, production Access databases, real supplier invoices, credentials or other private data to the development area as a side effect of choosing F:.
- Delete only an exact, verified development artifact created for the current task or explicitly identified as disposable. Preserve user data, other task worktrees, and durable run evidence; do not use broad recursive cleanup against a drive or workspace root.
- If `F:` is unavailable or lacks capacity, use a suitable task-specific location only after checking available space and ownership, and record the deviation in the run evidence.

This is a storage preference, not a requirement to duplicate source trees or retain generated dependencies after the task. It does not change repository, queue, production-data or privacy boundaries.
