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
            var status = ResolvePublicStatus(isReady, isDegradedSuccess, initialization, readiness.Reason);
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

    /// <summary>
    /// Maps internal readiness state to the public <c>/ready</c> status vocabulary.
    /// Schema-contract failures must surface as <c>failed</c>, never as <c>warming_up</c>.
    /// </summary>
    internal static string ResolvePublicStatus(
        bool isReady,
        bool isDegradedSuccess,
        StartupReadinessState.DatabaseInitializationSnapshot initialization,
        string reason)
    {
        if (isReady)
            return isDegradedSuccess ? "degraded" : "healthy";

        if (IsSchemaContractFailure(initialization, reason)
            || string.Equals(initialization.State, "failed", StringComparison.Ordinal))
        {
            return "failed";
        }

        if (reason.Contains("warmup", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("starting", StringComparison.OrdinalIgnoreCase))
        {
            return "warming_up";
        }

        return "degraded";
    }

    private static bool IsSchemaContractFailure(
        StartupReadinessState.DatabaseInitializationSnapshot initialization,
        string reason) =>
        string.Equals(reason, "schema_incompatible", StringComparison.Ordinal)
        || string.Equals(reason, "schema_verification_failed", StringComparison.Ordinal)
        || string.Equals(initialization.FailureCategory, "schema_contract_incompatible", StringComparison.Ordinal)
        || string.Equals(initialization.FailureCategory, "schema_verification_failed", StringComparison.Ordinal);

    private static long? ResolveProbeLatency(long? defaultLatencyMs, long? analyticsLatencyMs)
    {
        if (defaultLatencyMs.HasValue && analyticsLatencyMs.HasValue)
            return Math.Max(defaultLatencyMs.Value, analyticsLatencyMs.Value);

        return defaultLatencyMs ?? analyticsLatencyMs;
    }
}
