# Decision Pulse delivery state contract (RQ116)

Date: 2026-08-24
Repo: `ivanjovicic/Trendplus`

## Verdict

Decision Pulse delivery stays honest about whether a Pulse email was actually sent, skipped, or blocked by configuration.
Current main already distinguishes the local attempt-state contract without inventing live SMTP proof.

## State behavior on current main

| State | Meaning |
|---|---|
| `source_error` | The feed itself was not successful, so email delivery must not pretend success. |
| `recipients_missing` | No valid recipients were configured for the request or schedule. |
| `smtp_disabled` | The feed is available in-app, but SMTP delivery is disabled. |
| `emailed` | The email path actually ran with configured recipients and an enabled email service. |

## Durable receipt

- `DecisionPulseSchedulerWorker` records the schedule run result after each attempt.
- `DecisionPulseScheduleService.MarkRunResultAsync` persists `LastRunAtUtc`, `LastRunStatus`, and `LastError`.
- That makes the queue/worker result visible without treating the email path as successful when it was skipped or blocked.

## Operator rule

- Do not claim live SMTP delivery unless a real configured send was executed.
- Missing SMTP or missing recipients are explicit non-delivery states, not green delivery.

