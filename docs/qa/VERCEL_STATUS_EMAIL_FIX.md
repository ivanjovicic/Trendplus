# Vercel Status Email Fix

Updated: 2026-08-05

## Problem

Vercel commit/deploy status can fail when the commit author email does not match a GitHub-verified email address or a GitHub-provided no-reply email.

For this repo, the recent local commits are authored with:

- `Ivan Jovicic <ivanjovicic1986@gmail.com>`
- local Git config:
  - `user.name = Ivan Jovicic`
  - `user.email = ivanjovic1986@gmail.com`

That is only safe if `ivanjovicic1986@gmail.com` is verified on the GitHub account that owns the repo. If it is not verified, Vercel may keep rejecting the commit/status flow.

## Evidence

- `git config user.name` -> `Ivan Jovicic`
- `git config user.email` -> `ivanjovicic1986@gmail.com`
- recent commits in this repo use `Ivan Jovicic <ivanjovicic1986@gmail.com>`

## 2026-08-05 Repo Check

- `git config user.name` -> `Ivan Jovicic`
- `git config user.email` -> `ivanjovicic1986@gmail.com`
- recent commits in this repo:
  - `a1b9231 Ivan Jovicic <ivanjovicic1986@gmail.com>`
  - `2ab36cb Ivan Jovicic <ivanjovicic1986@gmail.com>`
  - `c5e6ce6 Ivan Jovicic <ivanjovicic1986@gmail.com>`
- older history includes at least one desktop-authored commit:
  - `8fb6141 Ivan <Ivan@DESKTOP-V877DAD>`
- conclusion:
  - the current branch already uses a consistent repo-local identity that matches the documented fix path
  - if GitHub does not verify `ivanjovicic1986@gmail.com`, switch to a GitHub no-reply email before the next commit

## What GitHub accepts

GitHub supports either:

- a verified email address on the account
- a GitHub no-reply address

For no-reply commits, GitHub documents two common formats:

- `ID+USERNAME@users.noreply.github.com`
- `USERNAME@users.noreply.github.com`

Which one applies depends on the account age and privacy settings.

## What Vercel expects

Vercel checks commit identity against the Git provider. If commit author email does not match a verified GitHub identity, deployment/status updates can fail. If verified commits are enabled on the project, unverified commits will not deploy.

Vercel also requires an exact email match when commit author details are compared, so plus-addresses and no-reply formats must match exactly.

## Remediation

If `ivanjovicic1986@gmail.com` is verified on GitHub:

1. Keep the current Git config.
2. Create a small new commit.
3. Push it to `origin/main`.
4. Confirm Vercel accepts the new commit status.

If it is not verified:

1. Switch local Git to a verified GitHub email or GitHub no-reply email.
2. Create a small new commit with the corrected author email.
3. Push the commit.
4. Recheck Vercel status.

## Commands

Check the current local identity:

```powershell
git config user.name
git config user.email
git log --format="%h %an <%ae>" -5
```

Set a verified email for this repo only:

```powershell
git config user.email "verified-email@example.com"
```

Or use a GitHub no-reply address:

```powershell
git config user.email "ID+USERNAME@users.noreply.github.com"
```

Verify the result:

```powershell
git config --get user.email
git log --format="%h %an <%ae>" -5
```

## Verification after the fix

After the next push, confirm:

1. The new commit author email matches the verified GitHub email or GitHub no-reply format.
2. Vercel shows a successful status/deployment for that commit.
3. The same blocker does not recur on the next analytics commit.
