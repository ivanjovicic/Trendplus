# Branch consolidation evidence

- Task ID: `direct-branch-consolidation-20260915`
- Date: `2026-09-15`
- Agent/tool: `Codex`
- Queue: `direct-user-request`
- Target: local branch/worktree cleanup with safe code delivery to `main`
- Main before consolidation: `689c5531ed107091d344b78be6d3b60a7727df1d` (`HEAD` = `origin/main`)

## Decision

Reviewed all local branch tips not contained in current `main`, plus all linked worktrees.

- No additional code was safe to merge:
  - the RQ96 staged foundation is already present on `main`; its remaining delta removes startup-lock and migration-failure protection;
  - the cache-invalidation branch is superseded by newer `main` invalidation coverage;
  - the product-action resilience branch is already represented by later `main` commits;
  - route/document branches are stale, duplicate, or conflict with newer canonical files;
  - broad backup/perf branches contain unrelated code, stale snapshots or build artifacts.
- The Daily Sales no-op scope-event guard was already delivered before this consolidation as `6a13f2c5`.

## Worktree safety boundary

Preserve without modification:

- `Trendplus2-mainlatest`: dirty, with an active `STAB14` lock;
- `Trendplus2-obs10`: dirty detached worktree with mixed OBS10/RL10/RQ96 staged work;
- `Trendplus2-rl10`: dirty detached worktree with mixed RL10/RQ96 staged work;
- `Trendplus2-rq96`: dirty detached worktree with RQ96 staged work.

These are not deleted or committed because the changes are incomplete/mixed and ownership is not cleared.

Clean stale worktrees approved for removal after status verification:

- `Trendplus2-gov` (`queue-guidance-main`, behind current main, no local changes);
- `Trendplus2-maincheck` (detached, no local changes);
- `Trendplus2-rq96-land` (`cursor/rq96-inventory-snapshot`, no local changes; alternate RQ96 snapshot already superseded on main).

## Branch cleanup plan

Delete local stale/duplicate branch refs after preserving their remote refs and recording their tips. Keep only branch refs needed to identify dirty worktrees or active work; never force-push or delete remote branches in this pass.

## Validation plan

- verify clean worktree status before removal;
- remove only the three clean stale worktrees above;
- delete only the corresponding local stale/duplicate refs and other local refs proven patch-equivalent, contained in main, or intentionally superseded;
- verify `main` remains clean except user-owned `.codex-remote-attachments/`, and `HEAD` equals `origin/main`;
- push any evidence-only update to `main`.

## Completed consolidation

- Removed the three clean stale worktree directories: `Trendplus2-gov`, `Trendplus2-maincheck`, and `Trendplus2-rq96-land`; pruned stale worktree metadata.
- Deleted 55 local stale/duplicate branch refs after verifying every deleted ref had a corresponding remote ref. This included the old backup/mixed, June/August Codex audit/feature branches, duplicate RQ branches, the stale RQ96 alternate, and the two local branches that were already behind their remote tips.
- Preserved the remote refs for recovery; no remote branch was deleted.
- Preserved local refs associated with dirty detached worktrees: `cursor/obs10-dashboard-honesty`, `cursor/rl10-advisory-calibration`, `cursor/rq96-land`, and `cursor/rq96-observed-inventory-snapshot`.
- Current local branch refs after cleanup: `main` plus those four recovery anchors.
- No uncommitted content was deleted from `Trendplus2-mainlatest`, `Trendplus2-obs10`, `Trendplus2-rl10`, or `Trendplus2-rq96`.

## Final verification

- `main` remained clean except user-owned `.codex-remote-attachments/` and this evidence file before its evidence-only commit.
- Main implementation and previous evidence delivery remained on `origin/main`; the branch cleanup itself changes only local refs/worktree metadata plus this run log.
- Residual blocker: the four dirty worktrees require their owning tasks to either commit/hand off or explicitly discard their changes before they can be removed.
