using System.Globalization;

namespace Api.Services.Startup;

public static class StartupReadinessEndpoints
{
    public static void MapStartupReadinessEndpoint(
        this WebApplication app,
        Func<HttpContext, string> resolveProviderName)
    {
        app.MapGet("/ready", (StartupReadinessState readiness, HttpContext context) =>
        {
            var initialization = readiness.DatabaseInitialization;
            var isReady = readiness.IsReady;
            var isDegradedSuccess = initialization.State == "completed_with_errors";
            var status = isReady
                ? isDegradedSuccess ? "degraded" : "healthy"
                : readiness.Reason.Contains("warmup", StringComparison.OrdinalIgnoreCase)
                    || readiness.Reason.Contains("starting", StringComparison.OrdinalIgnoreCase)
                    ? "warming_up"
                    : "degraded";
            var retryAfterSeconds = isReady ? (int?)null : 5;
            if (!isReady)
            {
                context.Response.Headers.RetryAfter = retryAfterSeconds!.Value.ToString(CultureInfo.InvariantCulture);
            }

            var payload = new
            {
                status,
                provider = resolveProviderName(context),
                ready = isReady,
                db = new
                {
                    ok = readiness.DefaultDb.Ok && readiness.AnalyticsDb.Ok,
                    latencyMs = ResolveProbeLatency(readiness.DefaultDb.LatencyMs, readiness.AnalyticsDb.LatencyMs)
                },
                timestampUtc = DateTimeOffset.UtcNow,
                retryAfterSeconds,
                reason = readiness.Reason,
                startedAtUtc = readiness.StartedAtUtc,
                readyAtUtc = readiness.ReadyAtUtc,
                lastProbeAtUtc = readiness.LastProbeAtUtc,
                databaseInitialization = new
                {
                    state = initialization.State,
                    required = initialization.Required,
                    failureCategory = initialization.FailureCategory,
                    attempts = initialization.Attempts,
                    lastAttemptAtUtc = initialization.LastAttemptAtUtc,
                    completedAtUtc = initialization.CompletedAtUtc
                }
            };

            return isReady
                ? Results.Ok(payload)
                : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
        }).AllowAnonymous();
    }

    private static long? ResolveProbeLatency(long? defaultLatencyMs, long? analyticsLatencyMs)
    {
        if (defaultLatencyMs.HasValue && analyticsLatencyMs.HasValue)
            return Math.Max(defaultLatencyMs.Value, analyticsLatencyMs.Value);

        return defaultLatencyMs ?? analyticsLatencyMs;
    }
}
