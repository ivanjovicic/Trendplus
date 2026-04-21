using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Services.Startup
{
    internal static class DbConnectionHelper
    {
        /// <summary>
        /// Executes a lightweight SQL probe (SELECT 1) against the provided <see cref="DbContext"/> with retries.
        /// Returns true when the probe succeeds, false when all attempts fail or cancellation occurs.
        /// </summary>
        public static async Task<bool> TryExecuteSqlProbeAsync(DbContext dbContext, ILogger logger, CancellationToken ct, int maxAttempts = 3, TimeSpan? initialDelay = null)
        {
            initialDelay ??= TimeSpan.FromSeconds(2);
            var delay = initialDelay.Value;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (ct.IsCancellationRequested)
                    return false;

                try
                {
                    var sw = Stopwatch.StartNew();
                    // Execute a minimal probe. EF will open and close the connection for this call.
                    await dbContext.Database.ExecuteSqlRawAsync("SELECT 1;", ct);
                    sw.Stop();
                    logger?.LogInformation("DB probe succeeded for {DbContext} in {ElapsedMs}ms (attempt {Attempt}/{MaxAttempts})", dbContext.GetType().Name, sw.ElapsedMilliseconds, attempt, maxAttempts);
                    return true;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    logger?.LogInformation("DB probe cancelled for {DbContext}", dbContext.GetType().Name);
                    return false;
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "DB probe attempt {Attempt}/{MaxAttempts} failed for {DbContext}", attempt, maxAttempts, dbContext.GetType().Name);
                    if (attempt == maxAttempts)
                    {
                        logger?.LogError(ex, "DB probe ultimately failed for {DbContext} after {Attempts} attempts", dbContext.GetType().Name, maxAttempts);
                        return false;
                    }

                    try
                    {
                        await Task.Delay(delay, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return false;
                    }

                    // Exponential backoff with a sensible cap.
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
                }
            }

            return false;
        }
    }
}
