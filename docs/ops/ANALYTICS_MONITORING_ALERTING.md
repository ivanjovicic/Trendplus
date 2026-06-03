# Analytics Monitoring And Alerting Plan

This plan describes how Trendplus should watch analytics health and what people should do when something goes wrong.

It is a monitoring and response plan, not an implementation claim. It does not assume that email, Slack, Teams, or webhooks are already wired.

## What we monitor

| Signal | What it means | Severity |
|---|---|---|
| Refresh critical | Analytics freshness is marked critical and the data should not be trusted for decisions | Critical |
| Last success older than 72h | The last successful refresh is too old for normal operational use | Warning or critical, depending on business tolerance |
| Failure after success | A previously healthy refresh started failing after a successful run | Warning |
| Worker not running | The refresh worker is stopped, paused, or not available in the expected process | Critical |
| Repeated endpoint errors | The same analytics endpoint keeps returning errors over multiple checks | Warning or critical if persistent |
| Stale cache warning | Cache state suggests stale or unsafe reads | Warning |
| Import failed | Import did not complete successfully or stopped before the data was usable | Critical |

## Severity model

- `info`
  - something is worth noting, but it is not immediately dangerous
  - example: refresh completed with a minor warning

- `warning`
  - something needs attention soon
  - example: last successful refresh is older than expected, but data is still partially usable

- `critical`
  - analytics should not be trusted without manual verification
  - example: refresh is critical, worker is down, or import failed

## Response actions

When a signal fires, the response should be chosen from this list:

1. Check worker status
2. Check import
3. Run manual refresh
4. Clear cache if safe
5. Inspect logs

### Recommended response mapping

| Signal | First response | Follow-up |
|---|---|---|
| Refresh critical | Check worker status | Inspect logs, then run manual refresh if the worker is healthy |
| Last success older than 72h | Check worker status | Inspect logs and refresh history |
| Failure after success | Inspect logs | Check worker status and import history |
| Worker not running | Check worker status | Run manual refresh only after the worker is confirmed available |
| Repeated endpoint errors | Inspect logs | Clear cache if safe, then re-check the endpoint |
| Stale cache warning | Clear cache if safe | Re-check the cache status and endpoint freshness |
| Import failed | Check import | Inspect logs, then rerun import only after the root cause is understood |

## Future alert channels

Future alert delivery can be extended to:

- email
- Slack
- Teams
- webhook

Important:
- these are future channels, not current guarantees
- do not claim an email alert exists unless the implementation actually sends one

## Operational notes

- `critical` freshness means the UI should clearly warn users that decisions should not be made without checking refresh status.
- A stale cache warning does not always mean data is wrong, but it does mean the cache should be checked before relying on a report.
- Repeated endpoint errors should be treated as a pattern, not a one-off failure.
- Manual refresh and cache clear are operational actions and should be used carefully when they are safe and authorized.

## What the UI should say

When freshness is critical, the UI copy should be:

> Podaci su kritično zastareli. Ne preporučuje se donošenje odluka bez provere osvežavanja.

This exact copy is already present in the analytics refresh status banner. If the banner changes later, the critical state must remain equally obvious or the docs should be updated to describe the new gap.

## What this plan does not claim

- It does not claim that email alerts are already implemented.
- It does not claim Slack, Teams, or webhook delivery is already active.
- It does not replace auth, authorization, or operational access control.
