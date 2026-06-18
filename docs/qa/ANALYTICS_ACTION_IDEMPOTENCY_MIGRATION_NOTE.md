# Analytics Action Idempotency Migration Note

The analytics action queue already has a filtered unique index on
`(SourceType, SourceKey)` for open statuses:

- `new`
- `accepted`
- `deferred`

That means no new EF migration was required for this task.

If a target database predates that index or still contains duplicate open
actions from an older deployment, the index creation will fail until the
duplicate open rows are cleaned up or merged.

Recommended rollout order:

1. Identify duplicate open actions for the same `SourceType` / `SourceKey`.
2. Keep the newest or canonical row per pair.
3. Re-run the migration or schema apply step.
